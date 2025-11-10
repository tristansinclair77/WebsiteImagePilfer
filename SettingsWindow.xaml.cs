using System.Windows;

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
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            // Save settings
            _settings.FilterBySize = FilterBySizeCheckBox.IsChecked == true;
            _settings.MinimumImageSize = (int)MinSizeSlider.Value;
            _settings.ShowThumbnails = ShowThumbnailsCheckBox.IsChecked == true;
            _settings.FilterJpgOnly = FilterJpgCheckBox.IsChecked == true;
            _settings.FilterPngOnly = FilterPngCheckBox.IsChecked == true;
            _settings.SkipFullResolutionCheck = SkipFullResCheckBox.IsChecked == true;
            _settings.LimitScanCount = LimitScanCheckBox.IsChecked == true;
            _settings.MaxImagesToScan = (int)MaxImagesSlider.Value;
            _settings.LoadPreviews = LoadPreviewsCheckBox.IsChecked == true;

            // Validate file type filters
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
