using System;
using System.IO;
using System.Text.Json;

namespace WebsiteImagePilfer
{
    public class PortableSettingsManager
    {
        private const string SETTINGS_FILE_NAME = "settings.json";
        private const int MIN_IMAGE_SIZE = 100;
        private const int MAX_IMAGE_SIZE = 1_000_000_000;
        private const int MIN_ITEMS_PER_PAGE = 1;
        private const int MAX_ITEMS_PER_PAGE = 1000;
        private const int MIN_MAX_IMAGES_TO_SCAN = 1;
        private const int MAX_MAX_IMAGES_TO_SCAN = 10000;

        private static string SettingsFilePath => Path.Combine(AppContext.BaseDirectory, SETTINGS_FILE_NAME);

        public class AppSettings
        {
            public string DownloadFolder { get; set; } = "";
            public string LastUrl { get; set; } = "";
            public bool FilterBySize { get; set; } = false;
            public int MinimumImageSize { get; set; } = 5000;
            public bool ShowThumbnails { get; set; } = true;
            public bool LoadPreviews { get; set; } = true;
            public bool FilterJpgOnly { get; set; } = false;
            public bool FilterPngOnly { get; set; } = false;
            public bool SkipFullResolutionCheck { get; set; } = false;
            public bool LimitScanCount { get; set; } = false;
            public int MaxImagesToScan { get; set; } = 20;
            public int ItemsPerPage { get; set; } = 50;

            public bool IsValid(out string? validationError)
            {
                if (MinimumImageSize < MIN_IMAGE_SIZE || MinimumImageSize > MAX_IMAGE_SIZE)
                {
                    validationError = $"MinimumImageSize must be between {MIN_IMAGE_SIZE} and {MAX_IMAGE_SIZE}";
                    return false;
                }

                if (ItemsPerPage < MIN_ITEMS_PER_PAGE || ItemsPerPage > MAX_ITEMS_PER_PAGE)
                {
                    validationError = $"ItemsPerPage must be between {MIN_ITEMS_PER_PAGE} and {MAX_ITEMS_PER_PAGE}";
                    return false;
                }

                if (MaxImagesToScan < MIN_MAX_IMAGES_TO_SCAN || MaxImagesToScan > MAX_MAX_IMAGES_TO_SCAN)
                {
                    validationError = $"MaxImagesToScan must be between {MIN_MAX_IMAGES_TO_SCAN} and {MAX_MAX_IMAGES_TO_SCAN}";
                    return false;
                }

                if (FilterJpgOnly && FilterPngOnly)
                {
                    validationError = "Cannot enable both FilterJpgOnly and FilterPngOnly";
                    return false;
                }

                validationError = null;
                return true;
            }

            public void ApplySafeDefaults()
            {
                MinimumImageSize = Math.Clamp(MinimumImageSize, MIN_IMAGE_SIZE, MAX_IMAGE_SIZE);
                ItemsPerPage = Math.Clamp(ItemsPerPage, MIN_ITEMS_PER_PAGE, MAX_ITEMS_PER_PAGE);
                MaxImagesToScan = Math.Clamp(MaxImagesToScan, MIN_MAX_IMAGES_TO_SCAN, MAX_MAX_IMAGES_TO_SCAN);

                if (FilterJpgOnly && FilterPngOnly)
                {
                    FilterJpgOnly = false;
                    FilterPngOnly = false;
                }
            }
        }

        public static AppSettings LoadSettings()
        {
            try
            {
                if (!File.Exists(SettingsFilePath))
                {
                    System.Diagnostics.Debug.WriteLine($"Settings file not found at {SettingsFilePath}, returning defaults");
                    return new AppSettings();
                }

                string json = File.ReadAllText(SettingsFilePath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json);

                if (settings == null)
                {
                    System.Diagnostics.Debug.WriteLine("Failed to deserialize settings, returning defaults");
                    return new AppSettings();
                }

                // Validate and apply safe defaults if needed
                if (!settings.IsValid(out string? validationError))
                {
                    System.Diagnostics.Debug.WriteLine($"Settings validation failed: {validationError}. Applying safe defaults.");
                    settings.ApplySafeDefaults();
                }

                System.Diagnostics.Debug.WriteLine($"Settings loaded successfully from {SettingsFilePath}");
                return settings;
            }
            catch (JsonException ex)
            {
                System.Diagnostics.Debug.WriteLine($"JSON parsing error loading settings: {ex.Message}");
                return new AppSettings();
            }
            catch (IOException ex)
            {
                System.Diagnostics.Debug.WriteLine($"IO error loading settings: {ex.Message}");
                return new AppSettings();
            }
            catch (UnauthorizedAccessException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Access denied loading settings: {ex.Message}");
                return new AppSettings();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Unexpected error loading settings: {ex.GetType().Name} - {ex.Message}");
                return new AppSettings();
            }
        }

        public static bool SaveSettings(AppSettings settings)
        {
            if (settings == null)
            {
                System.Diagnostics.Debug.WriteLine("Cannot save null settings");
                return false;
            }

            // Validate settings before saving
            if (!settings.IsValid(out string? validationError))
            {
                System.Diagnostics.Debug.WriteLine($"Cannot save invalid settings: {validationError}");
                settings.ApplySafeDefaults();
                System.Diagnostics.Debug.WriteLine("Safe defaults applied, continuing with save");
            }

            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(settings, options);
                File.WriteAllText(SettingsFilePath, json);
                System.Diagnostics.Debug.WriteLine($"Settings saved successfully to {SettingsFilePath}");
                return true;
            }
            catch (JsonException ex)
            {
                System.Diagnostics.Debug.WriteLine($"JSON serialization error saving settings: {ex.Message}");
                return false;
            }
            catch (IOException ex)
            {
                System.Diagnostics.Debug.WriteLine($"IO error saving settings: {ex.Message}");
                return false;
            }
            catch (UnauthorizedAccessException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Access denied saving settings: {ex.Message}");
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Unexpected error saving settings: {ex.GetType().Name} - {ex.Message}");
                return false;
            }
        }

        public static string GetSettingsFilePath() => SettingsFilePath;

        public static bool SettingsFileExists() => File.Exists(SettingsFilePath);

        public static bool DeleteSettings()
        {
            try
            {
                if (File.Exists(SettingsFilePath))
                {
                    File.Delete(SettingsFilePath);
                    System.Diagnostics.Debug.WriteLine($"Settings file deleted: {SettingsFilePath}");
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error deleting settings: {ex.Message}");
                return false;
            }
        }
    }
}
