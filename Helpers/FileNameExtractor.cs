using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using WebsiteImagePilfer.Constants;
using static WebsiteImagePilfer.Constants.AppConstants;
using IOPath = System.IO.Path;

namespace WebsiteImagePilfer.Helpers
{
    public static class FileNameExtractor
    {
        // Constants removed - using AppConstants

        public static string ExtractFromUrl(string imageUrl)
        {
            var uri = new Uri(imageUrl);
            var fileName = "";

            // Check for ?f= query parameter (contains actual filename)
            if (uri.Query.Contains($"?{Files.QueryParamFilename}=") || uri.Query.Contains($"&{Files.QueryParamFilename}="))
            {
                var queryParams = HttpUtility.ParseQueryString(uri.Query);
                var fParam = queryParams[Files.QueryParamFilename];
                if (!string.IsNullOrEmpty(fParam))
                    fileName = fParam;
            }

            // Fallback to path filename if no query parameter
            if (string.IsNullOrEmpty(fileName))
                fileName = IOPath.GetFileName(uri.LocalPath);

            // If we have a filename without extension, try to use the path as the filename
            if (!string.IsNullOrEmpty(fileName) && !IOPath.HasExtension(fileName))
            {
                // For URLs like /g/gen_01k9fp0nwgf83aqabs6mxsbg94, use the last path segment as name
                fileName = fileName + Files.FallbackExtension;
            }
            
            // Final fallback if still empty
            if (string.IsNullOrEmpty(fileName))
                fileName = GenerateFallbackFileName();

            return SanitizeFileName(fileName);
        }
        
        public static async Task<string> ExtractFromUrlWithContentTypeAsync(string imageUrl, HttpClient httpClient, CancellationToken cancellationToken)
        {
            var fileName = ExtractFromUrl(imageUrl);
            
            // If we already have an extension, return it
            if (IOPath.HasExtension(fileName) && fileName.EndsWith(Files.FallbackExtension) == false)
                return fileName;
            
            // Try to get the actual content type from the server
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Head, imageUrl);
                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(Network.HeadRequestTimeoutSeconds));
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
                
                var response = await httpClient.SendAsync(request, linkedCts.Token).ConfigureAwait(false);
                
                if (response.IsSuccessStatusCode && response.Content.Headers.ContentType != null)
                {
                    var contentType = response.Content.Headers.ContentType.MediaType;
                    var extension = GetExtensionFromContentType(contentType);
                    
                    if (!string.IsNullOrEmpty(extension))
                    {
                        // Replace the fallback extension with the correct one
                        var fileNameWithoutExt = IOPath.GetFileNameWithoutExtension(fileName);
                        fileName = fileNameWithoutExt + extension;
                    }
                }
            }
            catch
            {
                // If we can't get content type, just use what we have
            }
            
            return fileName;
        }
        
        private static string GetExtensionFromContentType(string contentType)
        {
            return contentType.ToLowerInvariant() switch
            {
                "image/jpeg" => ".jpg",
                "image/png" => ".png",
                "image/gif" => ".gif",
                "image/webp" => ".webp",
                "image/bmp" => ".bmp",
                "image/svg+xml" => ".svg",
                _ => ""
            };
        }

        public static string SanitizeFileName(string fileName)
        {
            // Remove invalid path characters
            var invalidChars = IOPath.GetInvalidFileNameChars();
            var sanitized = string.Join("_", fileName.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries));

            // Ensure filename is not too long
            if (sanitized.Length > Files.MaxFilenameLength)
            {
                var extension = IOPath.GetExtension(sanitized);
                sanitized = sanitized[..(Files.MaxFilenameLength - extension.Length)] + extension;
            }

            return sanitized;
        }

        private static string GenerateFallbackFileName() =>
            $"image_{DateTime.Now:yyyyMMddHHmmss}_{Guid.NewGuid().ToString("N")[..Files.GuidShortLength]}{Files.FallbackExtension}";
    }
}
