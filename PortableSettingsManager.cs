using System;
using System.IO;
using System.Text.Json;
using WebsiteImagePilfer.Constants;
using WebsiteImagePilfer.Services;
using static WebsiteImagePilfer.Constants.AppConstants;

namespace WebsiteImagePilfer
{
    public class PortableSettingsManager
    {
        private static string SettingsFilePath => Path.Combine(AppContext.BaseDirectory, Settings.FileName);

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
            
            // New property for file type filtering
            public List<string> AllowedFileTypes { get; set; } = new List<string>();
            
            // Thorough scan options
            public bool ThoroughScan_UseSelenium { get; set; } = true;
            public bool ThoroughScan_CheckBackgroundImages { get; set; } = true;
            public bool ThoroughScan_CheckDataAttributes { get; set; } = true;
            public bool ThoroughScan_CheckScriptTags { get; set; } = true;
            public bool ThoroughScan_CheckShadowDOM { get; set; } = false;
            public bool ThoroughScan_SaveDebugFiles { get; set; } = false;

            public bool IsValid(out string? validationError)
            {
                if (MinimumImageSize < Validation.MinImageSize || MinimumImageSize > Validation.MaxImageSize)
                {
                    validationError = $"MinimumImageSize must be between {Validation.MinImageSize} and {Validation.MaxImageSize}";
                    return false;
                }

                if (ItemsPerPage < Validation.MinItemsPerPage || ItemsPerPage > Validation.MaxItemsPerPage)
                {
                    validationError = $"ItemsPerPage must be between {Validation.MinItemsPerPage} and {Validation.MaxItemsPerPage}";
                    return false;
                }

                if (MaxImagesToScan < Validation.MinMaxImagesToScan || MaxImagesToScan > Validation.MaxMaxImagesToScan)
                {
                    validationError = $"MaxImagesToScan must be between {Validation.MinMaxImagesToScan} and {Validation.MaxMaxImagesToScan}";
                    return false;
                }

                // Remove the validation that prevented both JPG and PNG from being enabled
                // (Now they can both be checked, meaning both file types are allowed)

                validationError = null;
                return true;
            }

            public void ApplySafeDefaults()
            {
                MinimumImageSize = Math.Clamp(MinimumImageSize, Validation.MinImageSize, Validation.MaxImageSize);
                ItemsPerPage = Math.Clamp(ItemsPerPage, Validation.MinItemsPerPage, Validation.MaxItemsPerPage);
                MaxImagesToScan = Math.Clamp(MaxImagesToScan, Validation.MinMaxImagesToScan, Validation.MaxMaxImagesToScan);

                // Remove the logic that disabled both flags
                // No longer needed with new collection-based approach
            }
        }

        public static async Task<AppSettings> LoadSettingsAsync()
        {
            try
            {
                if (!File.Exists(SettingsFilePath))
                {
                    Logger.Info($"Settings file not found at {SettingsFilePath}, returning defaults");
                    return new AppSettings();
                }

                string json = await File.ReadAllTextAsync(SettingsFilePath).ConfigureAwait(false);
                var settings = JsonSerializer.Deserialize<AppSettings>(json);

                if (settings == null)
                {
                    Logger.Warning("Failed to deserialize settings, returning defaults");
                    return new AppSettings();
                }

                // Validate and apply safe defaults if needed
                if (!settings.IsValid(out string? validationError))
                {
                    Logger.Warning($"Settings validation failed: {validationError}. Applying safe defaults.");
                    settings.ApplySafeDefaults();
                }

                Logger.Info($"Settings loaded successfully from {SettingsFilePath}");
                return settings;
            }
            catch (JsonException ex)
            {
                Logger.Error("JSON parsing error loading settings", ex);
                return new AppSettings();
            }
            catch (IOException ex)
            {
                Logger.Error("IO error loading settings", ex);
                return new AppSettings();
            }
            catch (UnauthorizedAccessException ex)
            {
                Logger.Error("Access denied loading settings", ex);
                return new AppSettings();
            }
            catch (Exception ex)
            {
                Logger.Error("Unexpected error loading settings", ex);
                return new AppSettings();
            }
        }

        public static async Task<bool> SaveSettingsAsync(AppSettings settings)
        {
            if (settings == null)
            {
                Logger.Warning("Cannot save null settings");
                return false;
            }

            // Validate settings before saving
            if (!settings.IsValid(out string? validationError))
            {
                Logger.Warning($"Cannot save invalid settings: {validationError}");
                settings.ApplySafeDefaults();
                Logger.Info("Safe defaults applied, continuing with save");
            }

            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(settings, options);
                await File.WriteAllTextAsync(SettingsFilePath, json).ConfigureAwait(false);
                Logger.Info($"Settings saved successfully to {SettingsFilePath}");
                return true;
            }
            catch (JsonException ex)
            {
                Logger.Error("JSON serialization error saving settings", ex);
                return false;
            }
            catch (IOException ex)
            {
                Logger.Error("IO error saving settings", ex);
                return false;
            }
            catch (UnauthorizedAccessException ex)
            {
                Logger.Error("Access denied saving settings", ex);
                return false;
            }
            catch (Exception ex)
            {
                Logger.Error("Unexpected error saving settings", ex);
                return false;
            }
        }

        // Keep synchronous versions for backward compatibility
        public static AppSettings LoadSettings()
        {
            return LoadSettingsAsync().GetAwaiter().GetResult();
        }

        public static bool SaveSettings(AppSettings settings)
        {
            return SaveSettingsAsync(settings).GetAwaiter().GetResult();
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
                    Logger.Info($"Settings file deleted: {SettingsFilePath}");
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                Logger.Error("Error deleting settings", ex);
                return false;
            }
        }
    }
}
