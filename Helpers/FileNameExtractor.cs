using System;
using System.Web;
using IOPath = System.IO.Path;

namespace WebsiteImagePilfer.Helpers
{
    public static class FileNameExtractor
    {
    public static string ExtractFromUrl(string imageUrl)
      {
       var uri = new Uri(imageUrl);
     var fileName = "";

   // KEMONO.CR: Check for ?f= query parameter (contains actual filename)
            if (uri.Query.Contains("?f=") || uri.Query.Contains("&f="))
     {
       var queryParams = HttpUtility.ParseQueryString(uri.Query);
 var fParam = queryParams["f"];
           if (!string.IsNullOrEmpty(fParam))
          {
   fileName = fParam;
        }
          }

            // Fallback to path filename if no query parameter
            if (string.IsNullOrEmpty(fileName))
        {
      fileName = IOPath.GetFileName(uri.LocalPath);
      }

   // Final fallback if still empty
    if (string.IsNullOrEmpty(fileName) || !IOPath.HasExtension(fileName))
{
     fileName = $"image_{DateTime.Now:yyyyMMddHHmmss}_{Guid.NewGuid().ToString().Substring(0, 8)}.jpg";
   }

 return SanitizeFileName(fileName);
    }

        public static string SanitizeFileName(string fileName)
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
