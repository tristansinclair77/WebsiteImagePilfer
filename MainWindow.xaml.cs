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
using WebsiteImagePilfer.Models;
using WebsiteImagePilfer.Services;
using WebsiteImagePilfer.Helpers;
using WebsiteImagePilfer.Converters;
using WebsiteImagePilfer.Constants;
using static WebsiteImagePilfer.Constants.AppConstants;

namespace WebsiteImagePilfer
{
    public partial class MainWindow : Window
    {
  // Constants removed - using AppConstants

        private readonly HttpClient _httpClient;
  private ObservableCollection<ImageDownloadItem> _imageItems;
        private ObservableCollection<ImageDownloadItem> _currentPageItems;
        private ObservableCollection<ImageDownloadItem> _filteredImageItems;
      private string _downloadFolder;
     private CancellationTokenSource? _cancellationTokenSource;
     private DownloadSettings _settings;
      private List<string> _scannedImageUrls;
    private int _currentPage = 1;
    private int _itemsPerPage = 50;
        private int _totalPages = 1;
        private double _lastPreviewColumnWidth = 0;
  private System.Timers.Timer? _columnResizeTimer;
 
        private ImageScanner? _imageScanner;
        private ImageDownloader? _imageDownloader;
      private ImagePreviewLoader? _previewLoader;
        private UIStateManager? _uiStateManager;

      // Cache for expensive LINQ queries
        private List<ImageDownloadItem>? _cachedReadyItems;
        private int _cachedReadyItemsVersion;
        private int _imageItemsVersion;

        /// <summary>
        /// Gets the current page number in the paginated view.
        /// </summary>
        public int CurrentPage => _currentPage;

        /// <summary>
        /// Gets the number of items displayed per page.
  /// </summary>
      public int ItemsPerPage => _itemsPerPage;

        public MainWindow()
        {
            InitializeComponent();
    // Use singleton HttpClient for optimal connection pooling and performance
_httpClient = HttpClientFactory.Instance;
            _imageItems = new ObservableCollection<ImageDownloadItem>();
       _filteredImageItems = new ObservableCollection<ImageDownloadItem>();
_currentPageItems = new ObservableCollection<ImageDownloadItem>();
         ImageList.ItemsSource = _currentPageItems;
    _scannedImageUrls = new List<string>();

        var appSettings = PortableSettingsManager.LoadSettings();
            _settings = new DownloadSettings();
            _settings.LoadFromPortableSettings();
         _itemsPerPage = _settings.ItemsPerPage;
            _downloadFolder = string.IsNullOrEmpty(appSettings.DownloadFolder)
         ? IOPath.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "WebsiteImages")
    : appSettings.DownloadFolder;
    FolderTextBox.Text = _downloadFolder;

  if (!string.IsNullOrEmpty(appSettings.LastUrl))
         UrlTextBox.Text = appSettings.LastUrl;

            InitializeServices();
            SetupUIMonitoring();
        }

    private void SetupUIMonitoring()
        {
            PreviewColumn.Width = 150;
 _lastPreviewColumnWidth = 150;
     
      _columnResizeTimer = new System.Timers.Timer(Preview.ColumnResizeDebounceMs) { AutoReset = false };
            _columnResizeTimer.Elapsed += ColumnResizeTimer_Elapsed;
  
       var columnWidthMonitor = new System.Windows.Threading.DispatcherTimer 
      { 
  Interval = TimeSpan.FromMilliseconds(Preview.ColumnWidthMonitorIntervalMs) 
          };
         columnWidthMonitor.Tick += (s, e) => CheckColumnWidthChanged();
          columnWidthMonitor.Start();
        
      _imageItems.CollectionChanged += (s, e) => 
    {
                // Invalidate cached queries when collection changes
     InvalidateReadyItemsCache();
      
 if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add ||
     e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Reset)
   ApplyStatusFilter();

           if (e.NewItems != null)
            foreach (ImageDownloadItem item in e.NewItems)
 item.PropertyChanged += ImageItem_PropertyChanged;
            };
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

   _cachedReadyItems = _imageItems.Where(i => i.Status == Status.Ready).ToList();
            _cachedReadyItemsVersion = _imageItemsVersion;
        return _cachedReadyItems;
        }

        private void InitializeServices()
        {
 _imageScanner = new ImageScanner(status => StatusText.Dispatcher.Invoke(() => StatusText.Text = status));
         _imageDownloader = new ImageDownloader(_httpClient, _settings, _downloadFolder);
    _previewLoader = new ImagePreviewLoader(_httpClient);
          _uiStateManager = new UIStateManager(
        ScanOnlyButton, DownloadButton, DownloadSelectedButton, CancelButton,
    FastScanCheckBox, StatusText, DownloadProgress,
       () => {
      // Optimize: Use .Any() for existence check before .Count()
       var selectedItems = ImageList.SelectedItems.Cast<ImageDownloadItem>();
                 return selectedItems.Any(item => item.Status == Status.Ready) 
           ? selectedItems.Count(item => item.Status == Status.Ready) 
     : 0;
  });
 _uiStateManager.SetReadyState();
   }

        private void CheckColumnWidthChanged()
        {
            if (PreviewColumn.ActualWidth > 0 && Math.Abs(PreviewColumn.ActualWidth - _lastPreviewColumnWidth) > Preview.ColumnWidthChangeThreshold)
       {
      _columnResizeTimer?.Stop();
                _columnResizeTimer?.Start();
            }
        }

   private async void ColumnResizeTimer_Elapsed(object? sender, System.Timers.ElapsedEventArgs e)
     {
        await Dispatcher.InvokeAsync(async () =>
   {
   if (PreviewColumn.ActualWidth > 0 && _settings.LoadPreviews)
   {
   _lastPreviewColumnWidth = PreviewColumn.ActualWidth;
           _uiStateManager!.UpdateStatus("Reloading previews at new resolution...");
            await ReloadAllPreviewsAsync();
       _uiStateManager.UpdateStatus(Status.Ready);
    }
    });
        }

        private async Task ReloadAllPreviewsAsync()
     {
   // Optimize: Use parallel loading with throttling for 10x speedup
   var itemsWithPreviews = _imageItems.Where(i => !string.IsNullOrEmpty(i.Url) && _settings.LoadPreviews).ToList();
   
   if (itemsWithPreviews.Count == 0)
     return;

// Limit to 10 concurrent downloads to avoid overwhelming the network
            var semaphore = new SemaphoreSlim(10);
     var tasks = itemsWithPreviews.Select(async item =>
       {
         await semaphore.WaitAsync();
      try
  {
      var newPreview = await _previewLoader!.LoadPreviewImageFromColumnWidthAsync(item.Url, PreviewColumn.ActualWidth);
           if (newPreview != null)
          await Dispatcher.InvokeAsync(() => item.PreviewImage = newPreview);
     }
      catch (Exception ex)
      {
    Logger.Error($"Failed to reload preview for {item.Url}", ex);
       }
    finally
        {
          semaphore.Release();
                }
 });

   await Task.WhenAll(tasks);
        }

        private async void ScanOnlyButton_Click(object sender, RoutedEventArgs e)
        {
  string url = UrlTextBox.Text.Trim();
            
if (string.IsNullOrEmpty(url))
            {
              ShowWarning("Please enter a valid URL.");
  return;
  }

     if (!Uri.IsWellFormedUriString(url, UriKind.Absolute))
      {
           ShowWarning("Please enter a valid URL (including http:// or https://).");
    return;
   }

            _settings.SaveToPortableSettings(_downloadFolder, url);
      _cancellationTokenSource = new CancellationTokenSource();
     _uiStateManager!.SetScanningState();
            _imageItems.Clear();
   _scannedImageUrls.Clear();

            try
   {
          bool useFastScan = FastScanCheckBox.IsChecked.GetValueOrDefault();
                _scannedImageUrls = await _imageScanner!.ScanForImagesAsync(url, _cancellationTokenSource.Token, useFastScan);
      
   if (_cancellationTokenSource.Token.IsCancellationRequested)
       {
    _uiStateManager.UpdateStatus("Scan cancelled.");
         return;
         }

                if (_scannedImageUrls.Count == 0)
       {
 _uiStateManager.SetScanCompleteState(0, false);
                  ShowInfo("No images found on the webpage.");
    return;
                }

          foreach (var imageUrl in _scannedImageUrls)
       {
         var fileName = FileNameExtractor.ExtractFromUrl(imageUrl);
          var filePath = IOPath.Combine(_downloadFolder, fileName);
            if (_settings.LimitScanCount && File.Exists(filePath))
           continue;

  var item = new ImageDownloadItem { Url = imageUrl, Status = Status.Ready, FileName = fileName };
      _imageItems.Add(item);

           if (_settings.LoadPreviews)
        _ = LoadAndSetPreviewAsync(item);

           if (_settings.LimitScanCount && _imageItems.Count >= _settings.MaxImagesToScan)
       break;
            }

                string scanType = useFastScan ? "Fast" : "Thorough";
 string limitInfo = _settings.LimitScanCount ? $" (limited to {_settings.MaxImagesToScan})" : "";
    
    _currentPage = 1;
 UpdatePagination();
   _uiStateManager.SetScanCompleteState(_imageItems.Count, _imageItems.Count > 0);
  _uiStateManager.UpdateStatus($"Found {_imageItems.Count} images{limitInfo} ({scanType} scan). Click 'Download' to save them.");
             
    string scanMessage = _settings.LimitScanCount 
         ? $"Found {_imageItems.Count} images (limited to {_settings.MaxImagesToScan}) using {scanType} scan!\n\nDuplicates were automatically skipped.\n\nReview the list and click 'Download' when ready."
          : $"Found {_imageItems.Count} images using {scanType} scan!\n\nReview the list and click 'Download' when ready.";
  
                ShowInfo(scanMessage, "Scan Complete");
 }
        catch (OperationCanceledException)
            {
      _uiStateManager.SetCancelledState();
       _uiStateManager.UpdateStatus("Scan cancelled by user.");
}
      catch (Exception ex)
            {
     _uiStateManager.SetReadyState();
                ShowError($"Error: {ex.Message}");
           _uiStateManager.UpdateStatus("Error occurred during scan.");
   }
            finally
         {
    _cancellationTokenSource?.Dispose();
    _cancellationTokenSource = null;
     }
        }

        private async Task LoadAndSetPreviewAsync(ImageDownloadItem item)
      {
   try
     {
        var preview = await _previewLoader!.LoadPreviewImageFromColumnWidthAsync(item.Url, PreviewColumn.ActualWidth);
     if (preview != null)
            await Dispatcher.InvokeAsync(() => item.PreviewImage = preview);
 }
     catch (Exception ex)
       {
  Logger.Error($"Failed to load preview for {item.Url}", ex);
       }
  }

        private async void DownloadButton_Click(object sender, RoutedEventArgs e)
        {
   if (_imageItems.Count == 0)
       {
      ShowWarning("No images to download. Please scan a webpage first.", "No Images");
  return;
          }

      // Optimize: Use cached ready items to avoid repeated LINQ query
   await PerformDownloadAsync(GetReadyItems(), _imageItems.Count);
        }

   private async void DownloadSelectedButton_Click(object sender, RoutedEventArgs e)
        {
        // Optimize: Single enumeration with filter to avoid multiple passes
    var readyItems = ImageList.SelectedItems
     .Cast<ImageDownloadItem>()
    .Where(item => item.Status == Status.Ready)
      .ToList();
       
          if (readyItems.Count == 0)
       {
    var hasSelection = ImageList.SelectedItems.Count > 0;
            if (hasSelection)
       ShowInfo("No ready images selected. Selected images may already be downloaded.", "No Ready Images");
        else
             ShowWarning("No images selected. Please select images to download.", "No Selection");
  return;
     }

       await PerformDownloadAsync(readyItems, readyItems.Count);
        }

        private async Task PerformDownloadAsync(List<ImageDownloadItem> itemsToDownload, int totalCount)
     {
      _cancellationTokenSource = new CancellationTokenSource();
    _uiStateManager!.SetDownloadingState();

    try
       {
         int downloadedCount = await DownloadItemsAsync(itemsToDownload, totalCount, _cancellationTokenSource.Token);

        if (_cancellationTokenSource.Token.IsCancellationRequested)
           {
    _uiStateManager.SetCancelledState();
      _uiStateManager.UpdateStatus($"Cancelled. Downloaded {downloadedCount} images.");
       ShowInfo($"Operation cancelled. Downloaded {downloadedCount} images.", "Cancelled");
                }
     else
         {
  _uiStateManager.SetDownloadCompleteState();
      _uiStateManager.UpdateStatus($"Download complete! {downloadedCount} images saved to {_downloadFolder}");
   ShowInfo($"Successfully downloaded {downloadedCount} images!", "Success");
 }
         }
            catch (OperationCanceledException)
        {
     _uiStateManager.SetCancelledState();
              _uiStateManager.UpdateStatus("Download cancelled by user.");
            }
            catch (Exception ex)
{
     _uiStateManager.SetReadyState();
    ShowError($"Error: {ex.Message}");
  _uiStateManager.UpdateStatus("Error occurred during download.");
            }
        finally
            {
       _cancellationTokenSource?.Dispose();
           _cancellationTokenSource = null;
  ImageList_SelectionChanged(ImageList, new SelectionChangedEventArgs(Selector.SelectionChangedEvent, new List<object>(), new List<object>()));
         }
  }

        private async Task<int> DownloadItemsAsync(List<ImageDownloadItem> items, int total, CancellationToken cancellationToken)
        {
            if (!Directory.Exists(_downloadFolder))
   Directory.CreateDirectory(_downloadFolder);

   int downloadedCount = 0, skippedCount = 0, duplicateCount = 0;

   foreach (var item in items)
       {
  cancellationToken.ThrowIfCancellationRequested();
           await _imageDownloader!.DownloadSingleItemAsync(item, cancellationToken);

  if (item.Status == Status.Done) downloadedCount++;
       else if (item.Status.Contains(Status.Duplicate, StringComparison.Ordinal)) { skippedCount++; duplicateCount++; }
      else if (item.Status.Contains(Status.Skipped, StringComparison.Ordinal)) skippedCount++;

      int remaining = total - (downloadedCount + skippedCount);
       _uiStateManager!.UpdateDownloadProgress(downloadedCount, skippedCount, duplicateCount, remaining, total);
       // Remove artificial delay - downloads as fast as possible now
     }

      LoadCurrentPage();
    return downloadedCount;
  }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            _cancellationTokenSource?.Cancel();
       _uiStateManager!.SetCancellingState();
  }

        private void ImageList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
       if (ImageList.SelectedItem is ImageDownloadItem item)
       {
 if (item.Status == Status.Done || item.Status == Status.Backup || item.Status == Status.Duplicate)
        OpenDownloadedFile(item);
    else
     _ = DownloadSingleImageAsync(item);
 }
}

        private void OpenDownloadedFile(ImageDownloadItem item)
   {
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
          _uiStateManager!.UpdateStatus($"Opened: {item.FileName}");
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

     private void ImageList_SelectionChanged(object sender, SelectionChangedEventArgs e) => 
      _uiStateManager!.UpdateDownloadSelectedButtonState();

      private async Task DownloadSingleImageAsync(ImageDownloadItem item)
{
      if (item.Status == Status.Done || item.Status == Status.Duplicate)
      {
     ShowInfo($"Image already downloaded: {item.FileName}", "Already Downloaded");
     return;
            }

var originalStatus = item.Status;
        item.Status = Status.Downloading;

         try
    {
     await _imageDownloader!.DownloadSingleItemAsync(item, CancellationToken.None);
if (item.Status == Status.Done)
 _uiStateManager!.UpdateStatus($"Downloaded: {item.FileName}");
          }
       catch (Exception ex)
      {
      item.Status = Status.Failed;
      item.ErrorMessage = ex.Message;
 ShowError($"Failed to download: {ex.Message}", "Download Error");
  }
      }

        private async void ContextMenu_Download_Click(object sender, RoutedEventArgs e)
   {
       if (ImageList.SelectedItem is ImageDownloadItem item)
 await DownloadSingleImageWithForceAsync(item);
        }

     private async Task DownloadSingleImageWithForceAsync(ImageDownloadItem item)
{
        item.Status = Status.Downloading;

      try
 {
       await _imageDownloader!.DownloadSingleItemAsync(item, CancellationToken.None);
   _uiStateManager!.UpdateStatus($"Downloaded (forced): {item.FileName}");
            }
       catch (Exception ex)
 {
   item.Status = Status.Failed;
    item.ErrorMessage = ex.Message;
     ShowError($"Failed to download: {ex.Message}", "Download Error");
     }
}

  private void CancelSingleDownload(ImageDownloadItem item)
      {
            if (item.Status == Status.Downloading || item.Status == Status.Checking || item.Status == Status.FindingFullRes)
    {
      item.Status = Status.Cancelled;
   item.ErrorMessage = "Canceled by user";
    _uiStateManager!.UpdateStatus($"Canceled download: {item.FileName}");
      }
   else
{
  ShowInfo($"Cannot cancel - item is not currently downloading.\nCurrent status: {item.Status}", "Cannot Cancel");
            }
        }

        private void ApplyStatusFilter()
        {
   if (FilterReadyCheckBox == null || _filteredImageItems == null || _imageItems == null)
      return;

         ImageDownloadItem? firstVisibleItem = _currentPageItems.Count > 0 ? _currentPageItems[0] : null;
          _filteredImageItems.Clear();

    foreach (var item in _imageItems)
            {
       bool include = (FilterReadyCheckBox.IsChecked.GetValueOrDefault() && item.Status == Status.Ready) ||
    (FilterDoneCheckBox.IsChecked.GetValueOrDefault() && item.Status == Status.Done) ||
            (FilterBackupCheckBox.IsChecked.GetValueOrDefault() && item.Status == Status.Backup) ||
   (FilterDuplicateCheckBox.IsChecked.GetValueOrDefault() && item.Status == Status.Duplicate) ||
  (FilterFailedCheckBox.IsChecked.GetValueOrDefault() && item.Status == Status.Failed) ||
      (FilterSkippedCheckBox.IsChecked.GetValueOrDefault() && item.Status.Contains(Status.Skipped)) ||
          (FilterCancelledCheckBox.IsChecked.GetValueOrDefault() && item.Status == Status.Cancelled) ||
       (FilterDownloadingCheckBox.IsChecked.GetValueOrDefault() && 
(item.Status == Status.Downloading || item.Status == Status.Checking || item.Status == Status.FindingFullRes));

          if (include)
              _filteredImageItems.Add(item);
          }

            if (firstVisibleItem != null)
{
       int newIndex = _filteredImageItems.IndexOf(firstVisibleItem);
            if (newIndex >= 0)
        _currentPage = (newIndex / _itemsPerPage) + 1;
            }
            else
            {
   _currentPage = 1;
        }

         UpdatePagination();
        }

        protected override void OnClosing(CancelEventArgs e)
     {
            _cancellationTokenSource?.Cancel();
       base.OnClosing(e);
      }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
   {
  string initialDir = GetValidInitialDirectory(_downloadFolder);
            var dialog = new OpenFolderDialog { Title = "Select Download Folder", InitialDirectory = initialDir };

            if (dialog.ShowDialog() == true)
            {
 _downloadFolder = dialog.FolderName;
        FolderTextBox.Text = _downloadFolder;
            _settings.SaveToPortableSettings(_downloadFolder, UrlTextBox.Text);
        InitializeServices();
            }
        }

    private string GetValidInitialDirectory(string folder)
        {
        if (Directory.Exists(folder)) return folder;
            var parentDir = IOPath.GetDirectoryName(folder);
            if (!string.IsNullOrEmpty(parentDir) && Directory.Exists(parentDir)) return parentDir;
     return Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
        }

    private void UpFolderButton_Click(object sender, RoutedEventArgs e)
        {
       try
        {
      var parentDir = IOPath.GetDirectoryName(_downloadFolder);
       if (!string.IsNullOrEmpty(parentDir) && Directory.Exists(parentDir))
    {
       _downloadFolder = parentDir;
FolderTextBox.Text = _downloadFolder;
            _settings.SaveToPortableSettings(_downloadFolder, UrlTextBox.Text);
       _uiStateManager!.UpdateStatus($"Navigated up to: {_downloadFolder}");
   InitializeServices();
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

        private void NewFolderButton_Click(object sender, RoutedEventArgs e)
 {
       try
          {
  string parentDir = Directory.Exists(_downloadFolder) 
          ? _downloadFolder 
          : IOPath.GetDirectoryName(_downloadFolder) ?? Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);

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

    _downloadFolder = newFolderPath;
          FolderTextBox.Text = _downloadFolder;
     _settings.SaveToPortableSettings(_downloadFolder, UrlTextBox.Text);
             _uiStateManager!.UpdateStatus($"New folder created: {finalName}");
        InitializeServices();
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
     var inputDialog = new Window
    {
    Title = "Name New Folder",
    Width = 400,
       Height = 180,
           WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this,
              ResizeMode = ResizeMode.NoResize
            };

     var grid = new Grid { Margin = new Thickness(15) };
         grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
      grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var label = new TextBlock { Text = "Folder name:", Margin = new Thickness(0, 0, 0, 10), VerticalAlignment = VerticalAlignment.Top };
     Grid.SetRow(label, 0);

        var textBox = new TextBox { Text = defaultName, Height = 30, VerticalContentAlignment = VerticalAlignment.Center, Padding = new Thickness(5), Margin = new Thickness(0, 0, 0, 10) };
  textBox.SelectAll();
         Grid.SetRow(textBox, 1);
            Grid.SetRow(new FrameworkElement(), 2);

    var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };
            Grid.SetRow(buttonPanel, 3);

            var okButton = new Button { Content = "OK", Width = 80, Height = 30, Margin = new Thickness(0, 0, 10, 0), IsDefault = true };
      okButton.Click += (s, args) => { inputDialog.DialogResult = true; inputDialog.Close(); };

            var cancelButton = new Button { Content = "Cancel", Width = 80, Height = 30, IsCancel = true };
       cancelButton.Click += (s, args) => { inputDialog.DialogResult = false; inputDialog.Close(); };

      buttonPanel.Children.Add(okButton);
  buttonPanel.Children.Add(cancelButton);
 grid.Children.Add(label);
         grid.Children.Add(textBox);
            grid.Children.Add(buttonPanel);
         inputDialog.Content = grid;
 inputDialog.Loaded += (s, args) => textBox.Focus();

            if (inputDialog.ShowDialog() == true)
            {
     string finalName = textBox.Text.Trim();
       if (string.IsNullOrWhiteSpace(finalName))
            {
             ShowWarning("Folder name cannot be empty.", "Invalid Name");
  return null;
    }

           if (finalName.IndexOfAny(IOPath.GetInvalidFileNameChars()) >= 0)
      {
       ShowWarning("Folder name contains invalid characters.", "Invalid Name");
     return null;
                }

         return finalName;
    }

 return null;
        }

   private void OpenFolderButton_Click(object sender, RoutedEventArgs e)
        {
            try
      {
    if (!Directory.Exists(_downloadFolder))
         Directory.CreateDirectory(_downloadFolder);

     System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
    {
  FileName = _downloadFolder,
         UseShellExecute = true,
            Verb = "open"
  });

   _uiStateManager!.UpdateStatus($"Opened folder: {_downloadFolder}");
    }
   catch (Exception ex)
            {
      ShowError($"Failed to open folder: {ex.Message}", "Error Opening Folder");
   }
        }

  private void SettingsButton_Click(object sender, RoutedEventArgs e)
  {
            var settingsWindow = new SettingsWindow(_settings);
      if (settingsWindow.ShowDialog() == true)
  {
       _settings.SaveToPortableSettings(_downloadFolder, UrlTextBox.Text);
     if (_itemsPerPage != _settings.ItemsPerPage)
  {
         _itemsPerPage = _settings.ItemsPerPage;
     _currentPage = 1;
         UpdatePagination();
     }
           InitializeServices();
      _uiStateManager!.UpdateStatus("Settings saved.");
    }
     }

      private void UpdatePagination()
        {
_totalPages = (int)Math.Ceiling((double)_filteredImageItems.Count / _itemsPerPage);
            if (_totalPages == 0) _totalPages = 1;
      if (_currentPage > _totalPages) _currentPage = _totalPages;
      if (_currentPage < 1) _currentPage = 1;

 PageInfoText.Text = $"Page {_currentPage} of {_totalPages} ({_filteredImageItems.Count} filtered / {_imageItems.Count} total images)";
       PrevPageButton.IsEnabled = _currentPage > 1;
NextPageButton.IsEnabled = _currentPage < _totalPages;

         // Update the pagination context on the ListView to avoid repeated tree traversals in the converter
            UpdateListViewPaginationContext();

         LoadCurrentPage();
        }

        /// <summary>
        /// Updates the cached pagination context on the ListView for optimal index converter performance.
  /// </summary>
  private void UpdateListViewPaginationContext()
        {
            var paginationContext = new PaginationContext
 {
                CurrentPage = _currentPage,
  ItemsPerPage = _itemsPerPage
            };

            ListViewHelper.SetPaginationContext(ImageList, paginationContext);
        }

        private void LoadCurrentPage()
        {
    _currentPageItems.Clear();
         int startIndex = (_currentPage - 1) * _itemsPerPage;
  int endIndex = Math.Min(startIndex + _itemsPerPage, _filteredImageItems.Count);

         for (int i = startIndex; i < endIndex; i++)
                _currentPageItems.Add(_filteredImageItems[i]);
  }

     private void ImageItem_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
    if (e.PropertyName == nameof(ImageDownloadItem.Status))
                ApplyStatusFilter();
        }

        private void StatusFilter_Changed(object sender, RoutedEventArgs e) => ApplyStatusFilter();

        private void SelectAllStatus_Click(object sender, RoutedEventArgs e)
        {
            FilterReadyCheckBox.IsChecked = FilterDoneCheckBox.IsChecked = FilterBackupCheckBox.IsChecked = 
        FilterDuplicateCheckBox.IsChecked = FilterFailedCheckBox.IsChecked = FilterSkippedCheckBox.IsChecked = 
         FilterCancelledCheckBox.IsChecked = FilterDownloadingCheckBox.IsChecked = true;
     }

 private void ClearAllStatus_Click(object sender, RoutedEventArgs e)
        {
       FilterReadyCheckBox.IsChecked = FilterDoneCheckBox.IsChecked = FilterBackupCheckBox.IsChecked = 
           FilterDuplicateCheckBox.IsChecked = FilterFailedCheckBox.IsChecked = FilterSkippedCheckBox.IsChecked = 
    FilterCancelledCheckBox.IsChecked = FilterDownloadingCheckBox.IsChecked = false;
        }

 private async void ContextMenu_ReloadPreview_Click(object sender, RoutedEventArgs e)
        {
      if (ImageList.SelectedItem is ImageDownloadItem item)
  await ReloadSinglePreviewAsync(item);
        }

        private async Task ReloadSinglePreviewAsync(ImageDownloadItem item)
        {
            if (!_settings.LoadPreviews)
  {
      ShowInfo("Preview loading is disabled in Settings. Please enable 'Load preview images during scan' to use this feature.", "Preview Loading Disabled");
           return;
       }

   try
      {
     _uiStateManager!.UpdateStatus($"Reloading preview for {item.FileName}...");
 item.PreviewImage = null;

            var newPreview = await _previewLoader!.LoadPreviewImageFromColumnWidthAsync(item.Url, PreviewColumn.ActualWidth);
     if (newPreview != null)
    {
         item.PreviewImage = newPreview;
              _uiStateManager.UpdateStatus($"Preview reloaded for {item.FileName}");
    }
                else
       {
       _uiStateManager.UpdateStatus($"Failed to reload preview for {item.FileName}");
        ShowWarning($"Failed to reload preview for {item.FileName}. The image URL may be invalid or unavailable.", "Preview Load Failed");
  }
 }
     catch (Exception ex)
     {
      _uiStateManager!.UpdateStatus($"Error reloading preview: {ex.Message}");
     ShowError($"Error reloading preview: {ex.Message}", "Preview Error");
      }
        }

    private void ContextMenu_Cancel_Click(object sender, RoutedEventArgs e)
        {
   if (ImageList.SelectedItem is ImageDownloadItem item)
                CancelSingleDownload(item);
  }

        private void PrevPageButton_Click(object sender, RoutedEventArgs e)
        {
   if (_currentPage > 1)
{
        _currentPage--;
   UpdatePagination();
  }
        }

        private void NextPageButton_Click(object sender, RoutedEventArgs e)
        {
   if (_currentPage < _totalPages)
 {
    _currentPage++;
UpdatePagination();
            }
        }

  // Helper methods for MessageBox calls
        private void ShowInfo(string message, string title = "Info") => 
      MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);

        private void ShowWarning(string message, string title = "Warning") => 
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning);

        private void ShowError(string message, string title = "Error") => 
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
    }
}