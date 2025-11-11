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
            FilterJpgCheckBox.IsChecked = _settings.FilterJpgOnly;
            FilterPngCheckBox.IsChecked = _settings.FilterPngOnly;
            SkipFullResCheckBox.IsChecked = _settings.SkipFullResolutionCheck;
            LimitScanCheckBox.IsChecked = _settings.LimitScanCount;
            MaxImagesSlider.Value = _settings.MaxImagesToScan;
            LoadPreviewsCheckBox.IsChecked = _settings.LoadPreviews;
            ItemsPerPageSlider.Value = _settings.ItemsPerPage;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            // Save settings using GetValueOrDefault() for cleaner null handling
            _settings.FilterBySize = FilterBySizeCheckBox.IsChecked.GetValueOrDefault();
            _settings.MinimumImageSize = (int)MinSizeSlider.Value;
            _settings.ShowThumbnails = ShowThumbnailsCheckBox.IsChecked.GetValueOrDefault();
            _settings.FilterJpgOnly = FilterJpgCheckBox.IsChecked.GetValueOrDefault();
            _settings.FilterPngOnly = FilterPngCheckBox.IsChecked.GetValueOrDefault();
            _settings.SkipFullResolutionCheck = SkipFullResCheckBox.IsChecked.GetValueOrDefault();
            _settings.LimitScanCount = LimitScanCheckBox.IsChecked.GetValueOrDefault();
            _settings.MaxImagesToScan = (int)MaxImagesSlider.Value;
            _settings.LoadPreviews = LoadPreviewsCheckBox.IsChecked.GetValueOrDefault();
            _settings.ItemsPerPage = (int)ItemsPerPageSlider.Value;

            // Validate file type filters - simplified validation
            if (_settings.FilterJpgOnly && _settings.FilterPngOnly)
            {
                MessageBox.Show("You cannot select both JPG and PNG only filters. Please choose one or neither.", 
  "Invalid Settings", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

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
