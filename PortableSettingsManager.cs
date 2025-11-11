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
                MinimumImageSize = Math.Clamp(MinimumImageSize, Validation.MinImageSize, Validation.MaxImageSize);
                ItemsPerPage = Math.Clamp(ItemsPerPage, Validation.MinItemsPerPage, Validation.MaxItemsPerPage);
                MaxImagesToScan = Math.Clamp(MaxImagesToScan, Validation.MinMaxImagesToScan, Validation.MaxMaxImagesToScan);

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
                    Logger.Info($"Settings file not found at {SettingsFilePath}, returning defaults");
                    return new AppSettings();
                }

                string json = File.ReadAllText(SettingsFilePath);
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

        public static bool SaveSettings(AppSettings settings)
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
                File.WriteAllText(SettingsFilePath, json);
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
