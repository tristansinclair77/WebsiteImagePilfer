using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using WebsiteImagePilfer.Models;
using WebsiteImagePilfer.Helpers;
using WebsiteImagePilfer.Constants;
using static WebsiteImagePilfer.Constants.AppConstants;
using IOPath = System.IO.Path;

namespace WebsiteImagePilfer.Services
{
    public class ImageDownloader
    {
        private readonly HttpClient _httpClient;
        private readonly DownloadSettings _settings;
        private readonly string _downloadFolder;

        public ImageDownloader(HttpClient httpClient, DownloadSettings settings, string downloadFolder)
     {
     _httpClient = httpClient;
       _settings = settings;
      _downloadFolder = downloadFolder;
        }

    public async Task DownloadSingleItemAsync(ImageDownloadItem item, CancellationToken cancellationToken)
     {
     try
    {
     item.Status = Status.Checking;
      var startTime = DateTime.Now;

 var (urlToDownload, usedBackup) = await ResolveDownloadUrlAsync(item.Url, cancellationToken).ConfigureAwait(false);

// Use existing filename or extract from URL
       if (string.IsNullOrEmpty(item.FileName) || item.FileName.StartsWith("image_", StringComparison.Ordinal))
   item.FileName = FileNameExtractor.ExtractFromUrl(urlToDownload);

       // Check file type filters
         if (!PassesFileTypeFilter(item.FileName, out string? filterReason))
       {
    item.Status = filterReason!;
  item.FileName = $"{item.FileName} (Filtered: {IOPath.GetExtension(item.FileName)})";
 return;
     }

    // Check for duplicate
         var filePath = IOPath.Combine(_downloadFolder, item.FileName);
        if (File.Exists(filePath))
     {
  item.Status = Status.Duplicate;
return;
    }

   item.Status = Status.Downloading;
   var downloadStart = DateTime.Now;

          // Download the image
  var imageBytes = await _httpClient.GetByteArrayAsync(urlToDownload, cancellationToken).ConfigureAwait(false);
    var downloadElapsed = (DateTime.Now - downloadStart).TotalMilliseconds;

// Filter by size if enabled
  if (_settings.FilterBySize && imageBytes.Length < _settings.MinimumImageSize)
 {
   item.Status = Status.SkippedSize;
       item.FileName = $"{item.FileName} ({imageBytes.Length} bytes)";
     return;
   }

await File.WriteAllBytesAsync(filePath, imageBytes, cancellationToken).ConfigureAwait(false);

// Set thumbnail path if enabled
 if (_settings.ShowThumbnails)
    {
try { item.ThumbnailPath = filePath; }
   catch { /* Thumbnail generation failed, continue without it */ }
     }

        item.Status = usedBackup ? Status.Backup : Status.Done;
  item.ErrorMessage = "";

 var totalElapsed = (DateTime.Now - startTime).TotalMilliseconds;
   Logger.Info($"Downloaded {item.FileName} in {totalElapsed}ms (download: {downloadElapsed}ms)");
       }
            catch (OperationCanceledException)
    {
            item.Status = Status.Cancelled;
  throw;
   }
   catch (HttpRequestException ex)
       {
   item.Status = Status.Failed;
item.ErrorMessage = $"Network error: {ex.Message}";
  Logger.Error($"HTTP error for {item.FileName}", ex);
    }
catch (Exception ex)
    {
    item.Status = Status.Failed;
   item.ErrorMessage = ex.Message;
  Logger.Error($"Error downloading {item.FileName}", ex);
      }
      }

        private async Task<(string url, bool usedBackup)> ResolveDownloadUrlAsync(string originalUrl, CancellationToken cancellationToken)
  {
            if (_settings.SkipFullResolutionCheck)
     return (originalUrl, false);

 try
       {
         var fullResUrl = await TryFindFullResolutionUrlAsync(originalUrl, cancellationToken);
   
     if (fullResUrl == null)
         return (originalUrl, true); // No full-res found, use original as backup
    
  if (fullResUrl == originalUrl)
     return (originalUrl, false); // URL is already full-res
       
   return (fullResUrl, false); // Found different full-res URL
 }
       catch
   {
    return (originalUrl, true); // Error finding full-res, use backup
    }
}

  private async Task<string?> TryFindFullResolutionUrlAsync(string previewUrl, CancellationToken cancellationToken)
        {
    try
     {
     // KEMONO.CR SPECIFIC PATTERN - NO HEAD REQUEST NEEDED
   if (IsKemonoUrl(previewUrl, out string? kemonoResult))
  return kemonoResult;

   // Pattern 1: Remove "/thumbnail/" from path
      if (previewUrl.Contains("/thumbnail/", StringComparison.Ordinal))
  {
var fullResUrl = previewUrl.Replace("/thumbnail/", "/");
       if (await TestUrlExistsAsync(fullResUrl, cancellationToken).ConfigureAwait(false))
      return fullResUrl;
     }

   // Pattern 2: Remove size suffixes
foreach (var pattern in Images.SizePatterns)
      {
  if (previewUrl.Contains(pattern, StringComparison.Ordinal))
  {
  var fullResUrl = previewUrl.Replace(pattern, "");
       if (await TestUrlExistsAsync(fullResUrl, cancellationToken).ConfigureAwait(false))
  return fullResUrl;
    }
   }

  // No transformation needed/found - return original URL
        return previewUrl;
            }
       catch (Exception ex)
 {
  Logger.Error($"TryFindFullResolutionUrlAsync exception", ex);
   return previewUrl;
 }
        }

   private bool IsKemonoUrl(string url, out string? result)
        {
 if (!url.Contains("kemono.cr", StringComparison.OrdinalIgnoreCase))
 {
  result = null;
  return false;
    }

  if (url.Contains("/thumbnail/", StringComparison.Ordinal))
 {
  // Transform preview to full-res
    result = url.Replace("/thumbnail/", "/").Replace("img.kemono.cr", "n4.kemono.cr");
       Logger.Debug("Kemono.cr preview detected, transforming to full-res URL");
    return true;
   }
     
            if (url.Contains("n4.kemono.cr", StringComparison.Ordinal) || url.Contains("n5.kemono.cr", StringComparison.Ordinal))
     {
// Already a full-res URL
 result = url;
 Logger.Debug("Kemono.cr full-res URL detected (already full-res)");
       return true;
    }

     result = null;
  return false;
   }

        private async Task<bool> TestUrlExistsAsync(string url, CancellationToken cancellationToken)
        {
      try
     {
 var request = new HttpRequestMessage(HttpMethod.Head, url);
    using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(Network.HeadRequestTimeoutSeconds));
      using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

      var response = await _httpClient.SendAsync(request, linkedCts.Token).ConfigureAwait(false);
  return response.IsSuccessStatusCode;
}
    catch
  {
 return false;
  }
     }

        private bool PassesFileTypeFilter(string fileName, out string? filterReason)
        {
     var extension = IOPath.GetExtension(fileName).ToLowerInvariant();
   
       if (_settings.FilterJpgOnly && extension != ".jpg" && extension != ".jpeg")
       {
      filterReason = Status.SkippedJpg;
   return false;
      }
    
         if (_settings.FilterPngOnly && extension != ".png")
  {
 filterReason = Status.SkippedPng;
     return false;
    }

    filterReason = null;
  return true;
        }
    }
}
