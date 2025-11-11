namespace WebsiteImagePilfer.Models
{
    public class DownloadSettings
    {
        public bool FilterBySize { get; set; }
        public int MinimumImageSize { get; set; } = 5000;
        public bool ShowThumbnails { get; set; } = true;
        
        // Legacy properties for backward compatibility with saved settings
        [Obsolete("Use AllowedFileTypes instead")]
        public bool FilterJpgOnly { get; set; }
        [Obsolete("Use AllowedFileTypes instead")]
        public bool FilterPngOnly { get; set; }
        
        // New property: collection of allowed file extensions (e.g., ".jpg", ".png")
        // If empty, all file types are allowed
        public HashSet<string> AllowedFileTypes { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        
        public bool SkipFullResolutionCheck { get; set; }
        public bool LimitScanCount { get; set; }
        public int MaxImagesToScan { get; set; } = 20;
        public bool LoadPreviews { get; set; } = true;
        public int ItemsPerPage { get; set; } = 50;
        
        // Thorough scan options - each adds a layer of detection
        public bool ThoroughScan_UseSelenium { get; set; } = true;
        public bool ThoroughScan_CheckBackgroundImages { get; set; } = true;
        public bool ThoroughScan_CheckDataAttributes { get; set; } = true;
        public bool ThoroughScan_CheckScriptTags { get; set; } = true;
        public bool ThoroughScan_CheckShadowDOM { get; set; } = false;
        public bool ThoroughScan_SaveDebugFiles { get; set; } = false;

        // Load settings from portable JSON asynchronously
        public async Task LoadFromPortableSettingsAsync()
        {
            var appSettings = await PortableSettingsManager.LoadSettingsAsync().ConfigureAwait(false);
            FilterBySize = appSettings.FilterBySize;
            MinimumImageSize = appSettings.MinimumImageSize;
            ShowThumbnails = appSettings.ShowThumbnails;
            LoadPreviews = appSettings.LoadPreviews;
            SkipFullResolutionCheck = appSettings.SkipFullResolutionCheck;
            LimitScanCount = appSettings.LimitScanCount;
            MaxImagesToScan = appSettings.MaxImagesToScan;
            ItemsPerPage = appSettings.ItemsPerPage;
            
            // Load thorough scan options
            ThoroughScan_UseSelenium = appSettings.ThoroughScan_UseSelenium;
            ThoroughScan_CheckBackgroundImages = appSettings.ThoroughScan_CheckBackgroundImages;
            ThoroughScan_CheckDataAttributes = appSettings.ThoroughScan_CheckDataAttributes;
            ThoroughScan_CheckScriptTags = appSettings.ThoroughScan_CheckScriptTags;
            ThoroughScan_CheckShadowDOM = appSettings.ThoroughScan_CheckShadowDOM;
            ThoroughScan_SaveDebugFiles = appSettings.ThoroughScan_SaveDebugFiles;
            
            // Load new AllowedFileTypes or migrate from legacy settings
            if (appSettings.AllowedFileTypes != null && appSettings.AllowedFileTypes.Count > 0)
            {
                AllowedFileTypes = new HashSet<string>(appSettings.AllowedFileTypes, StringComparer.OrdinalIgnoreCase);
            }
            else
            {
                // Migrate from legacy boolean flags
                AllowedFileTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (appSettings.FilterJpgOnly)
                {
                    AllowedFileTypes.Add(".jpg");
                    AllowedFileTypes.Add(".jpeg");
                }
                if (appSettings.FilterPngOnly)
                {
                    AllowedFileTypes.Add(".png");
                }
            }
        }

        // Save settings to portable JSON asynchronously
        public async Task SaveToPortableSettingsAsync(string downloadFolder, string lastUrl)
        {
            var appSettings = new PortableSettingsManager.AppSettings
            {
                DownloadFolder = downloadFolder,
                LastUrl = lastUrl,
                FilterBySize = FilterBySize,
                MinimumImageSize = MinimumImageSize,
                ShowThumbnails = ShowThumbnails,
                LoadPreviews = LoadPreviews,
                SkipFullResolutionCheck = SkipFullResolutionCheck,
                LimitScanCount = LimitScanCount,
                MaxImagesToScan = MaxImagesToScan,
                ItemsPerPage = ItemsPerPage,
                AllowedFileTypes = AllowedFileTypes.ToList(),
                // Thorough scan options
                ThoroughScan_UseSelenium = ThoroughScan_UseSelenium,
                ThoroughScan_CheckBackgroundImages = ThoroughScan_CheckBackgroundImages,
                ThoroughScan_CheckDataAttributes = ThoroughScan_CheckDataAttributes,
                ThoroughScan_CheckScriptTags = ThoroughScan_CheckScriptTags,
                ThoroughScan_CheckShadowDOM = ThoroughScan_CheckShadowDOM,
                ThoroughScan_SaveDebugFiles = ThoroughScan_SaveDebugFiles,
                // Keep legacy properties for backward compatibility
                FilterJpgOnly = AllowedFileTypes.Contains(".jpg") || AllowedFileTypes.Contains(".jpeg"),
                FilterPngOnly = AllowedFileTypes.Contains(".png")
            };
            await PortableSettingsManager.SaveSettingsAsync(appSettings).ConfigureAwait(false);
        }

        // Keep synchronous versions for backward compatibility
        public void LoadFromPortableSettings()
        {
            LoadFromPortableSettingsAsync().GetAwaiter().GetResult();
        }

        public void SaveToPortableSettings(string downloadFolder, string lastUrl)
        {
            SaveToPortableSettingsAsync(downloadFolder, lastUrl).GetAwaiter().GetResult();
        }
    }
}
