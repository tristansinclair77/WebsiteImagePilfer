using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using WebsiteImagePilfer.Models;
using WebsiteImagePilfer.Helpers;
using IOPath = System.IO.Path;

namespace WebsiteImagePilfer.Services
{
    public class ImageDownloader
    {
        private const string STATUS_CHECKING = "Checking...";
      private const string STATUS_FINDING_FULLRES = "Finding full-res...";
        private const string STATUS_DOWNLOADING = "Downloading...";
        private const string STATUS_DONE = "? Done";
    private const string STATUS_BACKUP = "? Backup";
      private const string STATUS_DUPLICATE = "? Duplicate";
    private const string STATUS_SKIPPED_SIZE = "? Skipped (too small)";
        private const string STATUS_SKIPPED_JPG = "? Skipped (not JPG)";
        private const string STATUS_SKIPPED_PNG = "? Skipped (not PNG)";
        private const string STATUS_FAILED = "? Failed";
        private const string STATUS_CANCELLED = "? Cancelled";

        private const int HEAD_REQUEST_TIMEOUT_SECONDS = 5;

        private static readonly string[] SIZE_PATTERNS = { "_800x800", "_small", "_medium", "_thumb", "_preview", "-thumb", "-preview" };

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
        item.Status = STATUS_CHECKING;
      var startTime = DateTime.Now;

    var (urlToDownload, usedBackup) = await ResolveDownloadUrlAsync(item.Url, cancellationToken);

       // Use existing filename or extract from URL
        if (string.IsNullOrEmpty(item.FileName) || item.FileName.StartsWith("image_"))
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
      item.Status = STATUS_DUPLICATE;
          return;
           }

       item.Status = STATUS_DOWNLOADING;
        var downloadStart = DateTime.Now;

      // Download the image
 var imageBytes = await _httpClient.GetByteArrayAsync(urlToDownload, cancellationToken);
            var downloadElapsed = (DateTime.Now - downloadStart).TotalMilliseconds;

                // Filter by size if enabled
    if (_settings.FilterBySize && imageBytes.Length < _settings.MinimumImageSize)
        {
 item.Status = STATUS_SKIPPED_SIZE;
       item.FileName = $"{item.FileName} ({imageBytes.Length} bytes)";
    return;
   }

            await File.WriteAllBytesAsync(filePath, imageBytes, cancellationToken);

                // Set thumbnail path if enabled
      if (_settings.ShowThumbnails)
      {
     try { item.ThumbnailPath = filePath; }
   catch { /* Thumbnail generation failed, continue without it */ }
           }

            item.Status = usedBackup ? STATUS_BACKUP : STATUS_DONE;
 item.ErrorMessage = "";

                var totalElapsed = (DateTime.Now - startTime).TotalMilliseconds;
 System.Diagnostics.Debug.WriteLine($"Downloaded {item.FileName} in {totalElapsed}ms (download: {downloadElapsed}ms)");
            }
    catch (OperationCanceledException)
       {
         item.Status = STATUS_CANCELLED;
        throw;
            }
   catch (HttpRequestException ex)
      {
             item.Status = STATUS_FAILED;
        item.ErrorMessage = $"Network error: {ex.Message}";
      System.Diagnostics.Debug.WriteLine($"HTTP error for {item.FileName}: {ex.Message}");
 }
 catch (Exception ex)
            {
              item.Status = STATUS_FAILED;
     item.ErrorMessage = ex.Message;
                System.Diagnostics.Debug.WriteLine($"Error for {item.FileName}: {ex.Message}");
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
     if (previewUrl.Contains("/thumbnail/"))
      {
   var fullResUrl = previewUrl.Replace("/thumbnail/", "/");
   if (await TestUrlExistsAsync(fullResUrl, cancellationToken))
   return fullResUrl;
     }

                // Pattern 2: Remove size suffixes
       foreach (var pattern in SIZE_PATTERNS)
      {
   if (previewUrl.Contains(pattern))
     {
   var fullResUrl = previewUrl.Replace(pattern, "");
               if (await TestUrlExistsAsync(fullResUrl, cancellationToken))
       return fullResUrl;
         }
            }

     // No transformation needed/found - return original URL
 return previewUrl;
  }
            catch (Exception ex)
        {
       System.Diagnostics.Debug.WriteLine($"TryFindFullResolutionUrlAsync exception: {ex.Message}");
    return previewUrl;
}
    }

        private bool IsKemonoUrl(string url, out string? result)
   {
          if (!url.Contains("kemono.cr"))
{
             result = null;
                return false;
   }

            if (url.Contains("/thumbnail/"))
            {
             // Transform preview to full-res
result = url.Replace("/thumbnail/", "/").Replace("img.kemono.cr", "n4.kemono.cr");
   System.Diagnostics.Debug.WriteLine("Kemono.cr preview detected, transforming to full-res URL");
       return true;
   }
      
            if (url.Contains("n4.kemono.cr") || url.Contains("n5.kemono.cr"))
            {
     // Already a full-res URL
       result = url;
    System.Diagnostics.Debug.WriteLine("Kemono.cr full-res URL detected (already full-res)");
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
         using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(HEAD_REQUEST_TIMEOUT_SECONDS));
    using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

var response = await _httpClient.SendAsync(request, linkedCts.Token);
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
                filterReason = STATUS_SKIPPED_JPG;
            return false;
            }
         
            if (_settings.FilterPngOnly && extension != ".png")
      {
            filterReason = STATUS_SKIPPED_PNG;
  return false;
     }

  filterReason = null;
    return true;
        }
    }
}
