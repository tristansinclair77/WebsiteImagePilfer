using System.Windows;
using WebsiteImagePilfer.Models;

namespace WebsiteImagePilfer
{
    public partial class SettingsWindow : Window
    {
        private readonly DownloadSettings _settings;

        public SettingsWindow(DownloadSettings settings)
        {
            InitializeComponent();
            _settings = settings;

            // Load current settings into UI
            FilterBySizeCheckBox.IsChecked = _settings.FilterBySize;
            MinSizeSlider.Value = _settings.MinimumImageSize;
            ShowThumbnailsCheckBox.IsChecked = _settings.ShowThumbnails;
            
            // Load file type filters from AllowedFileTypes collection
            FilterJpgCheckBox.IsChecked = _settings.AllowedFileTypes.Contains(".jpg") || _settings.AllowedFileTypes.Contains(".jpeg");
            FilterPngCheckBox.IsChecked = _settings.AllowedFileTypes.Contains(".png");
            FilterGifCheckBox.IsChecked = _settings.AllowedFileTypes.Contains(".gif");
            FilterWebpCheckBox.IsChecked = _settings.AllowedFileTypes.Contains(".webp");
            
            SkipFullResCheckBox.IsChecked = _settings.SkipFullResolutionCheck;
            EnableBooruModeCheckBox.IsChecked = _settings.EnableBooruMode;
            LimitScanCheckBox.IsChecked = _settings.LimitScanCount;
            MaxImagesSlider.Value = _settings.MaxImagesToScan;
            LoadPreviewsCheckBox.IsChecked = _settings.LoadPreviews;
            ItemsPerPageSlider.Value = _settings.ItemsPerPage;
            
            // Load thorough scan options
            ThoroughScan_UseSeleniumCheckBox.IsChecked = _settings.ThoroughScan_UseSelenium;
            ThoroughScan_CheckBackgroundImagesCheckBox.IsChecked = _settings.ThoroughScan_CheckBackgroundImages;
            ThoroughScan_CheckDataAttributesCheckBox.IsChecked = _settings.ThoroughScan_CheckDataAttributes;
            ThoroughScan_CheckScriptTagsCheckBox.IsChecked = _settings.ThoroughScan_CheckScriptTags;
            ThoroughScan_CheckShadowDOMCheckBox.IsChecked = _settings.ThoroughScan_CheckShadowDOM;
            ThoroughScan_SaveDebugFilesCheckBox.IsChecked = _settings.ThoroughScan_SaveDebugFiles;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            // Save settings using GetValueOrDefault() for cleaner null handling
            _settings.FilterBySize = FilterBySizeCheckBox.IsChecked.GetValueOrDefault();
            _settings.MinimumImageSize = (int)MinSizeSlider.Value;
            _settings.ShowThumbnails = ShowThumbnailsCheckBox.IsChecked.GetValueOrDefault();
            
            // Build AllowedFileTypes collection based on checked boxes
            _settings.AllowedFileTypes.Clear();
            if (FilterJpgCheckBox.IsChecked.GetValueOrDefault())
            {
                _settings.AllowedFileTypes.Add(".jpg");
                _settings.AllowedFileTypes.Add(".jpeg");
            }
            if (FilterPngCheckBox.IsChecked.GetValueOrDefault())
            {
                _settings.AllowedFileTypes.Add(".png");
            }
            if (FilterGifCheckBox.IsChecked.GetValueOrDefault())
            {
                _settings.AllowedFileTypes.Add(".gif");
            }
            if (FilterWebpCheckBox.IsChecked.GetValueOrDefault())
            {
                _settings.AllowedFileTypes.Add(".webp");
            }
            
            _settings.SkipFullResolutionCheck = SkipFullResCheckBox.IsChecked.GetValueOrDefault();
            _settings.EnableBooruMode = EnableBooruModeCheckBox.IsChecked.GetValueOrDefault();
            _settings.LimitScanCount = LimitScanCheckBox.IsChecked.GetValueOrDefault();
            _settings.MaxImagesToScan = (int)MaxImagesSlider.Value;
            _settings.LoadPreviews = LoadPreviewsCheckBox.IsChecked.GetValueOrDefault();
            _settings.ItemsPerPage = (int)ItemsPerPageSlider.Value;
            
            // Save thorough scan options
            _settings.ThoroughScan_UseSelenium = ThoroughScan_UseSeleniumCheckBox.IsChecked.GetValueOrDefault();
            _settings.ThoroughScan_CheckBackgroundImages = ThoroughScan_CheckBackgroundImagesCheckBox.IsChecked.GetValueOrDefault();
            _settings.ThoroughScan_CheckDataAttributes = ThoroughScan_CheckDataAttributesCheckBox.IsChecked.GetValueOrDefault();
            _settings.ThoroughScan_CheckScriptTags = ThoroughScan_CheckScriptTagsCheckBox.IsChecked.GetValueOrDefault();
            _settings.ThoroughScan_CheckShadowDOM = ThoroughScan_CheckShadowDOMCheckBox.IsChecked.GetValueOrDefault();
            _settings.ThoroughScan_SaveDebugFiles = ThoroughScan_SaveDebugFilesCheckBox.IsChecked.GetValueOrDefault();

            // No validation needed - all combinations are now valid
            // If no file types are checked, all file types will be allowed

            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
