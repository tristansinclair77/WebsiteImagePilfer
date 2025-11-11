using System.Runtime.CompilerServices;

namespace WebsiteImagePilfer.Constants
{
    /// <summary>
    /// Application-wide constant values.
    /// Provides single source of truth for status strings, limits, and configuration.
    /// </summary>
    public static class AppConstants
    {
        /// <summary>
        /// Status strings for download items
        /// </summary>
        public static class Status
        {
            public const string Ready = "Ready";
            public const string Checking = "Checking...";
            public const string FindingFullRes = "Finding full-res...";
            public const string Downloading = "Downloading...";
            public const string Done = "? Done";
            public const string Backup = "? Backup";
            public const string Duplicate = "? Duplicate";
            public const string Cancelled = "? Canceled";
            public const string Failed = "? Failed";
            public const string Skipped = "? Skipped";
            public const string SkippedSize = "? Skipped (too small)";
            public const string SkippedJpg = "? Skipped (not JPG)";
            public const string SkippedPng = "? Skipped (not PNG)";
            public const string Ignored = "Ignored";
        }

        /// <summary>
        /// Network and HTTP timeouts
        /// </summary>
        public static class Network
        {
            public const int HttpTimeoutSeconds = 30;
            public const int HeadRequestTimeoutSeconds = 5;
            public const int PageLoadTimeoutSeconds = 60;
            public const int ImplicitWaitSeconds = 10;
        }

        /// <summary>
        /// Scanning configuration
        /// </summary>
        public static class Scanning
        {
            public const int FastScanWaitMs = 5000;
            public const int ThoroughScanCheckIntervalMs = 5000;
            public const int ThoroughScanMaxStableChecks = 3;
            public const int ScrollDelayMs = 1000;
            public const int RetryDelayMs = 2000;
            public const int MaxRetryCount = 3;
            public const int PageLoadTimeoutSeconds = 60;
        }

        /// <summary>
        /// File system limits
        /// </summary>
        public static class Files
        {
            public const int MaxFilenameLength = 200;
            public const int GuidShortLength = 8;
            public const string FallbackExtension = ".jpg";
            public const string QueryParamFilename = "f";
        }

        /// <summary>
        /// Preview and UI configuration
        /// </summary>
        public static class Preview
        {
            public const int MinDecodeWidth = 200;
            public const int QualityMultiplier = 2;
            public const int ColumnResizeDebounceMs = 500;
            public const int ColumnWidthMonitorIntervalMs = 100;
            public const int ColumnWidthChangeThreshold = 10;
        }

        /// <summary>
        /// Settings validation limits
        /// </summary>
        public static class Validation
        {
            public const int MinImageSize = 100;
            public const int MaxImageSize = 1_000_000_000;
            public const int MinItemsPerPage = 1;
            public const int MaxItemsPerPage = 1000;
            public const int MinMaxImagesToScan = 1;
            public const int MaxMaxImagesToScan = 10000;
        }

        /// <summary>
        /// Image URL patterns and extensions
        /// </summary>
        public static class Images
        {
            public static readonly string[] SizePatterns = 
            { "_800x800", "_small", "_medium", "_thumb", "_preview", "-thumb", "-preview" };

            public static readonly string[] Attributes = 
            { "src", "data-src", "data-lazy-src", "data-original", "data-file", "srcset", "data-srcset", "data-url", "data-image" };

            public static readonly string[] Extensions = 
            { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp" };
            
            public static readonly string[] BackgroundImagePatterns = 
            { "background-image:", "background:" };
        }

        /// <summary>
        /// Converter constants
        /// </summary>
        public static class Converters
        {
            public const string UnknownIndex = "?";
        }

        /// <summary>
        /// Settings file configuration
        /// </summary>
        public static class Settings
        {
            public const string FileName = "settings.json";
        }
    }
}
