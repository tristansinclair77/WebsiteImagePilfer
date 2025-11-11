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
