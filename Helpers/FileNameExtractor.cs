using System;
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

   // Final fallback if still empty
            if (string.IsNullOrEmpty(fileName) || !IOPath.HasExtension(fileName))
     fileName = GenerateFallbackFileName();

  return SanitizeFileName(fileName);
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
