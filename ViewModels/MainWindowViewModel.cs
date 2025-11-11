using System.Collections.ObjectModel;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Input;
using WebsiteImagePilfer.Commands;
using WebsiteImagePilfer.Models;
using WebsiteImagePilfer.Services;
using WebsiteImagePilfer.Helpers;
using WebsiteImagePilfer.Constants;
using static WebsiteImagePilfer.Constants.AppConstants;
using IOPath = System.IO.Path;

namespace WebsiteImagePilfer.ViewModels
{
    /// <summary>
    /// ViewModel for the MainWindow, handling all business logic and state management.
    /// </summary>
    public class MainWindowViewModel : ViewModelBase, IDisposable
    {
        #region Fields

        private readonly HttpClient _httpClient;
        private readonly ImageScanner _imageScanner;
        private ImageDownloader _imageDownloader;  // Not readonly - needs to be recreated when settings change
        private readonly ImagePreviewLoader _previewLoader;

        private CancellationTokenSource? _cancellationTokenSource;
        private List<ImageDownloadItem>? _cachedReadyItems;
        private int _cachedReadyItemsVersion;
        private int _imageItemsVersion;
        private int _selectedReadyCount = 0;  // Cache for selected ready items count

        private string _url = "https://example.com";
        private string _downloadFolder = "";
        private bool _isFastScan;
        private int _currentPage = 1;
        private int _totalPages = 1;
        private string _statusText = "Ready";
        private double _progressValue;
        private string _pageInfoText = "Page 1 of 1 (0 filtered / 0 total images)";

        // Filter states
        private bool _filterReady = true;
        private bool _filterDone = true;
        private bool _filterBackup = true;
        private bool _filterDuplicate = true;
        private bool _filterFailed = true;
        private bool _filterSkipped = true;
        private bool _filterCancelled = true;
        private bool _filterDownloading = true;
        private bool _filterIgnored = true;

        // Button enable states
        private bool _isScanEnabled = true;
        private bool _isDownloadEnabled;
        private bool _isDownloadSelectedEnabled;
        private bool _isCancelEnabled;
        private bool _isPrevPageEnabled;
        private bool _isNextPageEnabled;

        #endregion

        #region Properties

        /// <summary>
        /// URL to scan for images.
        /// </summary>
        public string Url
        {
            get => _url;
            set => SetProperty(ref _url, value);
        }

        /// <summary>
        /// Download folder path.
        /// </summary>
        public string DownloadFolder
        {
            get => _downloadFolder;
            set
            {
                if (SetProperty(ref _downloadFolder, value))
                {
                    Settings.SaveToPortableSettings(_downloadFolder, Url);
                    ReinitializeServices();
                }
            }
        }

        /// <summary>
        /// Whether to use fast scan mode.
        /// </summary>
        public bool IsFastScan
        {
            get => _isFastScan;
            set => SetProperty(ref _isFastScan, value);
        }

        /// <summary>
        /// Current status text.
        /// </summary>
        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
        }

        /// <summary>
        /// Progress bar value (0-100).
        /// </summary>
        public double ProgressValue
        {
            get => _progressValue;
            set => SetProperty(ref _progressValue, value);
        }

        /// <summary>
        /// Pagination info text.
        /// </summary>
        public string PageInfoText
        {
            get => _pageInfoText;
            set => SetProperty(ref _pageInfoText, value);
        }

        /// <summary>
        /// All scanned images.
        /// </summary>
        public ObservableCollection<ImageDownloadItem> ImageItems { get; }

        /// <summary>
        /// Filtered images based on status filters.
        /// </summary>
        public ObservableCollection<ImageDownloadItem> FilteredImageItems { get; }

        /// <summary>
        /// Current page of images.
        /// </summary>
        public ObservableCollection<ImageDownloadItem> CurrentPageItems { get; }

        /// <summary>
        /// Currently selected items in the list.
        /// </summary>
        public ObservableCollection<ImageDownloadItem> SelectedItems { get; }

        /// <summary>
        /// Application settings.
        /// </summary>
        public DownloadSettings Settings { get; }

        /// <summary>
        /// Current page number for pagination.
        /// </summary>
        public int CurrentPage => _currentPage;

        /// <summary>
        /// Items per page for pagination.
        /// </summary>
        public int ItemsPerPage => Settings.ItemsPerPage;

        #region Filter Properties

        public bool FilterReady
        {
            get => _filterReady;
            set { if (SetProperty(ref _filterReady, value)) ApplyStatusFilter(); }
        }

        public bool FilterDone
        {
            get => _filterDone;
            set { if (SetProperty(ref _filterDone, value)) ApplyStatusFilter(); }
        }

        public bool FilterBackup
        {
            get => _filterBackup;
            set { if (SetProperty(ref _filterBackup, value)) ApplyStatusFilter(); }
        }

        public bool FilterDuplicate
        {
            get => _filterDuplicate;
            set { if (SetProperty(ref _filterDuplicate, value)) ApplyStatusFilter(); }
        }

        public bool FilterFailed
        {
            get => _filterFailed;
            set { if (SetProperty(ref _filterFailed, value)) ApplyStatusFilter(); }
        }

        public bool FilterSkipped
        {
            get => _filterSkipped;
            set { if (SetProperty(ref _filterSkipped, value)) ApplyStatusFilter(); }
        }

        public bool FilterCancelled
        {
            get => _filterCancelled;
            set { if (SetProperty(ref _filterCancelled, value)) ApplyStatusFilter(); }
        }

        public bool FilterDownloading
        {
            get => _filterDownloading;
            set { if (SetProperty(ref _filterDownloading, value)) ApplyStatusFilter(); }
        }

        public bool FilterIgnored
        {
            get => _filterIgnored;
            set { if (SetProperty(ref _filterIgnored, value)) ApplyStatusFilter(); }
        }

        #endregion

        #region Button Enable Properties

        public bool IsScanEnabled
        {
            get => _isScanEnabled;
            set => SetProperty(ref _isScanEnabled, value);
        }

        public bool IsDownloadEnabled
        {
            get => _isDownloadEnabled;
            set => SetProperty(ref _isDownloadEnabled, value);
        }

        public bool IsDownloadSelectedEnabled
        {
            get => _isDownloadSelectedEnabled;
            set => SetProperty(ref _isDownloadSelectedEnabled, value);
        }

        public bool IsCancelEnabled
        {
            get => _isCancelEnabled;
            set => SetProperty(ref _isCancelEnabled, value);
        }

        public bool IsPrevPageEnabled
        {
            get => _isPrevPageEnabled;
            set => SetProperty(ref _isPrevPageEnabled, value);
        }

        public bool IsNextPageEnabled
        {
            get => _isNextPageEnabled;
            set => SetProperty(ref _isNextPageEnabled, value);
        }

        #endregion

        #endregion

        #region Commands

        public ICommand ScanCommand { get; }
        public ICommand DownloadCommand { get; }
        public ICommand DownloadSelectedCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand BrowseFolderCommand { get; }
        public ICommand UpFolderCommand { get; }
        public ICommand NewFolderCommand { get; }
        public ICommand OpenFolderCommand { get; }
        public ICommand SettingsCommand { get; }
        public ICommand SelectAllStatusCommand { get; }
        public ICommand ClearAllStatusCommand { get; }
        public ICommand PrevPageCommand { get; }
        public ICommand NextPageCommand { get; }
        public ICommand ItemDoubleClickCommand { get; }
        public ICommand DownloadSingleCommand { get; }
        public ICommand ForceDownloadSingleCommand { get; }
        public ICommand ReloadPreviewCommand { get; }
        public ICommand CancelSingleCommand { get; }
        public ICommand IgnoreCommand { get; }
        public ICommand UnignoreCommand { get; }

        #endregion

        #region Constructor

        public MainWindowViewModel()
        {
            // Initialize collections
            ImageItems = new ObservableCollection<ImageDownloadItem>();
            FilteredImageItems = new ObservableCollection<ImageDownloadItem>();
            CurrentPageItems = new ObservableCollection<ImageDownloadItem>();
            SelectedItems = new ObservableCollection<ImageDownloadItem>();

            // Load settings
            var appSettings = PortableSettingsManager.LoadSettings();
            Settings = new DownloadSettings();
            Settings.LoadFromPortableSettings();

            // Initialize download folder
            _downloadFolder = string.IsNullOrEmpty(appSettings.DownloadFolder)
                ? IOPath.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "WebsiteImages")
                : appSettings.DownloadFolder;

            // Restore last URL
            if (!string.IsNullOrEmpty(appSettings.LastUrl))
                _url = appSettings.LastUrl;

            // Initialize services
            _httpClient = HttpClientFactory.Instance;
            _imageScanner = new ImageScanner(status => StatusText = status, Settings);
            _imageDownloader = new ImageDownloader(_httpClient, Settings, _downloadFolder);
            _previewLoader = new ImagePreviewLoader(_httpClient);

            // Initialize commands
            ScanCommand = new AsyncRelayCommand(async _ => await ScanAsync());
            DownloadCommand = new AsyncRelayCommand(async _ => await DownloadAllAsync());
            DownloadSelectedCommand = new AsyncRelayCommand(async _ => await DownloadSelectedAsync());
            CancelCommand = new RelayCommand(_ => CancelOperation());
            BrowseFolderCommand = new RelayCommand(_ => BrowseFolder());
            UpFolderCommand = new RelayCommand(_ => UpFolder());
            NewFolderCommand = new RelayCommand(_ => NewFolder());
            OpenFolderCommand = new RelayCommand(_ => OpenFolder());
            SettingsCommand = new RelayCommand(_ => OpenSettings());
            SelectAllStatusCommand = new RelayCommand(_ => SelectAllStatus());
            ClearAllStatusCommand = new RelayCommand(_ => ClearAllStatus());
            PrevPageCommand = new RelayCommand(_ => PreviousPage());
            NextPageCommand = new RelayCommand(_ => NextPage());
            ItemDoubleClickCommand = new AsyncRelayCommand(async item => await OnItemDoubleClick(item));
            DownloadSingleCommand = new AsyncRelayCommand(async item => await DownloadSingleAsync(item));
            ForceDownloadSingleCommand = new AsyncRelayCommand(async item => await ForceDownloadSingleAsync(item));
            ReloadPreviewCommand = new AsyncRelayCommand(async item => await ReloadPreviewAsync(item));
            CancelSingleCommand = new RelayCommand(item => CancelSingle(item));
            IgnoreCommand = new RelayCommand(item => IgnoreItem(item));
            UnignoreCommand = new RelayCommand(item => UnignoreItem(item));

            // Setup collection monitoring
            ImageItems.CollectionChanged += (s, e) =>
            {
                InvalidateReadyItemsCache();

                // Unsubscribe from removed items to prevent memory leaks
                if (e.OldItems != null)
                {
                    foreach (ImageDownloadItem item in e.OldItems)
                        item.PropertyChanged -= ImageItem_PropertyChanged;
                }

                // Subscribe to new items
                if (e.NewItems != null)
                {
                    foreach (ImageDownloadItem item in e.NewItems)
                        item.PropertyChanged += ImageItem_PropertyChanged;
                }

                if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add ||
                    e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Reset)
                    ApplyStatusFilter();
            };

            SelectedItems.CollectionChanged += (s, e) =>
            {
                RecalculateSelectedReadyCount();
                UpdateDownloadSelectedButtonState();
            };

            UpdatePagination();
        }

        #endregion

        #region Command Implementations

        private async Task ScanAsync()
        {
            if (string.IsNullOrEmpty(Url))
            {
                ShowWarning("Please enter a valid URL.");
                return;
            }

            if (!Uri.IsWellFormedUriString(Url, UriKind.Absolute))
            {
                ShowWarning("Please enter a valid URL (including http:// or https://).");
                return;
            }

            Settings.SaveToPortableSettings(DownloadFolder, Url);
            _cancellationTokenSource = new CancellationTokenSource();
            SetScanningState();
            ImageItems.Clear();

            try
            {
                var scannedImageUrls = await _imageScanner.ScanForImagesAsync(Url, _cancellationTokenSource.Token, IsFastScan);

                if (_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    StatusText = "Scan cancelled.";
                    return;
                }

                if (scannedImageUrls.Count == 0)
                {
                    SetScanCompleteState(0, false);
                    ShowInfo("No images found on the webpage.");
                    return;
                }

                foreach (var imageUrl in scannedImageUrls)
                {
                    var fileName = FileNameExtractor.ExtractFromUrl(imageUrl);
                    var filePath = IOPath.Combine(DownloadFolder, fileName);
                    if (Settings.LimitScanCount && File.Exists(filePath))
                        continue;

                    var item = new ImageDownloadItem { Url = imageUrl, Status = Status.Ready, FileName = fileName };
                    ImageItems.Add(item);

                    // BOORU MODE FIX: Skip preview loading for Booru detail pages
                    // These are HTML pages, not direct image URLs, and will be resolved during download
                    bool isBooruDetailPage = Settings.EnableBooruMode && IsBooruDetailPageUrl(imageUrl);
                    
                    if (Settings.LoadPreviews && !isBooruDetailPage)
                    {
                        _ = LoadAndSetPreviewAsync(item);
                    }
                    else if (isBooruDetailPage)
                    {
                        Logger.Debug($"Skipping preview load for Booru detail page: {imageUrl}");
                    }

                    if (Settings.LimitScanCount && ImageItems.Count >= Settings.MaxImagesToScan)
                        break;
                }

                string scanType = IsFastScan ? "Fast" : "Thorough";
                string limitInfo = Settings.LimitScanCount ? $" (limited to {Settings.MaxImagesToScan})" : "";
                string booruInfo = Settings.EnableBooruMode ? " (Booru mode)" : "";

                _currentPage = 1;
                UpdatePagination();
                SetScanCompleteState(ImageItems.Count, ImageItems.Count > 0);
                StatusText = $"Found {ImageItems.Count} images{limitInfo}{booruInfo} ({scanType} scan). Click 'Download' to save them.";

                string scanMessage = Settings.LimitScanCount
                    ? $"Found {ImageItems.Count} images (limited to {Settings.MaxImagesToScan}) using {scanType} scan{booruInfo}!\n\nDuplicates were automatically skipped.\n\nReview the list and click 'Download' when ready."
                    : $"Found {ImageItems.Count} images using {scanType} scan{booruInfo}!\n\nReview the list and click 'Download' when ready.";

                ShowInfo(scanMessage, "Scan Complete");
            }
            catch (OperationCanceledException)
            {
                SetCancelledState();
                StatusText = "Scan cancelled by user.";
            }
            catch (Exception ex)
            {
                SetReadyState();
                ShowError($"Error: {ex.Message}");
                StatusText = "Error occurred during scan.";
            }
            finally
            {
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
            }
        }

        /// <summary>
        /// Checks if a URL is a Booru detail/view page rather than a direct image URL.
        /// Used to skip preview loading for these pages since they're HTML, not images.
        /// </summary>
        private bool IsBooruDetailPageUrl(string url)
        {
            var lowerUrl = url.ToLowerInvariant();
            
            // Check if it's from a known Booru site
            bool isBooruSite = lowerUrl.Contains("safebooru") || 
                              lowerUrl.Contains("gelbooru") || 
                              lowerUrl.Contains("danbooru") ||
                              lowerUrl.Contains("konachan") ||
                              lowerUrl.Contains("yande.re") ||
                              lowerUrl.Contains("sankaku");
            
            if (!isBooruSite)
                return false;
            
            // Check if it's a detail/view page
            bool isDetailPage = lowerUrl.Contains("page=post") &&
                               (lowerUrl.Contains("s=view") || lowerUrl.Contains("s=show"));
            
            return isDetailPage;
        }

        private async Task DownloadAllAsync()
        {
            if (ImageItems.Count == 0)
            {
                ShowWarning("No images to download. Please scan a webpage first.", "No Images");
                return;
            }

            await PerformDownloadAsync(GetReadyItems(), ImageItems.Count);
        }

        private async Task DownloadSelectedAsync()
        {
            var readyItems = SelectedItems.Where(item => item.Status == Status.Ready).ToList();

            if (readyItems.Count == 0)
            {
                var hasSelection = SelectedItems.Count > 0;
                if (hasSelection)
                    ShowInfo("No ready images selected. Selected images may already be downloaded.", "No Ready Images");
                else
                    ShowWarning("No images selected. Please select images to download.", "No Selection");
                return;
            }

            await PerformDownloadAsync(readyItems, readyItems.Count);
        }

        private void CancelOperation()
        {
            _cancellationTokenSource?.Cancel();
            SetCancellingState();
        }

        private void BrowseFolder()
        {
            string initialDir = GetValidInitialDirectory(DownloadFolder);
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Select Download Folder",
                InitialDirectory = initialDir
            };

            if (dialog.ShowDialog() == true)
            {
                DownloadFolder = dialog.FolderName;
            }
        }

        private void UpFolder()
        {
            try
            {
                var parentDir = IOPath.GetDirectoryName(DownloadFolder);
                if (!string.IsNullOrEmpty(parentDir) && Directory.Exists(parentDir))
                {
                    DownloadFolder = parentDir;
                    StatusText = $"Navigated up to: {DownloadFolder}";
                }
                else
                {
                    ShowInfo("Cannot navigate up - already at root directory.", "Cannot Go Up");
                }
            }
            catch (Exception ex)
            {
                ShowError($"Failed to navigate up: {ex.Message}", "Error Navigating");
            }
        }

        private void NewFolder()
        {
            try
            {
                string parentDir = Directory.Exists(DownloadFolder)
                    ? DownloadFolder
                    : IOPath.GetDirectoryName(DownloadFolder) ?? Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);

                string newFolderPath = GenerateUniqueFolderPath(parentDir, "New Folder");
                Directory.CreateDirectory(newFolderPath);

                var finalName = PromptForFolderName(IOPath.GetFileName(newFolderPath));
                if (finalName != null)
                {
                    if (finalName != IOPath.GetFileName(newFolderPath))
                    {
                        string finalPath = IOPath.Combine(parentDir, finalName);
                        if (Directory.Exists(finalPath))
                        {
                            ShowWarning($"A folder named '{finalName}' already exists.", "Name Conflict");
                            Directory.Delete(newFolderPath);
                            return;
                        }
                        Directory.Move(newFolderPath, finalPath);
                        newFolderPath = finalPath;
                    }

                    DownloadFolder = newFolderPath;
                    StatusText = $"New folder created: {finalName}";
                }
                else
                {
                    Directory.Delete(newFolderPath);
                }
            }
            catch (Exception ex)
            {
                ShowError($"Error creating folder: {ex.Message}");
            }
        }

        private void OpenFolder()
        {
            try
            {
                if (!Directory.Exists(DownloadFolder))
                    Directory.CreateDirectory(DownloadFolder);

                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = DownloadFolder,
                    UseShellExecute = true,
                    Verb = "open"
                });

                StatusText = $"Opened folder: {DownloadFolder}";
            }
            catch (Exception ex)
            {
                ShowError($"Failed to open folder: {ex.Message}", "Error Opening Folder");
            }
        }

        private void OpenSettings()
        {
            var settingsWindow = new SettingsWindow(Settings);
            if (settingsWindow.ShowDialog() == true)
            {
                Settings.SaveToPortableSettings(DownloadFolder, Url);
                if (_currentPage != 1 || FilteredImageItems.Count != ImageItems.Count)
                {
                    _currentPage = 1;
                    UpdatePagination();
                }
                ReinitializeServices();
                StatusText = "Settings saved.";
            }
        }

        private void SelectAllStatus()
        {
            FilterReady = FilterDone = FilterBackup = FilterDuplicate =
                FilterFailed = FilterSkipped = FilterCancelled = FilterDownloading = FilterIgnored = true;
        }

        private void ClearAllStatus()
        {
            FilterReady = FilterDone = FilterBackup = FilterDuplicate =
                FilterFailed = FilterSkipped = FilterCancelled = FilterDownloading = FilterIgnored = false;
        }

        private void PreviousPage()
        {
            if (_currentPage > 1)
            {
                _currentPage--;
                UpdatePagination();
            }
        }

        private void NextPage()
        {
            if (_currentPage < _totalPages)
            {
                _currentPage++;
                UpdatePagination();
            }
        }

        private async Task OnItemDoubleClick(object? parameter)
        {
            if (parameter is ImageDownloadItem item)
            {
                if (item.Status == Status.Done || item.Status == Status.Backup || item.Status == Status.Duplicate)
                    OpenDownloadedFile(item);
                else
                    await DownloadSingleAsync(item);
            }
        }

        private async Task DownloadSingleAsync(object? parameter)
        {
            if (parameter is not ImageDownloadItem item)
                return;

            if (item.Status == Status.Done || item.Status == Status.Duplicate)
            {
                ShowInfo($"Image already downloaded: {item.FileName}", "Already Downloaded");
                return;
            }

            var originalStatus = item.Status;
            item.Status = Status.Downloading;

            try
            {
                // Use global cancellation token if available, otherwise use default
                var cancellationToken = _cancellationTokenSource?.Token ?? default;
                await _imageDownloader.DownloadSingleItemAsync(item, cancellationToken);
                if (item.Status == Status.Done)
                    StatusText = $"Downloaded: {item.FileName}";
            }
            catch (OperationCanceledException)
            {
                item.Status = Status.Cancelled;
                item.ErrorMessage = "Cancelled by user";
                StatusText = $"Cancelled download: {item.FileName}";
            }
            catch (Exception ex)
            {
                item.Status = Status.Failed;
                item.ErrorMessage = ex.Message;
                ShowError($"Failed to download: {ex.Message}", "Download Error");
            }
        }

        private async Task ForceDownloadSingleAsync(object? parameter)
        {
            if (parameter is not ImageDownloadItem item)
                return;

            var originalStatus = item.Status;
            item.Status = Status.Downloading;

            try
            {
                // Use global cancellation token if available, otherwise use default
                var cancellationToken = _cancellationTokenSource?.Token ?? default;
                await _imageDownloader.DownloadSingleItemAsync(item, cancellationToken, forceDownload: true);
                if (item.Status == Status.Done || item.Status == Status.Backup)
                    StatusText = $"Force downloaded: {item.FileName}";
            }
            catch (OperationCanceledException)
            {
                item.Status = Status.Cancelled;
                item.ErrorMessage = "Cancelled by user";
                StatusText = $"Cancelled download: {item.FileName}";
            }
            catch (Exception ex)
            {
                item.Status = Status.Failed;
                item.ErrorMessage = ex.Message;
                ShowError($"Failed to download: {ex.Message}", "Download Error");
            }
        }

        private async Task ReloadPreviewAsync(object? parameter)
        {
            if (parameter is not ImageDownloadItem item)
                return;

            if (!Settings.LoadPreviews)
            {
                ShowInfo("Preview loading is disabled in Settings. Please enable 'Load preview images during scan' to use this feature.", "Preview Loading Disabled");
                return;
            }

            try
            {
                StatusText = $"Reloading preview for {item.FileName}...";
                item.PreviewImage = null;

                // Get the preview column width from the view (we'll need to pass this somehow)
                var newPreview = await _previewLoader.LoadPreviewImageFromColumnWidthAsync(item.Url, 150).ConfigureAwait(false); // Default width
                if (newPreview != null)
                {
                    item.PreviewImage = newPreview;
                    StatusText = $"Preview reloaded for {item.FileName}";
                }
                else
                {
                    StatusText = $"Failed to reload preview for {item.FileName}";
                    ShowWarning($"Failed to reload preview for {item.FileName}. The image URL may be invalid or unavailable.", "Preview Load Failed");
                }
            }
            catch (Exception ex)
            {
                StatusText = $"Error reloading preview: {ex.Message}";
                ShowError($"Error reloading preview: {ex.Message}", "Preview Error");
            }
        }

        private void CancelSingle(object? parameter)
        {
            if (parameter is not ImageDownloadItem item)
                return;

            if (item.Status == Status.Downloading || item.Status == Status.Checking || item.Status == Status.FindingFullRes)
            {
                item.Status = Status.Cancelled;
                item.ErrorMessage = "Canceled by user";
                StatusText = $"Canceled download: {item.FileName}";
            }
            else
            {
                ShowInfo($"Cannot cancel - item is not currently downloading.\nCurrent status: {item.Status}", "Cannot Cancel");
            }
        }

        private void IgnoreItem(object? parameter)
        {
            if (parameter is not ImageDownloadItem item)
                return;

            item.PreviousStatus = item.Status;
            item.Status = Status.Ignored;
            StatusText = $"Ignored: {item.FileName}";
        }

        private void UnignoreItem(object? parameter)
        {
            if (parameter is not ImageDownloadItem item)
                return;

            if (item.Status == Status.Ignored)
            {
                item.Status = item.PreviousStatus ?? Status.Ready;
                item.PreviousStatus = null;
                StatusText = $"Unignored: {item.FileName}";
            }
        }

        #endregion

        #region Helper Methods

        private async Task PerformDownloadAsync(List<ImageDownloadItem> itemsToDownload, int totalCount)
        {
            _cancellationTokenSource = new CancellationTokenSource();
            SetDownloadingState();

            try
            {
                int downloadedCount = await DownloadItemsAsync(itemsToDownload, totalCount, _cancellationTokenSource.Token);

                if (_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    SetCancelledState();
                    StatusText = $"Cancelled. Downloaded {downloadedCount} images.";
                    ShowInfo($"Operation cancelled. Downloaded {downloadedCount} images.", "Cancelled");
                }
                else
                {
                    SetDownloadCompleteState();
                    StatusText = $"Download complete! {downloadedCount} images saved to {DownloadFolder}";
                    ShowInfo($"Successfully downloaded {downloadedCount} images!", "Success");
                }
            }
            catch (OperationCanceledException)
            {
                SetCancelledState();
                StatusText = "Download cancelled by user.";
            }
            catch (Exception ex)
            {
                SetReadyState();
                ShowError($"Error: {ex.Message}");
                StatusText = "Error occurred during download.";
            }
            finally
            {
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
                UpdateDownloadSelectedButtonState();
            }
        }

        private async Task<int> DownloadItemsAsync(List<ImageDownloadItem> items, int total, CancellationToken cancellationToken)
        {
            if (!Directory.Exists(DownloadFolder))
                Directory.CreateDirectory(DownloadFolder);

            int downloadedCount = 0, skippedCount = 0, duplicateCount = 0;

            foreach (var item in items)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await _imageDownloader.DownloadSingleItemAsync(item, cancellationToken);

                if (item.Status == Status.Done) downloadedCount++;
                else if (item.Status.Contains(Status.Duplicate, StringComparison.Ordinal)) { skippedCount++; duplicateCount++; }
                else if (item.Status.Contains(Status.Skipped, StringComparison.Ordinal)) skippedCount++;

                int remaining = total - (downloadedCount + skippedCount);
                UpdateDownloadProgress(downloadedCount, skippedCount, duplicateCount, remaining, total);
            }

            LoadCurrentPage();
            return downloadedCount;
        }

        private void UpdateDownloadProgress(int downloaded, int skipped, int duplicates, int remaining, int total)
        {
            ProgressValue = total > 0 ? (double)(downloaded + skipped) / total * 100 : 0;
            StatusText = $"Downloaded: {downloaded} | Skipped: {skipped} (Duplicates: {duplicates}) | Remaining: {remaining}";
        }

        private async Task LoadAndSetPreviewAsync(ImageDownloadItem item)
        {
            try
            {
                // Default preview width, will be updated if column width changes
                var preview = await _previewLoader.LoadPreviewImageFromColumnWidthAsync(item.Url, 150).ConfigureAwait(false);
                if (preview != null)
                    item.PreviewImage = preview;
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to load preview for {item.Url}", ex);
            }
        }

        private void OpenDownloadedFile(ImageDownloadItem item)
        {
            var filePath = IOPath.Combine(DownloadFolder, item.FileName);
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
                    StatusText = $"Opened: {item.FileName}";
                }
                catch (Exception ex)
                {
                    ShowError($"Failed to open file: {ex.Message}", "Error Opening File");
                }
            }
            else
            {
                ShowWarning($"File not found: {item.FileName}\n\nThe file may have been moved or deleted.", "File Not Found");
            }
        }

        private void ApplyStatusFilter()
        {
            ImageDownloadItem? firstVisibleItem = CurrentPageItems.Count > 0 ? CurrentPageItems[0] : null;
            FilteredImageItems.Clear();

            foreach (var item in ImageItems)
            {
                bool include = (FilterReady && item.Status == Status.Ready) ||
                    (FilterDone && item.Status == Status.Done) ||
                    (FilterBackup && item.Status == Status.Backup) ||
                    (FilterDuplicate && item.Status == Status.Duplicate) ||
                    (FilterFailed && item.Status == Status.Failed) ||
                    (FilterSkipped && item.Status.Contains(Status.Skipped)) ||
                    (FilterCancelled && item.Status == Status.Cancelled) ||
                    (FilterDownloading && (item.Status == Status.Downloading || item.Status == Status.Checking || item.Status == Status.FindingFullRes)) ||
                    (FilterIgnored && item.Status == Status.Ignored);

                if (include)
                    FilteredImageItems.Add(item);
            }

            if (firstVisibleItem != null)
            {
                int newIndex = FilteredImageItems.IndexOf(firstVisibleItem);
                if (newIndex >= 0)
                    _currentPage = (newIndex / Settings.ItemsPerPage) + 1;
            }
            else
            {
                _currentPage = 1;
            }

            UpdatePagination();
        }

        private void UpdatePagination()
        {
            _totalPages = (int)Math.Ceiling((double)FilteredImageItems.Count / Settings.ItemsPerPage);
            if (_totalPages == 0) _totalPages = 1;
            if (_currentPage > _totalPages) _currentPage = _totalPages;
            if (_currentPage < 1) _currentPage = 1;

            PageInfoText = $"Page {_currentPage} of {_totalPages} ({FilteredImageItems.Count} filtered / {ImageItems.Count} total images)";
            IsPrevPageEnabled = _currentPage > 1;
            IsNextPageEnabled = _currentPage < _totalPages;

            OnPropertyChanged(nameof(CurrentPage)); // Notify converter that page changed

            LoadCurrentPage();
        }

        private void LoadCurrentPage()
        {
            CurrentPageItems.Clear();
            int startIndex = (_currentPage - 1) * Settings.ItemsPerPage;
            int endIndex = Math.Min(startIndex + Settings.ItemsPerPage, FilteredImageItems.Count);

            for (int i = startIndex; i < endIndex; i++)
                CurrentPageItems.Add(FilteredImageItems[i]);
        }

        private void ImageItem_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ImageDownloadItem.Status))
            {
                ApplyStatusFilter();
                RecalculateSelectedReadyCount();
            }
        }

        private void InvalidateReadyItemsCache()
        {
            _cachedReadyItems = null;
            _imageItemsVersion++;
        }

        private List<ImageDownloadItem> GetReadyItems()
        {
            if (_cachedReadyItems != null && _cachedReadyItemsVersion == _imageItemsVersion)
                return _cachedReadyItems;

            _cachedReadyItems = ImageItems.Where(i => i.Status == Status.Ready).ToList();
            _cachedReadyItemsVersion = _imageItemsVersion;
            return _cachedReadyItems;
        }

        private void UpdateDownloadSelectedButtonState()
        {
            IsDownloadSelectedEnabled = _selectedReadyCount > 0 && !IsCancelEnabled;
        }

        private void RecalculateSelectedReadyCount()
        {
            _selectedReadyCount = SelectedItems.Count(item => item.Status == Status.Ready);
        }

        private void ReinitializeServices()
        {
            // Re-create image downloader with new settings/folder
            _imageDownloader = new ImageDownloader(_httpClient, Settings, DownloadFolder);
        }

        private string GetValidInitialDirectory(string folder)
        {
            if (Directory.Exists(folder)) return folder;
            var parentDir = IOPath.GetDirectoryName(folder);
            if (!string.IsNullOrEmpty(parentDir) && Directory.Exists(parentDir)) return parentDir;
            return Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
        }

        private string GenerateUniqueFolderPath(string parentDir, string baseName)
        {
            string path = IOPath.Combine(parentDir, baseName);
            int counter = 1;
            while (Directory.Exists(path))
            {
                path = IOPath.Combine(parentDir, $"{baseName} ({counter})");
                counter++;
            }
            return path;
        }

        private string? PromptForFolderName(string defaultName)
        {
            // This will need to be handled by the View
            // For now, return the default name
            // TODO: Implement proper dialog service for MVVM
            return defaultName;
        }

        #endregion

        #region State Management

        private void SetReadyState()
        {
            IsScanEnabled = true;
            IsDownloadEnabled = ImageItems.Count > 0;
            IsCancelEnabled = false;
            ProgressValue = 0;
            UpdateDownloadSelectedButtonState();
        }

        private void SetScanningState()
        {
            IsScanEnabled = false;
            IsDownloadEnabled = false;
            IsDownloadSelectedEnabled = false;
            IsCancelEnabled = true;
            StatusText = "Scanning webpage...";
        }

        private void SetScanCompleteState(int imageCount, bool hasImages)
        {
            IsScanEnabled = true;
            IsDownloadEnabled = hasImages;
            IsCancelEnabled = false;
            UpdateDownloadSelectedButtonState();
        }

        private void SetDownloadingState()
        {
            IsScanEnabled = false;
            IsDownloadEnabled = false;
            IsDownloadSelectedEnabled = false;
            IsCancelEnabled = true;
        }

        private void SetDownloadCompleteState()
        {
            SetReadyState();
        }

        private void SetCancelledState()
        {
            SetReadyState();
        }

        private void SetCancellingState()
        {
            IsCancelEnabled = false;
            StatusText = "Cancelling...";
        }

        #endregion

        #region Message Dialogs

        // These should ideally be handled through a dialog service for testability
        // For now, we'll use MessageBox directly

        private void ShowInfo(string message, string title = "Info")
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ShowWarning(string message, string title = "Warning")
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        private void ShowError(string message, string title = "Error")
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
        }

        #endregion
    }
}
