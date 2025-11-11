using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using WebsiteImagePilfer.Models;
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
   item.Status = "Checking...";
        var startTime = DateTime.Now;

       string urlToDownload = item.Url;
            bool usedBackup = false;

// Try to find full-resolution version if setting allows
                if (!_settings.SkipFullResolutionCheck)
  {
   try
  {
             item.Status = "Finding full-res...";
        var fullResUrl = await TryFindFullResolutionUrlAsync(item.Url, cancellationToken);

        if (fullResUrl != null && fullResUrl != item.Url)
 {
        // Found a DIFFERENT full-res URL (transformation succeeded)
          urlToDownload = fullResUrl;
           usedBackup = false;
      }
            else if (fullResUrl == item.Url)
           {
         // URL is already full-res (no transformation needed)
    usedBackup = false;
              }
       else
       {
            // No full-res found, using original preview URL as backup
    usedBackup = true;
            }
       }
         catch
   {
       usedBackup = true; // Error finding full-res, using backup
    }
     }
      else
      {
        // Skip check is enabled, using whatever URL we scanned
       usedBackup = false;
            }

      // USE THE FILENAME WE ALREADY HAVE from the scan!
            var fileName = item.FileName;

       // Only generate a new filename if we don't have one yet
             if (string.IsNullOrEmpty(fileName) || fileName.StartsWith("image_"))
                {
fileName = ExtractFileNameFromUrl(urlToDownload);
     item.FileName = fileName;
      }

    // Check file type filters before downloading
        var extension = IOPath.GetExtension(fileName).ToLowerInvariant();
         if (_settings.FilterJpgOnly && extension != ".jpg" && extension != ".jpeg")
         {
        item.Status = "? Skipped (not JPG)";
   item.FileName = $"{fileName} (Filtered: {extension})";
   return;
      }
   if (_settings.FilterPngOnly && extension != ".png")
              {
item.Status = "? Skipped (not PNG)";
          item.FileName = $"{fileName} (Filtered: {extension})";
return;
        }

         // Check if file already exists
 var filePath = IOPath.Combine(_downloadFolder, fileName);
  if (File.Exists(filePath))
 {
      item.Status = "? Duplicate";
      return;
      }

        item.Status = "Downloading...";
            var downloadStart = DateTime.Now;

     // Download the image
       var imageBytes = await _httpClient.GetByteArrayAsync(urlToDownload, cancellationToken);
     var downloadElapsed = (DateTime.Now - downloadStart).TotalMilliseconds;

        // Filter by size if setting is enabled
        if (_settings.FilterBySize && imageBytes.Length < _settings.MinimumImageSize)
    {
                item.Status = "? Skipped (too small)";
     item.FileName = $"{fileName} ({imageBytes.Length} bytes)";
 return;
          }

                await File.WriteAllBytesAsync(filePath, imageBytes, cancellationToken);

            // Create thumbnail for preview
       if (_settings.ShowThumbnails)
{
            try
        {
    item.ThumbnailPath = filePath;
        }
  catch
   {
                 // Thumbnail generation failed, continue without it
             }
          }

    if (usedBackup)
         {
       item.Status = "? Backup";
      }
         else
          {
     item.Status = "? Done";
 }

   // Clear error message on successful download
       item.ErrorMessage = "";

    var totalElapsed = (DateTime.Now - startTime).TotalMilliseconds;
       System.Diagnostics.Debug.WriteLine($"Downloaded {fileName} in {totalElapsed}ms (download: {downloadElapsed}ms)");
 }
            catch (OperationCanceledException)
            {
  item.Status = "? Cancelled";
          throw;
  }
            catch (HttpRequestException ex)
        {
            item.Status = "? Failed";
          item.ErrorMessage = $"Network error: {ex.Message}";
    System.Diagnostics.Debug.WriteLine($"HTTP error for {item.FileName}: {ex.Message}");
       }
   catch (Exception ex)
            {
   item.Status = "? Failed";
            item.ErrorMessage = ex.Message;
        System.Diagnostics.Debug.WriteLine($"Error for {item.FileName}: {ex.Message}");
        }
        }

     private async Task<string?> TryFindFullResolutionUrlAsync(string previewUrl, CancellationToken cancellationToken)
        {
  try
     {
 // KEMONO.CR SPECIFIC PATTERN - NO HEAD REQUEST NEEDED
 if (previewUrl.Contains("kemono.cr"))
           {
         if (previewUrl.Contains("/thumbnail/"))
        {
    // Transform preview to full-res
          var fullResUrl = previewUrl.Replace("/thumbnail/", "/").Replace("img.kemono.cr", "n4.kemono.cr");
      System.Diagnostics.Debug.WriteLine($"Kemono.cr preview detected, transforming to full-res URL");
          return fullResUrl;
   }
              else if (previewUrl.Contains("n4.kemono.cr") || previewUrl.Contains("n5.kemono.cr"))
{
  // Already a full-res URL
               System.Diagnostics.Debug.WriteLine($"Kemono.cr full-res URL detected (already full-res)");
   return previewUrl;
         }
        }

       // Pattern 1: Remove "/thumbnail/" from path
           if (previewUrl.Contains("/thumbnail/"))
      {
           var fullResUrl = previewUrl.Replace("/thumbnail/", "/");
           if (await TestUrlExistsAsync(fullResUrl, cancellationToken))
        {
     return fullResUrl;
         }
                }

        // Pattern 2: Remove size suffixes
      var sizePatterns = new[] { "_800x800", "_small", "_medium", "_thumb", "_preview", "-thumb", "-preview" };
             foreach (var pattern in sizePatterns)
             {
    if (previewUrl.Contains(pattern))
     {
         var fullResUrl = previewUrl.Replace(pattern, "");
          if (await TestUrlExistsAsync(fullResUrl, cancellationToken))
    {
      return fullResUrl;
 }
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

        private async Task<bool> TestUrlExistsAsync(string url, CancellationToken cancellationToken)
        {
            try
            {
           var request = new HttpRequestMessage(HttpMethod.Head, url);

         // Add timeout for HEAD request (5 seconds max)
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
              using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

           var response = await _httpClient.SendAsync(request, linkedCts.Token);
       return response.IsSuccessStatusCode;
    }
            catch
 {
     return false;
     }
        }

     private string ExtractFileNameFromUrl(string urlToDownload)
        {
   var uri = new Uri(urlToDownload);
string fileName = "";

  // Check for query parameter first
   if (uri.Query.Contains("?f=") || uri.Query.Contains("&f="))
            {
      var queryParams = HttpUtility.ParseQueryString(uri.Query);
                var fParam = queryParams["f"];
         if (!string.IsNullOrEmpty(fParam))
     {
    fileName = fParam;
                }
    }

     // Fallback to path filename
      if (string.IsNullOrEmpty(fileName))
         {
          fileName = IOPath.GetFileName(uri.LocalPath);
   }

          // Final fallback
  if (string.IsNullOrEmpty(fileName) || !IOPath.HasExtension(fileName))
            {
    fileName = $"image_{DateTime.Now:yyyyMMddHHmmss}_{Guid.NewGuid().ToString().Substring(0, 8)}.jpg";
         }

            return SanitizeFileName(fileName);
 }

        private string SanitizeFileName(string fileName)
  {
            // Remove invalid path characters
  var invalidChars = IOPath.GetInvalidFileNameChars();
     var sanitized = string.Join("_", fileName.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries));

        // Ensure filename is not too long (max 200 characters)
        if (sanitized.Length > 200)
            {
    var extension = IOPath.GetExtension(sanitized);
    sanitized = sanitized.Substring(0, 200 - extension.Length) + extension;
     }

     return sanitized;
        }
  }
}
