using System;
using System.Web;
using IOPath = System.IO.Path;

namespace WebsiteImagePilfer.Helpers
{
    public static class FileNameExtractor
    {
        private const int MAX_FILENAME_LENGTH = 200;
        private const int GUID_SHORT_LENGTH = 8;
        private const string FALLBACK_EXTENSION = ".jpg";
        private const string QUERY_PARAM_FILENAME = "f";

        public static string ExtractFromUrl(string imageUrl)
        {
            var uri = new Uri(imageUrl);
            var fileName = "";

            // Check for ?f= query parameter (contains actual filename)
            if (uri.Query.Contains($"?{QUERY_PARAM_FILENAME}=") || uri.Query.Contains($"&{QUERY_PARAM_FILENAME}="))
            {
                var queryParams = HttpUtility.ParseQueryString(uri.Query);
                var fParam = queryParams[QUERY_PARAM_FILENAME];
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
            if (sanitized.Length > MAX_FILENAME_LENGTH)
            {
                var extension = IOPath.GetExtension(sanitized);
                sanitized = sanitized[..(MAX_FILENAME_LENGTH - extension.Length)] + extension;
            }

            return sanitized;
        }

        private static string GenerateFallbackFileName() =>
            $"image_{DateTime.Now:yyyyMMddHHmmss}_{Guid.NewGuid().ToString("N")[..GUID_SHORT_LENGTH]}{FALLBACK_EXTENSION}";
    }
}
