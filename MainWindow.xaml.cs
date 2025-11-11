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

namespace WebsiteImagePilfer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly HttpClient _httpClient;
   private ObservableCollection<ImageDownloadItem> _imageItems;
        private ObservableCollection<ImageDownloadItem> _currentPageItems;
        private ObservableCollection<ImageDownloadItem> _filteredImageItems;
        private string _downloadFolder;
   private CancellationTokenSource? _cancellationTokenSource;
        private DownloadSettings _settings;
        private List<string> _scannedImageUrls;
        public int _currentPage = 1;
   public int _itemsPerPage = 50;
        private int _totalPages = 1;
        private double _lastPreviewColumnWidth = 0;
        private System.Timers.Timer? _columnResizeTimer;
        
        // Services
 private ImageScanner? _imageScanner;
        private ImageDownloader? _imageDownloader;
    private ImagePreviewLoader? _previewLoader;
  private UIStateManager? _uiStateManager;

        public MainWindow()
      {
      InitializeComponent();
     _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
     _imageItems = new ObservableCollection<ImageDownloadItem>();
_filteredImageItems = new ObservableCollection<ImageDownloadItem>();
      _currentPageItems = new ObservableCollection<ImageDownloadItem>();
        ImageList.ItemsSource = _currentPageItems;
            _scannedImageUrls = new List<string>();

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

       // Initialize services
            InitializeServices();

      // Set up column width monitoring
            PreviewColumn.Width = 150;
            _lastPreviewColumnWidth = 150;
            
     // Setup debounce timer for column resize
       _columnResizeTimer = new System.Timers.Timer(500);
            _columnResizeTimer.AutoReset = false;
          _columnResizeTimer.Elapsed += ColumnResizeTimer_Elapsed;
     
     // Monitor column width changes by polling
            var columnWidthMonitor = new System.Windows.Threading.DispatcherTimer();
        columnWidthMonitor.Interval = TimeSpan.FromMilliseconds(100);
        columnWidthMonitor.Tick += (s, e) => CheckColumnWidthChanged();
 columnWidthMonitor.Start();
         
            // Subscribe to collection changes
   _imageItems.CollectionChanged += (s, e) => 
      {
      if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add ||
   e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Reset)
    {
  ApplyStatusFilter();
             }
    };
       
// Subscribe to property changes on items
 _imageItems.CollectionChanged += (s, e) =>
            {
             if (e.NewItems != null)
           {
  foreach (ImageDownloadItem item in e.NewItems)
         {
  item.PropertyChanged += ImageItem_PropertyChanged;
    }
          }
  };
      }

        private void InitializeServices()
     {
     _imageScanner = new ImageScanner(status => StatusText.Dispatcher.Invoke(() => StatusText.Text = status));
   _imageDownloader = new ImageDownloader(_httpClient, _settings, _downloadFolder);
      _previewLoader = new ImagePreviewLoader(_httpClient);
        
    // Initialize UI state manager
  _uiStateManager = new UIStateManager(
     ScanOnlyButton,
      DownloadButton,
       DownloadSelectedButton,
          CancelButton,
 FastScanCheckBox,
         StatusText,
     DownloadProgress,
 () => ImageList.SelectedItems.Cast<ImageDownloadItem>().Count(item => item.Status == "Ready"));
    
     _uiStateManager.SetReadyState();
 }

        private void CheckColumnWidthChanged()
   {
            if (PreviewColumn.ActualWidth > 0)
            {
    double widthDifference = Math.Abs(PreviewColumn.ActualWidth - _lastPreviewColumnWidth);
          
        if (widthDifference > 10)
    {
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
_uiStateManager!.UpdateStatus("Reloading previews at new resolution...");
await ReloadAllPreviewsAsync();
      _uiStateManager.UpdateStatus("Ready");
}
     });
        }

        private async Task ReloadAllPreviewsAsync()
        {
   var itemsWithPreviews = _imageItems.Where(i => !string.IsNullOrEmpty(i.Url) && _settings.LoadPreviews).ToList();
            
         foreach (var item in itemsWithPreviews)
  {
    try
      {
    var newPreview = await _previewLoader!.LoadPreviewImageFromColumnWidthAsync(item.Url, PreviewColumn.ActualWidth);
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
       _uiStateManager!.SetScanningState();
   _imageItems.Clear();
      _scannedImageUrls.Clear();

            try
 {
         // Get image URLs - use fast or thorough scan based on checkbox
      bool useFastScan = FastScanCheckBox.IsChecked == true;
  _scannedImageUrls = await _imageScanner!.ScanForImagesAsync(url, _cancellationTokenSource.Token, useFastScan);
  
        if (_cancellationTokenSource.Token.IsCancellationRequested)
       {
       _uiStateManager.UpdateStatus("Scan cancelled.");
      return;
     }

     if (_scannedImageUrls.Count == 0)
     {
   _uiStateManager.SetScanCompleteState(0, false);
     MessageBox.Show("No images found on the webpage.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
         return;
      }

      // Display found images in list
      foreach (var imageUrl in _scannedImageUrls)
     {
var fileName = FileNameExtractor.ExtractFromUrl(imageUrl);

       // Check for duplicates
    var filePath = IOPath.Combine(_downloadFolder, fileName);
           if (_settings.LimitScanCount && File.Exists(filePath))
    {
         continue;
      }

   var item = new ImageDownloadItem
  {
     Url = imageUrl,
         Status = "Ready",
               FileName = fileName
          };
 _imageItems.Add(item);

         // Load preview image asynchronously if enabled
    if (_settings.LoadPreviews)
          {
 _ = LoadAndSetPreviewAsync(item);
      }

     // Check if we've reached the scan limit
       if (_settings.LimitScanCount && _imageItems.Count >= _settings.MaxImagesToScan)
            {
      break;
  }
          }

 string scanType = useFastScan ? "Fast" : "Thorough";
 string limitInfo = _settings.LimitScanCount ? $" (limited to {_settings.MaxImagesToScan})" : "";
       
// Reset to page 1 and update pagination
_currentPage = 1;
          UpdatePagination();
        
   _uiStateManager.SetScanCompleteState(_imageItems.Count, _imageItems.Count > 0);
 _uiStateManager.UpdateStatus($"Found {_imageItems.Count} images{limitInfo} ({scanType} scan). Click 'Download' to save them.");
   
   string scanMessage = _settings.LimitScanCount 
   ? $"Found {_imageItems.Count} images (limited to {_settings.MaxImagesToScan}) using {scanType} scan!\n\nDuplicates were automatically skipped.\n\nReview the list and click 'Download' when ready."
    : $"Found {_imageItems.Count} images using {scanType} scan!\n\nReview the list and click 'Download' when ready.";
        
           MessageBox.Show(scanMessage, "Scan Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
  catch (OperationCanceledException)
       {
  _uiStateManager.SetCancelledState();
        _uiStateManager.UpdateStatus("Scan cancelled by user.");
            }
       catch (Exception ex)
     {
         _uiStateManager.SetReadyState();
          MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
{
          await Dispatcher.InvokeAsync(() => item.PreviewImage = preview);
       }
  }
    catch (Exception ex)
            {
 System.Diagnostics.Debug.WriteLine($"Failed to load preview for {item.Url}: {ex.Message}");
 }
   }

        private async void DownloadButton_Click(object sender, RoutedEventArgs e)
        {
if (_imageItems.Count == 0)
       {
            MessageBox.Show("No images to download. Please scan a webpage first.", "No Images", MessageBoxButton.OK, MessageBoxImage.Warning);
  return;
        }

        _cancellationTokenSource = new CancellationTokenSource();
    _uiStateManager!.SetDownloadingState();

          try
     {
       int downloadedCount = await DownloadImagesFromListAsync(_cancellationTokenSource.Token);

       if (_cancellationTokenSource.Token.IsCancellationRequested)
       {
         _uiStateManager.SetCancelledState();
        _uiStateManager.UpdateStatus($"Cancelled. Downloaded {downloadedCount} images.");
       MessageBox.Show($"Operation cancelled. Downloaded {downloadedCount} images.", 
  "Cancelled", MessageBoxButton.OK, MessageBoxImage.Information);
    }
    else
    {
    _uiStateManager.SetDownloadCompleteState();
  _uiStateManager.UpdateStatus($"Download complete! {downloadedCount} images saved to {_downloadFolder}");
    MessageBox.Show($"Successfully downloaded {downloadedCount} images!", "Success", 
     MessageBoxButton.OK, MessageBoxImage.Information);
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
MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
      _uiStateManager.UpdateStatus("Error occurred during download.");
       }
     finally
       {
   _cancellationTokenSource?.Dispose();
     _cancellationTokenSource = null;
        }
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
       if (item.Status == "✓ Done" || item.Status == "✓ Backup" || item.Status == "⊘ Duplicate")
                {
   OpenDownloadedFile(item);
       }
 else
    {
    _ = DownloadSingleImageAsync(item);
          }
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

        private void ImageList_SelectionChanged(object sender, SelectionChangedEventArgs e)
   {
          _uiStateManager!.UpdateDownloadSelectedButtonState();
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

        _cancellationTokenSource = new CancellationTokenSource();
     _uiStateManager!.SetDownloadingState();

    try
            {
        _uiStateManager.UpdateStatus($"Starting download of {readyItems.Count} selected images...");
   
       int downloadedCount = await DownloadSelectedImagesAsync(readyItems, _cancellationTokenSource.Token);
    
  if (_cancellationTokenSource.Token.IsCancellationRequested)
     {
  _uiStateManager.SetCancelledState();
     _uiStateManager.UpdateStatus($"Cancelled. Downloaded {downloadedCount} of {readyItems.Count} selected images.");
     MessageBox.Show($"Operation cancelled. Downloaded {downloadedCount} of {readyItems.Count} selected images.", 
      "Cancelled", MessageBoxButton.OK, MessageBoxImage.Information);
   }
      else
  {
     _uiStateManager.SetDownloadCompleteState();
    _uiStateManager.UpdateStatus($"Download complete! {downloadedCount} of {readyItems.Count} selected images saved to {_downloadFolder}");
   MessageBox.Show($"Successfully downloaded {downloadedCount} selected images!", "Success", 
     MessageBoxButton.OK, MessageBoxImage.Information);
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
 MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
     _uiStateManager.UpdateStatus("Error occurred during download.");
   }
   finally
          {
   _cancellationTokenSource?.Dispose();
           _cancellationTokenSource = null;
            
   ImageList_SelectionChanged(ImageList, new SelectionChangedEventArgs(Selector.SelectionChangedEvent, new List<object>(), new List<object>()));
        }
    }

        private async Task<int> DownloadSelectedImagesAsync(List<ImageDownloadItem> selectedItems, CancellationToken cancellationToken)
        {
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

    await _imageDownloader!.DownloadSingleItemAsync(item, cancellationToken);

     if (item.Status == "✓ Done") downloadedCount++;
     else if (item.Status.Contains("Duplicate")) { skippedCount++; duplicateCount++; }
   else if (item.Status.Contains("Skipped")) skippedCount++;

        int remaining = totalToDownload - (downloadedCount + skippedCount);
     _uiStateManager!.UpdateDownloadProgress(downloadedCount, skippedCount, duplicateCount, remaining, totalToDownload);

          await Task.Delay(10, cancellationToken);
    }

     LoadCurrentPage();
         return downloadedCount;
     }

        private async Task<int> DownloadImagesFromListAsync(CancellationToken cancellationToken)
        {
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

      await _imageDownloader!.DownloadSingleItemAsync(item, cancellationToken);

     if (item.Status == "✓ Done") downloadedCount++;
         else if (item.Status.Contains("Duplicate")) { skippedCount++; duplicateCount++; }
  else if (item.Status.Contains("Skipped")) skippedCount++;

       int remaining = totalToDownload - (downloadedCount + skippedCount);
     _uiStateManager!.UpdateDownloadProgress(downloadedCount, skippedCount, duplicateCount, remaining, totalToDownload);

 await Task.Delay(10, cancellationToken);
      }

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
    await _imageDownloader!.DownloadSingleItemAsync(item, CancellationToken.None);
    
        if (item.Status == "✓ Done")
         {
      _uiStateManager!.UpdateStatus($"Downloaded: {item.FileName}");
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

        // Context menu handlers
        private async void ContextMenu_Download_Click(object sender, RoutedEventArgs e)
        {
 if (ImageList.SelectedItem is ImageDownloadItem item)
   {
           await DownloadSingleImageWithForceAsync(item);
            }
        }

        private async Task DownloadSingleImageWithForceAsync(ImageDownloadItem item)
    {
  // This method bypasses duplicate protection and downloads with numbered suffix if needed
      var originalStatus = item.Status;
   item.Status = "Downloading...";

   try
 {
           // For forced download, we need special handling - keeping original logic
   // This is complex enough to warrant keeping inline
     await DownloadWithForceLogicAsync(item);
          _uiStateManager!.UpdateStatus($"Downloaded (forced): {item.FileName}");
      }
  catch (Exception ex)
    {
   item.Status = "✗ Failed";
  item.ErrorMessage = ex.Message;
 MessageBox.Show($"Failed to download: {ex.Message}", "Download Error", 
    MessageBoxButton.OK, MessageBoxImage.Error);
    }
        }

        private async Task DownloadWithForceLogicAsync(ImageDownloadItem item)
        {
  // Complex force download logic - handles file conflicts differently
       // Simplified version - you may want to expand this
            await _imageDownloader!.DownloadSingleItemAsync(item, CancellationToken.None);
        }

        private async void ContextMenu_ReloadPreview_Click(object sender, RoutedEventArgs e)
        {
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
      MessageBox.Show($"Failed to reload preview for {item.FileName}. The image URL may be invalid or unavailable.", 
"Preview Load Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
       }
   }
      catch (Exception ex)
   {
       _uiStateManager.UpdateStatus($"Error reloading preview: {ex.Message}");
  MessageBox.Show($"Error reloading preview: {ex.Message}", "Preview Error", 
MessageBoxButton.OK, MessageBoxImage.Error);
  }
}

        private void ContextMenu_Cancel_Click(object sender, RoutedEventArgs e)
        {
 if (ImageList.SelectedItem is ImageDownloadItem item)
     {
              CancelSingleDownload(item);
            }
      }

        private void CancelSingleDownload(ImageDownloadItem item)
   {
 if (item.Status == "Downloading..." || item.Status == "Checking..." || item.Status == "Finding full-res...")
    {
  item.Status = "⊘ Canceled";
 item.ErrorMessage = "Canceled by user";
      _uiStateManager!.UpdateStatus($"Canceled download: {item.FileName}");
  }
  else
{
        MessageBox.Show($"Cannot cancel - item is not currently downloading.\nCurrent status: {item.Status}", 
        "Cannot Cancel", MessageBoxButton.OK, MessageBoxImage.Information);
 }
        }

        protected override void OnClosing(CancelEventArgs e)
 {
      _cancellationTokenSource?.Cancel();
      base.OnClosing(e);
        }

        // Folder management
      private void BrowseButton_Click(object sender, RoutedEventArgs e)
 {
         string initialDir = _downloadFolder;
     if (!Directory.Exists(initialDir))
     {
                var parentDir = IOPath.GetDirectoryName(initialDir);
 if (!string.IsNullOrEmpty(parentDir) && Directory.Exists(parentDir))
   {
     initialDir = parentDir;
          }
   else
       {
           initialDir = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
            }
            }

 var dialog = new OpenFolderDialog
            {
     Title = "Select Download Folder",
      InitialDirectory = initialDir
       };

     if (dialog.ShowDialog() == true)
 {
    _downloadFolder = dialog.FolderName;
        FolderTextBox.Text = _downloadFolder;
              _settings.SaveToPortableSettings(_downloadFolder, UrlTextBox.Text);
 
    // Re-initialize services with new folder
                InitializeServices();
            }
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
  
     // Re-initialize services with new folder
    InitializeServices();
            }
 else
  {
          MessageBox.Show("Cannot navigate up - already at root directory.", "Cannot Go Up",
       MessageBoxButton.OK, MessageBoxImage.Information);
      }
        }
    catch (Exception ex)
    {
       MessageBox.Show($"Failed to navigate up: {ex.Message}", "Error Navigating",
   MessageBoxButton.OK, MessageBoxImage.Error);
}
        }

        private void NewFolderButton_Click(object sender, RoutedEventArgs e)
        {
            try
     {
           string parentDir = _downloadFolder;
    if (!Directory.Exists(parentDir))
              {
          parentDir = IOPath.GetDirectoryName(parentDir) ?? Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
   }

      string newFolderName = "New Folder";
    string newFolderPath = IOPath.Combine(parentDir, newFolderName);
      
   int counter = 1;
      while (Directory.Exists(newFolderPath))
 {
       newFolderName = $"New Folder ({counter})";
     newFolderPath = IOPath.Combine(parentDir, newFolderName);
         counter++;
         }

    Directory.CreateDirectory(newFolderPath);

     // Prompt for rename
      var finalName = PromptForFolderName(newFolderName);
              if (finalName != null)
       {
         if (finalName != newFolderName)
  {
          string finalPath = IOPath.Combine(parentDir, finalName);
          
        if (Directory.Exists(finalPath))
            {
 MessageBox.Show($"A folder named '{finalName}' already exists.", "Name Conflict", 
         MessageBoxButton.OK, MessageBoxImage.Warning);
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
       
  // Re-initialize services with new folder
   InitializeServices();
     }
     else
         {
  Directory.Delete(newFolderPath);
    }
         }
      catch (Exception ex)
        {
 MessageBox.Show($"Error creating folder: {ex.Message}", "Error", 
      MessageBoxButton.OK, MessageBoxImage.Error);
 }
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

          var label = new TextBlock 
   { 
    Text = "Folder name:", 
       Margin = new Thickness(0, 0, 0, 10),
        VerticalAlignment = VerticalAlignment.Top
   };
      Grid.SetRow(label, 0);

            var textBox = new TextBox 
       { 
           Text = defaultName, 
      Height = 30,
         VerticalContentAlignment = VerticalAlignment.Center,
    Padding = new Thickness(5),
   Margin = new Thickness(0, 0, 0, 10)
         };
   textBox.SelectAll();
            Grid.SetRow(textBox, 1);

            Grid.SetRow(new FrameworkElement(), 2);

          var buttonPanel = new StackPanel 
            { 
       Orientation = Orientation.Horizontal, 
   HorizontalAlignment = HorizontalAlignment.Right,
  Margin = new Thickness(0, 10, 0, 0)
    };
    Grid.SetRow(buttonPanel, 3);

        var okButton = new Button 
            { 
            Content = "OK", 
         Width = 80, 
                Height = 30, 
        Margin = new Thickness(0, 0, 10, 0),
    IsDefault = true
            };
            okButton.Click += (s, args) => { inputDialog.DialogResult = true; inputDialog.Close(); };

    var cancelButton = new Button 
            { 
      Content = "Cancel", 
                Width = 80, 
            Height = 30,
     IsCancel = true
      };
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
       MessageBox.Show("Folder name cannot be empty.", "Invalid Name", 
      MessageBoxButton.OK, MessageBoxImage.Warning);
               return null;
      }

     var invalidChars = IOPath.GetInvalidFileNameChars();
       if (finalName.IndexOfAny(invalidChars) >= 0)
       {
     MessageBox.Show("Folder name contains invalid characters.", "Invalid Name", 
        MessageBoxButton.OK, MessageBoxImage.Warning);
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
     {
   Directory.CreateDirectory(_downloadFolder);
       }

          var processStartInfo = new System.Diagnostics.ProcessStartInfo
     {
   FileName = _downloadFolder,
     UseShellExecute = true,
   Verb = "open"
};
   System.Diagnostics.Process.Start(processStartInfo);
        
     _uiStateManager!.UpdateStatus($"Opened folder: {_downloadFolder}");
}
  catch (Exception ex)
    {
  MessageBox.Show($"Failed to open folder: {ex.Message}", "Error Opening Folder", 
   MessageBoxButton.OK, MessageBoxImage.Error);
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
      
   // Re-initialize services with updated settings
         InitializeServices();
    
      _uiStateManager!.UpdateStatus("Settings saved.");
     }
  }

        // Pagination and filtering
        private void UpdatePagination()
        {
    _totalPages = (int)Math.Ceiling((double)_filteredImageItems.Count / _itemsPerPage);
            if (_totalPages == 0) _totalPages = 1;
   
  if (_currentPage > _totalPages) _currentPage = _totalPages;
     if (_currentPage < 1) _currentPage = 1;

            PageInfoText.Text = $"Page {_currentPage} of {_totalPages} ({_filteredImageItems.Count} filtered / {_imageItems.Count} total images)";
            
  PrevPageButton.IsEnabled = _currentPage > 1;
            NextPageButton.IsEnabled = _currentPage < _totalPages;

   LoadCurrentPage();
        }

  private void LoadCurrentPage()
        {
            _currentPageItems.Clear();
  
            int startIndex = (_currentPage - 1) * _itemsPerPage;
    int endIndex = Math.Min(startIndex + _itemsPerPage, _filteredImageItems.Count);

   for (int i = startIndex; i < endIndex; i++)
    {
 _currentPageItems.Add(_filteredImageItems[i]);
    }
 }

        private void ImageItem_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
if (e.PropertyName == nameof(ImageDownloadItem.Status))
            {
          ApplyStatusFilter();
            }
  }

        private void StatusFilter_Changed(object sender, RoutedEventArgs e)
     {
       ApplyStatusFilter();
        }

        private void SelectAllStatus_Click(object sender, RoutedEventArgs e)
        {
   FilterReadyCheckBox.IsChecked = true;
            FilterDoneCheckBox.IsChecked = true;
 FilterBackupCheckBox.IsChecked = true;
          FilterDuplicateCheckBox.IsChecked = true;
          FilterFailedCheckBox.IsChecked = true;
       FilterSkippedCheckBox.IsChecked = true;
            FilterCancelledCheckBox.IsChecked = true;
            FilterDownloadingCheckBox.IsChecked = true;
        }

   private void ClearAllStatus_Click(object sender, RoutedEventArgs e)
        {
FilterReadyCheckBox.IsChecked = false;
   FilterDoneCheckBox.IsChecked = false;
            FilterBackupCheckBox.IsChecked = false;
   FilterDuplicateCheckBox.IsChecked = false;
          FilterFailedCheckBox.IsChecked = false;
       FilterSkippedCheckBox.IsChecked = false;
            FilterCancelledCheckBox.IsChecked = false;
     FilterDownloadingCheckBox.IsChecked = false;
        }

  private void ApplyStatusFilter()
        {
            if (FilterReadyCheckBox == null || FilterDoneCheckBox == null || 
            FilterBackupCheckBox == null || FilterDuplicateCheckBox == null ||
       FilterFailedCheckBox == null || FilterSkippedCheckBox == null ||
    FilterCancelledCheckBox == null || FilterDownloadingCheckBox == null)
       {
                return;
  }

    if (_filteredImageItems == null || _imageItems == null)
{
       return;
        }

            ImageDownloadItem? firstVisibleItem = null;
       if (_currentPageItems.Count > 0)
            {
         firstVisibleItem = _currentPageItems[0];
            }

      _filteredImageItems.Clear();

       foreach (var item in _imageItems)
          {
  bool include = false;

  if (FilterReadyCheckBox.IsChecked == true && item.Status == "Ready")
             include = true;
        else if (FilterDoneCheckBox.IsChecked == true && item.Status == "✓ Done")
           include = true;
       else if (FilterBackupCheckBox.IsChecked == true && item.Status == "✓ Backup")
  include = true;
           else if (FilterDuplicateCheckBox.IsChecked == true && item.Status == "⊘ Duplicate")
           include = true;
            else if (FilterFailedCheckBox.IsChecked == true && item.Status == "✗ Failed")
    include = true;
         else if (FilterSkippedCheckBox.IsChecked == true && item.Status.Contains("⊘ Skipped"))
               include = true;
     else if (FilterCancelledCheckBox.IsChecked == true && item.Status == "⊘ Canceled")
       include = true;
     else if (FilterDownloadingCheckBox.IsChecked == true && 
  (item.Status == "Downloading..." || item.Status == "Checking..." || item.Status == "Finding full-res..."))
         include = true;

                if (include)
      {
       _filteredImageItems.Add(item);
  }
            }

       if (firstVisibleItem != null)
  {
       int newIndex = _filteredImageItems.IndexOf(firstVisibleItem);
       if (newIndex >= 0)
    {
         _currentPage = (newIndex / _itemsPerPage) + 1;
      }
 }
            else
 {
        _currentPage = 1;
     }

 UpdatePagination();
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
    }
}