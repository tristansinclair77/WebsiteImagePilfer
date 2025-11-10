using System;
using System.IO;
using System.Text.Json;

namespace WebsiteImagePilfer
{
    public class PortableSettingsManager
    {
        private const string SETTINGS_FILE_NAME = "settings.json";
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
        }

        public static AppSettings LoadSettings()
        {
            try
            {
                if (File.Exists(SettingsFilePath))
                {
                    string json = File.ReadAllText(SettingsFilePath);
                    return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading settings: {ex.Message}");
            }

            return new AppSettings();
        }

        public static void SaveSettings(AppSettings settings)
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(settings, options);
                File.WriteAllText(SettingsFilePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving settings: {ex.Message}");
            }
        }
    }
}
