using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Net.Http;
using HtmlAgilityPack;
using System.IO;
using System.Collections.ObjectModel;
using Microsoft.Win32;
using IOPath = System.IO.Path;
using System.Text.RegularExpressions;
using System.ComponentModel;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using System.Web;
using System.Globalization;

namespace WebsiteImagePilfer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly HttpClient _httpClient;
        private ObservableCollection<ImageDownloadItem> _imageItems;
        private ObservableCollection<ImageDownloadItem> _currentPageItems; // Items for current page
        private string _downloadFolder;
        private CancellationTokenSource? _cancellationTokenSource;
        private DownloadSettings _settings;
        private List<string> _scannedImageUrls; // Store scanned URLs
        public int _currentPage = 1; // Public for converter access
        public int _itemsPerPage = 50; // Public for converter access
        private int _totalPages = 1;
        private double _lastPreviewColumnWidth = 0; // Track last column width for reload
        private System.Timers.Timer? _columnResizeTimer; // Debounce timer for column resize
        private SemaphoreSlim _previewSemaphore; // Throttle concurrent preview loads

        public MainWindow()
        {
            InitializeComponent();
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
            _imageItems = new ObservableCollection<ImageDownloadItem>();
            _currentPageItems = new ObservableCollection<ImageDownloadItem>();
            ImageList.ItemsSource = _currentPageItems; // Bind to current page items
            _scannedImageUrls = new List<string>();
            _previewSemaphore = new SemaphoreSlim(5, 5); // Max 5 concurrent preview loads

            // Load portable settings
            var appSettings = PortableSettingsManager.LoadSettings();
            _settings = new DownloadSettings();
            _settings.LoadFromPortableSettings();

            // Apply items per page from settings
            _itemsPerPage = _settings.ItemsPerPage;

            // Set default download folder from settings or use default
            _downloadFolder = string.IsNullOrEmpty(appSettings.DownloadFolder)
                ? IOPath.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "WebsiteImages")
                : appSettings.DownloadFolder;
            FolderTextBox.Text = _downloadFolder;

            // Load last URL if available
            if (!string.IsNullOrEmpty(appSettings.LastUrl))
            {
                UrlTextBox.Text = appSettings.LastUrl;
            }

            // Set up column width monitoring
            PreviewColumn.Width = 150; // Ensure it starts with a known width
            _lastPreviewColumnWidth = 150;

            // Setup debounce timer for column resize
            _columnResizeTimer = new System.Timers.Timer(500); // 500ms debounce
            _columnResizeTimer.AutoReset = false;
            _columnResizeTimer.Elapsed += ColumnResizeTimer_Elapsed;

            // Monitor column width changes by polling
            var columnWidthMonitor = new System.Windows.Threading.DispatcherTimer();
            columnWidthMonitor.Interval = TimeSpan.FromMilliseconds(100); // Check every 100ms
            columnWidthMonitor.Tick += (s, e) => CheckColumnWidthChanged();
            columnWidthMonitor.Start();
        }

        private void CheckColumnWidthChanged()
        {
            // Check if preview column width has changed significantly
            if (PreviewColumn.ActualWidth > 0)
            {
                double widthDifference = Math.Abs(PreviewColumn.ActualWidth - _lastPreviewColumnWidth);

                // If width changed by more than 10 pixels, reload previews
                if (widthDifference > 10)
                {
                    // Restart the debounce timer
                    _columnResizeTimer?.Stop();
                    _columnResizeTimer?.Start();
                }
            }
        }

        private async void ColumnResizeTimer_Elapsed(object? sender, System.Timers.ElapsedEventArgs e)
        {
            await Dispatcher.InvokeAsync(async () =>
            {
                if (PreviewColumn.ActualWidth > 0 && _settings.LoadPreviews)
                {
                    _lastPreviewColumnWidth = PreviewColumn.ActualWidth;
                    StatusText.Text = "Reloading previews at new resolution...";

                    // Reload all previews in the current page with new column width
                    await ReloadAllPreviewsAsync();

                    StatusText.Text = "Ready"; // Clear status when done
                }
            });
        }

        private async Task ReloadAllPreviewsAsync()
        {
            // Get all items that have preview images loaded
            var itemsWithPreviews = _imageItems.Where(i => !string.IsNullOrEmpty(i.Url) && _settings.LoadPreviews).ToList();

            foreach (var item in itemsWithPreviews)
            {
                try
                {
                    // Reload the preview at the new column width
                    var newPreview = await LoadPreviewImageAsync(item.Url);
                    if (newPreview != null)
                    {
                        item.PreviewImage = newPreview;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to reload preview for {item.Url}: {ex.Message}");
                }
            }
        }

        private async void ScanOnlyButton_Click(object sender, RoutedEventArgs e)
        {
            string url = UrlTextBox.Text.Trim();

            // Validate URL
            if (string.IsNullOrEmpty(url))
            {
                MessageBox.Show("Please enter a valid URL.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!Uri.IsWellFormedUriString(url, UriKind.Absolute))
            {
                MessageBox.Show("Please enter a valid URL (including http:// or https://).", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Save last URL to portable settings
            _settings.SaveToPortableSettings(_downloadFolder, url);

            // Setup cancellation token
            _cancellationTokenSource = new CancellationTokenSource();

            // Update UI state
            ScanOnlyButton.IsEnabled = false;
            DownloadButton.IsEnabled = false;
            FastScanCheckBox.IsEnabled = false;
            CancelButton.IsEnabled = true;
            _imageItems.Clear();
            _scannedImageUrls.Clear();
            DownloadProgress.Value = 0;

            try
            {
                StatusText.Text = "Scanning webpage...";

                // Get image URLs - use fast or thorough scan based on checkbox
                bool useFastScan = FastScanCheckBox.IsChecked == true;
                _scannedImageUrls = await GetImageUrlsWithSeleniumAsync(url, _cancellationTokenSource.Token, useFastScan);

                if (_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    StatusText.Text = "Scan cancelled.";
                    return;
                }

                if (_scannedImageUrls.Count == 0)
                {
                    MessageBox.Show("No images found on the webpage.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                    StatusText.Text = "No images found.";
                    return;
                }

                // Display found images in list (without downloading)
                foreach (var imageUrl in _scannedImageUrls)
                {
                    var uri = new Uri(imageUrl);
                    var fileName = "";

                    // KEMONO.CR: Check for ?f= query parameter (contains actual filename)
                    if (uri.Query.Contains("?f=") || uri.Query.Contains("&f="))
                    {
                        var queryParams = System.Web.HttpUtility.ParseQueryString(uri.Query);
                        var fParam = queryParams["f"];
                        if (!string.IsNullOrEmpty(fParam))
                        {
                            fileName = fParam;
                        }
                    }

                    // Fallback to path filename if no query parameter
                    if (string.IsNullOrEmpty(fileName))
                    {
                        fileName = IOPath.GetFileName(uri.LocalPath);
                    }

                    // Final fallback if still empty
                    if (string.IsNullOrEmpty(fileName) || !IOPath.HasExtension(fileName))
                    {
                        fileName = $"image_{_imageItems.Count + 1}.jpg";
                    }

                    fileName = SanitizeFileName(fileName);

                    // Check for duplicates - if limit is enabled and this is a duplicate, skip it
                    var filePath = IOPath.Combine(_downloadFolder, fileName);
                    if (_settings.LimitScanCount && File.Exists(filePath))
                    {
                        // Skip duplicate, continue to next image
                        continue;
                    }

                    var item = new ImageDownloadItem
                    {
                        Url = imageUrl,
                        Status = "Ready",
                        FileName = fileName
                    };
                    _imageItems.Add(item);

                    // Preview loading will happen lazily when the page is displayed
                    // (removed immediate preview loading to prevent UI freeze with large image counts)

                    // Check if we've reached the scan limit
                    if (_settings.LimitScanCount && _imageItems.Count >= _settings.MaxImagesToScan)
                    {
                        break; // Stop adding more images
                    }
                }

                string scanType = useFastScan ? "Fast" : "Thorough";
                string limitInfo = _settings.LimitScanCount ? $" (limited to {_settings.MaxImagesToScan})" : "";

                // Reset to page 1 and update pagination
                _currentPage = 1;
                UpdatePagination();

                StatusText.Text = $"Found {_imageItems.Count} images{limitInfo} ({scanType} scan). Click 'Download' to save them.";
                DownloadButton.IsEnabled = true;

                string scanMessage = _settings.LimitScanCount
                    ? $"Found {_imageItems.Count} images (limited to {_settings.MaxImagesToScan}) using {scanType} scan!\n\nDuplicates were automatically skipped.\n\nReview the list and click 'Download' when ready."
                    : $"Found {_imageItems.Count} images using {scanType} scan!\n\nReview the list and click 'Download' when ready.";

                MessageBox.Show(scanMessage, "Scan Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (OperationCanceledException)
            {
                StatusText.Text = "Scan cancelled by user.";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                StatusText.Text = "Error occurred during scan.";
            }
            finally
            {
                ScanOnlyButton.IsEnabled = true;
                FastScanCheckBox.IsEnabled = true;
                CancelButton.IsEnabled = false;
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
            }
        }

        private async Task LoadAndSetPreviewAsync(ImageDownloadItem item)
        {
            await _previewSemaphore.WaitAsync();
            try
            {
                var preview = await LoadPreviewImageAsync(item.Url).ConfigureAwait(false);
                if (preview != null)
                {
                    // Update UI on dispatcher thread
                    await Dispatcher.InvokeAsync(() => item.PreviewImage = preview);
                }
            }
            catch (Exception ex)
            {
                // Log error but don't fail
                System.Diagnostics.Debug.WriteLine($"Failed to load preview for {item.Url}: {ex.Message}");
            }
            finally
            {
                _previewSemaphore.Release();
            }
        }

        private async void DownloadButton_Click(object sender, RoutedEventArgs e)
        {
            if (_imageItems.Count == 0)
            {
                MessageBox.Show("No images to download. Please scan a webpage first.", "No Images", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Setup cancellation token
            _cancellationTokenSource = new CancellationTokenSource();

            // Update UI state
            ScanOnlyButton.IsEnabled = false;
            DownloadButton.IsEnabled = false;
            CancelButton.IsEnabled = true;
            DownloadProgress.Value = 0;

            try
            {
                StatusText.Text = "Starting download...";

                // Download images using existing items
                int downloadedCount = await DownloadImagesFromListAsync(_cancellationTokenSource.Token);

                if (_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    StatusText.Text = $"Cancelled. Downloaded {downloadedCount} images.";
                    MessageBox.Show($"Operation cancelled. Downloaded {downloadedCount} images.",
                        "Cancelled", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    StatusText.Text = $"Download complete! {downloadedCount} images saved to {_downloadFolder}";
                    MessageBox.Show($"Successfully downloaded {downloadedCount} images!", "Success",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (OperationCanceledException)
            {
                StatusText.Text = "Download cancelled by user.";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                StatusText.Text = "Error occurred during download.";
            }
            finally
            {
                ScanOnlyButton.IsEnabled = true;
                DownloadButton.IsEnabled = true;
                CancelButton.IsEnabled = false;
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            _cancellationTokenSource?.Cancel();
            CancelButton.IsEnabled = false;
            StatusText.Text = "Cancelling...";
        }

        private void ImageList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (ImageList.SelectedItem is ImageDownloadItem item)
            {
                // If image is already downloaded, open it
                if (item.Status == "✓ Done" || item.Status == "✓ Backup")
                {
                    var filePath = IOPath.Combine(_downloadFolder, item.FileName);
                    if (File.Exists(filePath))
                    {
                        try
                        {
                            // Open the file with default application
                            var processStartInfo = new System.Diagnostics.ProcessStartInfo
                            {
                                FileName = filePath,
                                UseShellExecute = true
                            };
                            System.Diagnostics.Process.Start(processStartInfo);
                            StatusText.Text = $"Opened: {item.FileName}";
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Failed to open file: {ex.Message}", "Error Opening File",
                                MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                    else
                    {
                        MessageBox.Show($"File not found: {item.FileName}\n\nThe file may have been moved or deleted.",
                            "File Not Found", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
                else if (item.Status == "⊘ Duplicate")
                {
                    // Duplicate - file already exists, open it
                    var filePath = IOPath.Combine(_downloadFolder, item.FileName);
                    if (File.Exists(filePath))
                    {
                        try
                        {
                            var processStartInfo = new System.Diagnostics.ProcessStartInfo
                            {
                                FileName = filePath,
                                UseShellExecute = true
                            };
                            System.Diagnostics.Process.Start(processStartInfo);
                            StatusText.Text = $"Opened: {item.FileName}";
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Failed to open file: {ex.Message}", "Error Opening File",
                                MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                    else
                    {
                        MessageBox.Show($"File not found: {item.FileName}\n\nThe file may have been moved or deleted.",
                            "File Not Found", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
                else
                {
                    // Not downloaded yet - try to download
                    _ = DownloadSingleImageAsync(item);
                }
            }
        }

        private void ImageList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Enable Download Selected button only if there are selected items with "Ready" status
            var selectedReadyItems = ImageList.SelectedItems.Cast<ImageDownloadItem>()
                .Where(item => item.Status == "Ready")
                .ToList();

            DownloadSelectedButton.IsEnabled = selectedReadyItems.Count > 0;
        }

        private async void DownloadSelectedButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedItems = ImageList.SelectedItems.Cast<ImageDownloadItem>().ToList();

            if (selectedItems.Count == 0)
            {
                MessageBox.Show("No images selected. Please select images to download.", "No Selection",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var readyItems = selectedItems.Where(item => item.Status == "Ready").ToList();

            if (readyItems.Count == 0)
            {
                MessageBox.Show("No ready images selected. Selected images may already be downloaded.", "No Ready Images",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Setup cancellation token
            _cancellationTokenSource = new CancellationTokenSource();

            // Update UI state
            ScanOnlyButton.IsEnabled = false;
            DownloadButton.IsEnabled = false;
            DownloadSelectedButton.IsEnabled = false;
            CancelButton.IsEnabled = true;
            DownloadProgress.Value = 0;

            try
            {
                StatusText.Text = $"Starting download of {readyItems.Count} selected images...";

                // Download selected images
                int downloadedCount = await DownloadSelectedImagesAsync(readyItems, _cancellationTokenSource.Token);

                if (_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    StatusText.Text = $"Cancelled. Downloaded {downloadedCount} of {readyItems.Count} selected images.";
                    MessageBox.Show($"Operation cancelled. Downloaded {downloadedCount} of {readyItems.Count} selected images.",
                        "Cancelled", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    StatusText.Text = $"Download complete! {downloadedCount} of {readyItems.Count} selected images saved to {_downloadFolder}";
                    MessageBox.Show($"Successfully downloaded {downloadedCount} selected images!", "Success",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (OperationCanceledException)
            {
                StatusText.Text = "Download cancelled by user.";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                StatusText.Text = "Error occurred during download.";
            }
            finally
            {
                ScanOnlyButton.IsEnabled = true;
                DownloadButton.IsEnabled = true;
                CancelButton.IsEnabled = false;
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;

                // Re-evaluate Download Selected button state
                ImageList_SelectionChanged(ImageList, new SelectionChangedEventArgs(Selector.SelectionChangedEvent, new List<object>(), new List<object>()));
            }
        }

        private async Task<int> DownloadSelectedImagesAsync(List<ImageDownloadItem> selectedItems, CancellationToken cancellationToken)
        {
            // Create download folder if it doesn't exist
            if (!Directory.Exists(_downloadFolder))
            {
                Directory.CreateDirectory(_downloadFolder);
            }

            int downloadedCount = 0;
            int skippedCount = 0;
            int duplicateCount = 0;
            int totalToDownload = selectedItems.Count;

            foreach (var item in selectedItems)
            {
                cancellationToken.ThrowIfCancellationRequested();

                await DownloadSingleItemAsync(item, cancellationToken);

                if (item.Status == "✓ Done") downloadedCount++;
                else if (item.Status.Contains("Duplicate")) { skippedCount++; duplicateCount++; }
                else if (item.Status.Contains("Skipped")) skippedCount++;

                // Update progress
                int processedCount = downloadedCount + skippedCount;
                DownloadProgress.Value = (processedCount * 100.0) / totalToDownload;
                StatusText.Text = $"Downloaded: {downloadedCount} | Skipped: {skippedCount} (Duplicates: {duplicateCount}) | Remaining: {totalToDownload - processedCount}";

                // Small delay to allow UI to update
                await Task.Delay(10, cancellationToken);
            }

            // Refresh current page to show updated statuses
            LoadCurrentPage();

            return downloadedCount;
        }

        private async Task DownloadSingleImageAsync(ImageDownloadItem item)
        {
            if (item.Status == "✓ Done" || item.Status == "⊘ Duplicate")
            {
                MessageBox.Show($"Image already downloaded: {item.FileName}", "Already Downloaded",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var originalStatus = item.Status;
            item.Status = "Downloading...";

            try
            {
                await DownloadSingleItemAsync(item, CancellationToken.None);

                if (item.Status == "✓ Done")
                {
                    StatusText.Text = $"Downloaded: {item.FileName}";
                }
            }
            catch (Exception ex)
            {
                item.Status = "✗ Failed";
                item.ErrorMessage = ex.Message;
                MessageBox.Show($"Failed to download: {ex.Message}", "Download Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void ContextMenu_Download_Click(object sender, RoutedEventArgs e)
        {
            // Get the clicked item from the ListView's selected item
            if (ImageList.SelectedItem is ImageDownloadItem item)
            {
                await DownloadSingleImageWithForceAsync(item);
            }
        }

        private async Task DownloadSingleImageWithForceAsync(ImageDownloadItem item)
        {
            // This method bypasses duplicate protection and downloads the file again
            // with a numbered suffix if a file name conflict exists

            var originalStatus = item.Status;
            item.Status = "Downloading...";

            try
            {
                // Download logic similar to DownloadSingleItemAsync but bypasses duplicate check
                string urlToDownload = item.Url;
                bool usedBackup = false;

                // Try to find full-resolution version if setting allows
                if (!_settings.SkipFullResolutionCheck)
                {
                    try
                    {
                        item.Status = "Finding full-res...";
                        var fullResUrl = await TryFindFullResolutionUrlAsync(item.Url, CancellationToken.None);

                        if (fullResUrl != null && fullResUrl != item.Url)
                        {
                            urlToDownload = fullResUrl;
                            usedBackup = false;
                        }
                        else if (fullResUrl == item.Url)
                        {
                            usedBackup = false;
                        }
                        else
                        {
                            usedBackup = true;
                        }
                    }
                    catch
                    {
                        usedBackup = true;
                    }
                }

                var fileName = item.FileName;
                if (string.IsNullOrEmpty(fileName) || fileName.StartsWith("image_"))
                {
                    var uri = new Uri(urlToDownload);

                    if (uri.Query.Contains("?f=") || uri.Query.Contains("&f="))
                    {
                        var queryParams = System.Web.HttpUtility.ParseQueryString(uri.Query);
                        var fParam = queryParams["f"];
                        if (!string.IsNullOrEmpty(fParam))
                        {
                            fileName = fParam;
                        }
                    }

                    if (string.IsNullOrEmpty(fileName))
                    {
                        fileName = IOPath.GetFileName(uri.LocalPath);
                    }

                    if (string.IsNullOrEmpty(fileName) || !IOPath.HasExtension(fileName))
                    {
                        fileName = $"image_{DateTime.Now:yyyyMMddHHmmss}_{Guid.NewGuid().ToString().Substring(0, 8)}.jpg";
                    }

                    fileName = SanitizeFileName(fileName);
                }

                // Check for file name conflicts and add a number if needed
                var filePath = IOPath.Combine(_downloadFolder, fileName);
                if (File.Exists(filePath))
                {
                    // File exists - add a number to the filename
                    var extension = IOPath.GetExtension(fileName);
                    var nameWithoutExtension = IOPath.GetFileNameWithoutExtension(fileName);
                    int counter = 1;

                    do
                    {
                        fileName = $"{nameWithoutExtension} ({counter}){extension}";
                        filePath = IOPath.Combine(_downloadFolder, fileName);
                        counter++;
                    }
                    while (File.Exists(filePath));

                    item.FileName = fileName;
                }

                item.Status = "Downloading...";

                // Download the image
                var imageBytes = await _httpClient.GetByteArrayAsync(urlToDownload, CancellationToken.None);

                await File.WriteAllBytesAsync(filePath, imageBytes, CancellationToken.None);

                if (usedBackup)
                {
                    item.Status = "✓ Backup";
                }
                else
                {
                    item.Status = "✓ Done";
                }

                item.ErrorMessage = "";
                StatusText.Text = $"Downloaded (forced): {item.FileName}";
            }
            catch (Exception ex)
            {
                item.Status = "✗ Failed";
                item.ErrorMessage = ex.Message;
                MessageBox.Show($"Failed to download: {ex.Message}", "Download Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void ContextMenu_ReloadPreview_Click(object sender, RoutedEventArgs e)
        {
            // Get the clicked item from the ListView's selected item
            if (ImageList.SelectedItem is ImageDownloadItem item)
            {
                await ReloadSinglePreviewAsync(item);
            }
        }

        private async Task ReloadSinglePreviewAsync(ImageDownloadItem item)
        {
            if (!_settings.LoadPreviews)
            {
                MessageBox.Show("Preview loading is disabled in Settings. Please enable 'Load preview images during scan' to use this feature.",
                    "Preview Loading Disabled", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                StatusText.Text = $"Reloading preview for {item.FileName}...";

                // Clear the existing preview
                item.PreviewImage = null;

                // Reload the preview
                var newPreview = await LoadPreviewImageAsync(item.Url);
                if (newPreview != null)
                {
                    item.PreviewImage = newPreview;
                    StatusText.Text = $"Preview reloaded for {item.FileName}";
                }
                else
                {
                    StatusText.Text = $"Failed to reload preview for {item.FileName}";
                    MessageBox.Show($"Failed to reload preview for {item.FileName}. The image URL may be invalid or unavailable.",
                        "Preview Load Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Error reloading preview: {ex.Message}";
                MessageBox.Show($"Error reloading preview: {ex.Message}", "Preview Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ContextMenu_Cancel_Click(object sender, RoutedEventArgs e)
        {
            // Get the clicked item from the ListView's selected item
            if (ImageList.SelectedItem is ImageDownloadItem item)
            {
                CancelSingleDownload(item);
            }
        }

        private void CancelSingleDownload(ImageDownloadItem item)
        {
            // Check if the item is currently downloading
            if (item.Status == "Downloading..." || item.Status == "Checking..." || item.Status == "Finding full-res...")
            {
                item.Status = "⊘ Canceled";
                item.ErrorMessage = "Canceled by user";
                StatusText.Text = $"Canceled download: {item.FileName}";
            }
            else
            {
                MessageBox.Show($"Cannot cancel - item is not currently downloading.\nCurrent status: {item.Status}",
                    "Cannot Cancel", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private async Task<int> DownloadImagesFromListAsync(CancellationToken cancellationToken)
        {
            // Create download folder if it doesn't exist
            if (!Directory.Exists(_downloadFolder))
            {
                Directory.CreateDirectory(_downloadFolder);
            }

            int downloadedCount = 0;
            int skippedCount = 0;
            int duplicateCount = 0;
            int totalToDownload = _imageItems.Count(i => i.Status == "Ready");

            foreach (var item in _imageItems.Where(i => i.Status == "Ready").ToList())
            {
                cancellationToken.ThrowIfCancellationRequested();

                await DownloadSingleItemAsync(item, cancellationToken);

                if (item.Status == "✓ Done") downloadedCount++;
                else if (item.Status.Contains("Duplicate")) { skippedCount++; duplicateCount++; }
                else if (item.Status.Contains("Skipped")) skippedCount++;

                // Update progress
                int processedCount = downloadedCount + skippedCount;
                DownloadProgress.Value = (processedCount * 100.0) / totalToDownload;
                StatusText.Text = $"Downloaded: {downloadedCount} | Skipped: {skippedCount} (Duplicates: {duplicateCount}) | Remaining: {totalToDownload - processedCount}";

                // Small delay to allow UI to update
                await Task.Delay(10, cancellationToken);
            }

            // Refresh current page to show updated statuses
            LoadCurrentPage();

            return downloadedCount;
        }

        private async Task DownloadSingleItemAsync(ImageDownloadItem item, CancellationToken cancellationToken)
        {
            try
            {
                item.Status = "Checking...";
                var startTime = DateTime.Now;

                string urlToDownload = item.Url;
                bool usedBackup = false;

                // Try to find full-resolution version if setting allows
                if (!_settings.SkipFullResolutionCheck)
                {
                    try
                    {
                        item.Status = "Finding full-res...";
                        var fullResStart = DateTime.Now;
                        var fullResUrl = await TryFindFullResolutionUrlAsync(item.Url, cancellationToken);
                        var fullResElapsed = (DateTime.Now - fullResStart).TotalMilliseconds;

                        // LOG: Full-res check timing
                        StatusText.Dispatcher.Invoke(() =>
                            StatusText.Text = $"Full-res check took {fullResElapsed}ms for {item.FileName}");

                        if (fullResUrl != null && fullResUrl != item.Url)
                        {
                            // Found a DIFFERENT