namespace WebsiteImagePilfer.Models
{
    public class DownloadSettings
    {
        public bool FilterBySize { get; set; }
        public int MinimumImageSize { get; set; } = 5000;
        public bool ShowThumbnails { get; set; } = true;
        public bool FilterJpgOnly { get; set; }
        public bool FilterPngOnly { get; set; }
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
            FilterJpgOnly = appSettings.FilterJpgOnly;
            FilterPngOnly = appSettings.FilterPngOnly;
            SkipFullResolutionCheck = appSettings.SkipFullResolutionCheck;
            LimitScanCount = appSettings.LimitScanCount;
            MaxImagesToScan = appSettings.MaxImagesToScan;
            ItemsPerPage = appSettings.ItemsPerPage;
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
                FilterJpgOnly = FilterJpgOnly,
                FilterPngOnly = FilterPngOnly,
                SkipFullResolutionCheck = SkipFullResolutionCheck,
                LimitScanCount = LimitScanCount,
                MaxImagesToScan = MaxImagesToScan,
                ItemsPerPage = ItemsPerPage
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
