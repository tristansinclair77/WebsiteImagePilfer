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
  
     // Load settings
   _settings = new DownloadSettings();
        
  // Apply items per page from settings
      _itemsPerPage = _settings.ItemsPerPage;

   // Set default download folder from settings or use default
    _downloadFolder = string.IsNullOrEmpty(Properties.Settings.Default.DownloadFolder)
  ? IOPath.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "WebsiteImages")
 : Properties.Settings.Default.DownloadFolder;
  FolderTextBox.Text = _downloadFolder;
    
// Load last URL if available
  if (!string.IsNullOrEmpty(Properties.Settings.Default.LastUrl))
       {
  UrlTextBox.Text = Properties.Settings.Default.LastUrl;
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

   // Save last URL
        Properties.Settings.Default.LastUrl = url;
   Properties.Settings.Default.Save();

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

      // Load preview image asynchronously if enabled
         if (_settings.LoadPreviews)
        {
 // Start loading preview immediately (fire and forget)
        _ = LoadAndSetPreviewAsync(item);
        }

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
   try
          {
   var preview = await LoadPreviewImageAsync(item.Url);
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
        // Found a DIFFERENT full-res URL (transformation succeeded)
 urlToDownload = fullResUrl;
  usedBackup = false;
  }
           else if (fullResUrl == item.Url)
   {
        // URL is already full-res (no transformation needed)
   usedBackup = false;
           }
   else
  {
    // No full-res found, using original preview URL as backup
    usedBackup = true;
  }
 }
catch (Exception ex)
  {
   // LOG: Full-res check error
     StatusText.Dispatcher.Invoke(() => 
  StatusText.Text = $"Full-res check failed: {ex.Message}");
    usedBackup = true; // Error finding full-res, using backup
   }
 }
    else
    {
   // Skip check is enabled, using whatever URL we scanned
          usedBackup = false;
      }

 // USE THE FILENAME WE ALREADY HAVE from the scan!
          // Don't re-extract it from the URL
   var fileName = item.FileName;
     
         // Only generate a new filename if we don't have one yet
       if (string.IsNullOrEmpty(fileName) || fileName.StartsWith("image_"))
   {
       // Extract from download URL as fallback
  var uri = new Uri(urlToDownload);
            
    // Check for query parameter first
  if (uri.Query.Contains("?f=") || uri.Query.Contains("&f="))
      {
     var queryParams = System.Web.HttpUtility.ParseQueryString(uri.Query);
  var fParam = queryParams["f"];
    if (!string.IsNullOrEmpty(fParam))
   {
            fileName = fParam;
            }
        }
        
    // Fallback to path filename
 if (string.IsNullOrEmpty(fileName))
 {
            fileName = IOPath.GetFileName(uri.LocalPath);
  }
  
  // Final fallback
      if (string.IsNullOrEmpty(fileName) || !IOPath.HasExtension(fileName))
    {
fileName = $"image_{DateTime.Now:yyyyMMddHHmmss}_{Guid.NewGuid().ToString().Substring(0, 8)}.jpg";
     }
   
  fileName = SanitizeFileName(fileName);
  item.FileName = fileName;
  }

    // Check file type filters before downloading
      var extension = IOPath.GetExtension(fileName).ToLowerInvariant();
  if (_settings.FilterJpgOnly && extension != ".jpg" && extension != ".jpeg")
 {
 item.Status = "⊘ Skipped (not JPG)";
        item.FileName = $"{fileName} (Filtered: {extension})";
return;
   }
       if (_settings.FilterPngOnly && extension != ".png")
  {
 item.Status = "⊘ Skipped (not PNG)";
        item.FileName = $"{fileName} (Filtered: {extension})";
      return;
     }

      // Check if file already exists - using the CORRECT filename we got from scan
  var filePath = IOPath.Combine(_downloadFolder, fileName);
     if (File.Exists(filePath))
 {
item.Status = "⊘ Duplicate";
  return;
       }

     item.Status = "Downloading...";
      var downloadStart = DateTime.Now;

  // Download the image
     var imageBytes = await _httpClient.GetByteArrayAsync(urlToDownload, cancellationToken);
    var downloadElapsed = (DateTime.Now - downloadStart).TotalMilliseconds;

     // Filter by size if setting is enabled
 if (_settings.FilterBySize && imageBytes.Length < _settings.MinimumImageSize)
   {
         item.Status = "⊘ Skipped (too small)";
 item.FileName = $"{fileName} ({imageBytes.Length} bytes)";
         return;
  }

    await File.WriteAllBytesAsync(filePath, imageBytes, cancellationToken);

    // Create thumbnail for preview
 if (_settings.ShowThumbnails)
        {
try
  {
   item.ThumbnailPath = filePath;
 }
catch
 {
     // Thumbnail generation failed, continue without it
       }
}

   if (usedBackup)
  {
  item.Status = "✓ Backup"; // Used preview/original URL because full-res wasn't found
 }
 else
 {
    item.Status = "✓ Done"; // Successfully downloaded (full-res if available)
 }

    var totalElapsed = (DateTime.Now - startTime).TotalMilliseconds;
      // LOG: Total time
     System.Diagnostics.Debug.WriteLine($"Downloaded {fileName} in {totalElapsed}ms (download: {downloadElapsed}ms)");
  }
       catch (OperationCanceledException)
     {
       item.Status = "⊘ Cancelled";
   throw;
 }
     catch (HttpRequestException ex)
      {
       item.Status = "✗ Failed";
   item.ErrorMessage = $"Network error: {ex.Message}";
      System.Diagnostics.Debug.WriteLine($"HTTP error for {item.FileName}: {ex.Message}");
  }
    catch (Exception ex)
  {
item.Status = "✗ Failed";
item.ErrorMessage = ex.Message;
System.Diagnostics.Debug.WriteLine($"Error for {item.FileName}: {ex.Message}");
  }
      }

        private async Task<List<string>> GetImageUrlsWithSeleniumAsync(string url, CancellationToken cancellationToken, bool useFastScan = false)
     {
 var imageUrls = new HashSet<string>();
  IWebDriver? driver = null;

await Task.Run(() =>
    {
            try
      {
   StatusText.Dispatcher.Invoke(() => StatusText.Text = "Launching browser...");

       // Setup Chrome options
      var options = new ChromeOptions();
      options.AddArgument("--headless=new");
      options.AddArgument("--disable-gpu");
     options.AddArgument("--no-sandbox");
        options.AddArgument("--disable-dev-shm-usage");
      options.AddArgument("--window-size=1920,1080");
    options.AddArgument("user-agent=Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
 options.AddArgument("--disable-blink-features=AutomationControlled");
        options.PageLoadStrategy = PageLoadStrategy.Normal;

   driver = new ChromeDriver(options);
      driver.Manage().Timeouts().PageLoad = TimeSpan.FromSeconds(60);
    driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(10);

        StatusText.Dispatcher.Invoke(() => StatusText.Text = "Loading page with JavaScript...");

 // Navigate to URL with retry logic
 int retryCount = 0;
       bool pageLoaded = false;
  
  while (!pageLoaded && retryCount < 3)
{
      try
    {
     driver.Navigate().GoToUrl(url);
        pageLoaded = true;
             }
     catch (WebDriverTimeoutException)
{
    retryCount++;
    if (retryCount >= 3) throw;
  StatusText.Dispatcher.Invoke(() => StatusText.Text = $"Page load timeout, retrying ({retryCount}/3)...");
System.Threading.Thread.Sleep(2000);
     }
}

  if (useFastScan)
  {
   // FAST SCAN: Original behavior - single wait
      StatusText.Dispatcher.Invoke(() => StatusText.Text = "Fast scan: Waiting for images to load...");
        System.Threading.Thread.Sleep(5000); // Wait 5 seconds

    // Try to scroll down to trigger lazy loading
      try
 {
        var js = (IJavaScriptExecutor)driver;
    js.ExecuteScript("window.scrollTo(0, document.body.scrollHeight);");
   System.Threading.Thread.Sleep(2000);
            }
   catch { /* Scroll failed, continue anyway */ }
     }
else
        {
            // THOROUGH SCAN: Dynamic waiting for lazy-loaded content
      StatusText.Dispatcher.Invoke(() => StatusText.Text = "Thorough scan: Waiting for images to load...");
    
       // DYNAMIC WAITING: Keep checking for new images
     int previousCount = 0;
int stableCount = 0;
  int maxStableChecks = 3; // Stop after 3 checks with no new images
  int checkInterval = 5000; // Check every 5 seconds

   while (stableCount < maxStableChecks && !cancellationToken.IsCancellationRequested)
        {
     // Scroll to trigger lazy loading
      try
             {
  var js = (IJavaScriptExecutor)driver;
  js.ExecuteScript("window.scrollTo(0, document.body.scrollHeight);");
  System.Threading.Thread.Sleep(1000);
          js.ExecuteScript("window.scrollTo(0, document.body.scrollHeight / 2);");
 System.Threading.Thread.Sleep(1000);
      js.ExecuteScript("window.scrollTo(0, 0);");
 }
  catch { /* Scroll failed, continue anyway */ }

        // Wait for images to load
   System.Threading.Thread.Sleep(checkInterval);

    // Extract current images
    var currentImages = new HashSet<string>();
       
try
    {
           var imgElements = driver.FindElements(By.TagName("img"));
        foreach (var img in imgElements)
 {
  if (cancellationToken.IsCancellationRequested) break;

 try
          {
    var src = img.GetAttribute("src");
    if (!string.IsNullOrEmpty(src) && !src.StartsWith("data:"))
         {
  currentImages.Add(src);
     }
        }
    catch { /* Skip invalid elements */ }
}
   }
      catch { /* Continue if extraction fails */ }

     // Check if we found new images
       int currentCount = currentImages.Count;
        if (currentCount > previousCount)
  {
         stableCount = 0; // Reset counter - we found new images!
       StatusText.Dispatcher.Invoke(() => 
     StatusText.Text = $"Thorough scan: Found {currentCount} images so far, continuing...");
  previousCount = currentCount;
    imageUrls.UnionWith(currentImages);
       }
    else
   {
           stableCount++; // No new images found
  StatusText.Dispatcher.Invoke(() => 
      StatusText.Text = $"Thorough scan: Stable at {currentCount} images ({stableCount}/{maxStableChecks} checks)...");
}
  }
    }

      StatusText.Dispatcher.Invoke(() => StatusText.Text = "Extracting final image URLs...");

     // Final comprehensive extraction
   var renderedHtml = driver.PageSource;
        var debugPath = IOPath.Combine(_downloadFolder, "debug_rendered_page.html");
 File.WriteAllText(debugPath, renderedHtml);

var htmlDoc = new HtmlDocument();
htmlDoc.LoadHtml(renderedHtml);
  var baseUri = new Uri(url);

 // Parse rendered HTML with all methods
    var imgNodes = htmlDoc.DocumentNode.SelectNodes("//img");
   if (imgNodes != null)
    {
     foreach (var img in imgNodes)
  {
  if (cancellationToken.IsCancellationRequested) break;

            // PRIORITY: Check if image is wrapped in a link to full-resolution
      var parentLink = img.ParentNode;
    string? fullResUrl = null;
 
            if (parentLink != null && parentLink.Name == "a")
  {
         var href = parentLink.GetAttributeValue("href", "");
     if (!string.IsNullOrEmpty(href))
        {
      var lower = href.ToLowerInvariant();
 if (lower.Contains(".jpg") || lower.Contains(".jpeg") || 
       lower.Contains(".png") || lower.Contains(".gif") || 
        lower.Contains(".webp") || lower.Contains(".bmp"))
 {
   if (Uri.TryCreate(baseUri, href, out Uri? absoluteUri))
  {
fullResUrl = absoluteUri.ToString();
           }
     }
  }
       }

          // If we found a full-res link, use that instead of img src
   if (fullResUrl != null)
      {
 imageUrls.Add(fullResUrl);
     continue; // Skip checking img attributes
            }

    // Otherwise check img attributes
  string[] possibleAttributes = { "src", "data-src", "data-lazy-src", "data-original", "data-file" };
  foreach (var attr in possibleAttributes)
       {
    var src = img.GetAttributeValue(attr, "");
    if (!string.IsNullOrEmpty(src) && !src.StartsWith("data:"))
 {
     if (Uri.TryCreate(baseUri, src, out Uri? absoluteUri))
   {
      imageUrls.Add(absoluteUri.ToString());
      break; // Only add one URL per image
   }
  }
 }
    }
    }

   // Find direct image links
        try
 {
    var linkElements = driver.FindElements(By.TagName("a"));
   foreach (var link in linkElements)
 {
       if (cancellationToken.IsCancellationRequested) break;

    try
  {
     var href = link.GetAttribute("href");
    if (!string.IsNullOrEmpty(href))
  {
 var lower = href.ToLowerInvariant();
         if (lower.EndsWith(".jpg") || lower.EndsWith(".jpeg") || 
  lower.EndsWith(".png") || lower.EndsWith(".gif") || 
  lower.EndsWith(".webp"))
{
 if (Uri.TryCreate(baseUri, href, out Uri? absoluteUri))
    {
  imageUrls.Add(absoluteUri.ToString());
   }
  }
       }
      }
   catch { /* Skip invalid elements */ }
  }
         }
      catch { /* Continue if Selenium fails */ }

   // Regex scan of rendered HTML
    var imageUrlPattern = @"https?://[^\s""'<>\\]+?\.(?:jpg|jpeg|png|gif|webp|bmp)(?:\?[^\s""'<>\\]*)?";
var matches = Regex.Matches(renderedHtml, imageUrlPattern, RegexOptions.IgnoreCase);

       foreach (Match match in matches)
  {
  if (cancellationToken.IsCancellationRequested) break;

      var imageUrl = match.Value;
if (Uri.TryCreate(imageUrl, UriKind.Absolute, out Uri? uri))
  {
 imageUrls.Add(uri.ToString());
 }
  }
        }
  catch (Exception ex)
   {
       StatusText.Dispatcher.Invoke(() => 
 StatusText.Text = $"Browser error: {ex.Message}");
 }
  finally
 {
           try { driver?.Quit(); } catch { }
   try { driver?.Dispose(); } catch { }
     }
    }, cancellationToken);

return imageUrls.ToList();
        }

        protected override void OnClosing(CancelEventArgs e)
      {
   // Cancel any ongoing operations
         _cancellationTokenSource?.Cancel();
    
 base.OnClosing(e);
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            // Ensure the initial directory exists, otherwise use parent or user folder
       string initialDir = _downloadFolder;
          if (!Directory.Exists(initialDir))
    {
// Try to get parent directory
       var parentDir = IOPath.GetDirectoryName(initialDir);
         if (!string.IsNullOrEmpty(parentDir) && Directory.Exists(parentDir))
          {
     initialDir = parentDir;
          }
        else
          {
// Fallback to My Pictures
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
   
   // Save to settings
        Properties.Settings.Default.DownloadFolder = _downloadFolder;
     Properties.Settings.Default.Save();
     }
        }

   private void NewFolderButton_Click(object sender, RoutedEventArgs e)
   {
            try
    {
// Get the parent directory to create the new folder in
   string parentDir = _downloadFolder;
    if (!Directory.Exists(parentDir))
  {
        parentDir = IOPath.GetDirectoryName(parentDir) ?? Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
     }

           // Create a new folder with default name
     string newFolderName = "New Folder";
      string newFolderPath = IOPath.Combine(parentDir, newFolderName);
                
    // Handle duplicates
     int counter = 1;
     while (Directory.Exists(newFolderPath))
{
  newFolderName = $"New Folder ({counter})";
  newFolderPath = IOPath.Combine(parentDir, newFolderName);
       counter++;
        }

        // Create the folder
   Directory.CreateDirectory(newFolderPath);

 // Prompt user to rename
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
    Text = newFolderName, 
     Height = 30,
        VerticalContentAlignment = VerticalAlignment.Center,
 Padding = new Thickness(5),
   Margin = new Thickness(0, 0, 0, 10)
    };
textBox.SelectAll();
   Grid.SetRow(textBox, 1);

            // Add spacing row
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
    
     // Validate folder name
    if (string.IsNullOrWhiteSpace(finalName))
 {
  MessageBox.Show("Folder name cannot be empty.", "Invalid Name", 
       MessageBoxButton.OK, MessageBoxImage.Warning);
             Directory.Delete(newFolderPath);
        return;
     }

     // Check for invalid characters
     var invalidChars = IOPath.GetInvalidFileNameChars();
  if (finalName.IndexOfAny(invalidChars) >= 0)
 {
         MessageBox.Show("Folder name contains invalid characters.", "Invalid Name", 
     MessageBoxButton.OK, MessageBoxImage.Warning);
    Directory.Delete(newFolderPath);
     return;
        }

       // Rename folder if name changed
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

  // Set as download folder
   _downloadFolder = newFolderPath;
         FolderTextBox.Text = _downloadFolder;

  // Save to settings
  Properties.Settings.Default.DownloadFolder = _downloadFolder;
    Properties.Settings.Default.Save();

       StatusText.Text = $"New folder created: {finalName}";
         }
  else
          {
   // User cancelled - delete the temporary folder
Directory.Delete(newFolderPath);
   }
  }
       catch (Exception ex)
       {
 MessageBox.Show($"Error creating folder: {ex.Message}", "Error", 
      MessageBoxButton.OK, MessageBoxImage.Error);
        }
        }

        private void OpenFolderButton_Click(object sender, RoutedEventArgs e)
        {
        try
   {
   // Ensure the folder exists before trying to open it
           if (!Directory.Exists(_downloadFolder))
        {
   // Try to create it
    Directory.CreateDirectory(_downloadFolder);
        }

     // Open the folder in File Explorer
          var processStartInfo = new System.Diagnostics.ProcessStartInfo
    {
           FileName = _downloadFolder,
  UseShellExecute = true,
Verb = "open"
     };
       System.Diagnostics.Process.Start(processStartInfo);
            
       StatusText.Text = $"Opened folder: {_downloadFolder}";
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
          // Settings were updated

  // Apply items per page if it changed
 if (_itemsPerPage != _settings.ItemsPerPage)
      {
   _itemsPerPage = _settings.ItemsPerPage;
  
    // Reset to page 1 and update pagination
      _currentPage = 1;
        UpdatePagination();
        }
     
     StatusText.Text = "Settings updated.";
    }
        }

        private string SanitizeFileName(string fileName)
   {
 // Remove invalid path characters
 var invalidChars = IOPath.GetInvalidFileNameChars();
    var sanitized = string.Join("_", fileName.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries));
     
            // Ensure filename is not too long (max 200 characters)
 if (sanitized.Length > 200)
     {
 var extension = IOPath.GetExtension(sanitized);
      sanitized = sanitized.Substring(0, 200 - extension.Length) + extension;
 }
 
     return sanitized;
      }

        private async Task<BitmapImage?> LoadPreviewImageAsync(string imageUrl)
 {
 try
    {
   // Download image data
  var imageBytes = await _httpClient.GetByteArrayAsync(imageUrl);
      
 // Get the current preview column width and DPI scale
         double columnWidth = 120; // Default
     double dpiScale = 1.0;
 await Dispatcher.InvokeAsync(() =>
      {
       if (PreviewColumn.ActualWidth > 0)
      {
     columnWidth = PreviewColumn.ActualWidth;
    }
       
  // Get DPI scaling factor for high-DPI displays
      var source = PresentationSource.FromVisual(this);
     if (source?.CompositionTarget != null)
       {
 dpiScale = source.CompositionTarget.TransformToDevice.M11;
         }
   });

        // Calculate decode width with DPI scaling
// Multiply by 2 for extra quality, and by DPI scale for high-DPI displays
   int decodeWidth = (int)Math.Max(200, (columnWidth - 4) * dpiScale * 2);

        // Create BitmapImage from bytes
         var bitmap = new BitmapImage();
     using (var stream = new MemoryStream(imageBytes))
    {
   bitmap.BeginInit();
    bitmap.CacheOption = BitmapCacheOption.OnLoad;
           bitmap.StreamSource = stream;
 bitmap.DecodePixelWidth = decodeWidth; // High quality decode
  bitmap.EndInit();
           bitmap.Freeze(); // Make it cross-thread accessible
    }

     return bitmap;
  }
     catch (Exception ex)
       {
    // If preview fails, return null (will show no preview)
 System.Diagnostics.Debug.WriteLine($"Preview load failed for {imageUrl}: {ex.Message}");
       return null;
         }
        }

        private void UpdatePagination()
  {
      _totalPages = (int)Math.Ceiling((double)_imageItems.Count / _itemsPerPage);
    if (_totalPages == 0) _totalPages = 1;
            
   // Ensure current page is valid
   if (_currentPage > _totalPages) _currentPage = _totalPages;
  if (_currentPage < 1) _currentPage = 1;

   // Update page info text
    PageInfoText.Text = $"Page {_currentPage} of {_totalPages} ({_imageItems.Count} images)";
   
   // Update button states
    PrevPageButton.IsEnabled = _currentPage > 1;
    NextPageButton.IsEnabled = _currentPage < _totalPages;

         // Load current page items
  LoadCurrentPage();
 }

   private void LoadCurrentPage()
        {
       _currentPageItems.Clear();
            
      int startIndex = (_currentPage - 1) * _itemsPerPage;
   int endIndex = Math.Min(startIndex + _itemsPerPage, _imageItems.Count);

   for (int i = startIndex; i < endIndex; i++)
  {
    _currentPageItems.Add(_imageItems[i]);
   }
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

     private async Task<string?> TryFindFullResolutionUrlAsync(string previewUrl, CancellationToken cancellationToken)
        {
  try
   {
  // KEMONO.CR SPECIFIC PATTERN - NO HEAD REQUEST NEEDED
      // Preview: https://img.kemono.cr/thumbnail/data/...
  // Full:    https://n4.kemono.cr/data/...?f=filename
      if (previewUrl.Contains("kemono.cr"))
 {
        if (previewUrl.Contains("/thumbnail/"))
   {
       // Transform preview to full-res
  var fullResUrl = previewUrl.Replace("/thumbnail/", "/").Replace("img.kemono.cr", "n4.kemono.cr");
      System.Diagnostics.Debug.WriteLine($"Kemono.cr preview detected, transforming to full-res URL");
       return fullResUrl;
       }
 else if (previewUrl.Contains("n4.kemono.cr") || previewUrl.Contains("n5.kemono.cr"))
   {
     // Already a full-res URL (n4/n5 subdomain)
     System.Diagnostics.Debug.WriteLine($"Kemono.cr full-res URL detected (already full-res)");
return previewUrl; // Return same URL to indicate it's already full-res
   }
 }

  // Pattern 1: Remove "/thumbnail/" from path (generic pattern)
      if (previewUrl.Contains("/thumbnail/"))
    {
   var fullResUrl = previewUrl.Replace("/thumbnail/", "/");
if (await TestUrlExistsAsync(fullResUrl, cancellationToken))
   {
   return fullResUrl;
 }
    }

   // Pattern 2: Remove size suffixes
   var sizePatterns = new[] { "_800x800", "_small", "_medium", "_thumb", "_preview", "-thumb", "-preview" };
      foreach (var pattern in sizePatterns)
 {
    if (previewUrl.Contains(pattern))
   {
      var fullResUrl = previewUrl.Replace(pattern, "");
   if (await TestUrlExistsAsync(fullResUrl, cancellationToken))
    {
       return fullResUrl;
 }
       }
 }

   // Pattern 3: Replace "thumb" or "preview" with empty
      if (previewUrl.Contains("thumb") || previewUrl.Contains("preview"))
   {
      var fullResUrl = previewUrl.Replace("thumb", "").Replace("preview", "");
   if (await TestUrlExistsAsync(fullResUrl, cancellationToken))
      {
  return fullResUrl;
    }
    }

      // No transformation needed/found - return original URL
  System.Diagnostics.Debug.WriteLine($"No full-res pattern found, URL is likely already full-res or no transformation available");
return previewUrl; // Return original URL (it's likely already full-res from scan)
        }
       catch (Exception ex)
    {
     System.Diagnostics.Debug.WriteLine($"TryFindFullResolutionUrlAsync exception: {ex.Message}");
  return previewUrl; // On error, assume URL is fine as-is
 }
  }

        private async Task<bool> TestUrlExistsAsync(string url, CancellationToken cancellationToken)
        {
     try
 {
       var testStart = DateTime.Now;
   var request = new HttpRequestMessage(HttpMethod.Head, url);
 
// Add timeout for HEAD request (5 seconds max)
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
     using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
 
        var response = await _httpClient.SendAsync(request, linkedCts.Token);
      var elapsed = (DateTime.Now - testStart).TotalMilliseconds;

    System.Diagnostics.Debug.WriteLine($"HEAD request to {url.Substring(0, Math.Min(60, url.Length))}... took {elapsed}ms - Status: {response.StatusCode}");
       
    return response.IsSuccessStatusCode;
         }
 catch (TaskCanceledException)
  {
 System.Diagnostics.Debug.WriteLine($"HEAD request timed out for {url.Substring(0, Math.Min(60, url.Length))}...");
   return false;
   }
  catch (Exception ex)
 {
    System.Diagnostics.Debug.WriteLine($"HEAD request failed for {url.Substring(0, Math.Min(60, url.Length))}...: {ex.Message}");
     return false;
   }
   }
    }

    public class ImageDownloadItem : INotifyPropertyChanged
    {
private string _url = "";
   private string _status = "";
        private string _fileName = "";
        private string? _thumbnailPath;
        private string _errorMessage = "";
   private BitmapImage? _previewImage;

public string Url
        {
    get => _url;
      set { _url = value; OnPropertyChanged(nameof(Url)); }
}

public string Status
      {
     get => _status;
 set { _status = value; OnPropertyChanged(nameof(Status)); }
    }

   public string FileName
{
     get => _fileName;
  set { _fileName = value; OnPropertyChanged(nameof(FileName)); }
  }

 public string? ThumbnailPath
 {
   get => _thumbnailPath;
 set { _thumbnailPath = value; OnPropertyChanged(nameof(ThumbnailPath)); }
  }

    public string ErrorMessage
 {
 get => _errorMessage;
   set { _errorMessage = value; OnPropertyChanged(nameof(ErrorMessage)); }
      }

  public BitmapImage? PreviewImage
   {
 get => _previewImage;
 set { _previewImage = value; OnPropertyChanged(nameof(PreviewImage)); }
   }

   public event PropertyChangedEventHandler? PropertyChanged;

   protected virtual void OnPropertyChanged(string propertyName)
        {
  PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class DownloadSettings
    {
     public bool FilterBySize { get; set; } = false;
   public int MinimumImageSize { get; set; } = 5000; // 5KB minimum
        public bool ShowThumbnails { get; set; } = true;
        public bool FilterJpgOnly { get; set; } = false;
   public bool FilterPngOnly { get; set; } = false;
 public bool SkipFullResolutionCheck { get; set; } = false;
   public bool LimitScanCount { get; set; } = false;
     public int MaxImagesToScan { get; set; } = 20; // Default: scan 20 images
     public bool LoadPreviews { get; set; } = true; // Load preview images during scan
        public int ItemsPerPage { get; set; } = 50; // Items per page in pagination
  }

    // Converter to get the index of a ListView item
 public class ListViewIndexConverter : IValueConverter
    {
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
   {
       if (value is ListViewItem listViewItem)
 {
   var listView = FindParent<ListView>(listViewItem);
  if (listView != null)
    {
  var mainWindow = FindParent<MainWindow>(listView);
   int localIndex = listView.ItemContainerGenerator.IndexFromContainer(listViewItem);
 
           if (mainWindow != null && localIndex >= 0)
      {
         // Calculate global index based on current page
        int globalIndex = ((mainWindow._currentPage - 1) * mainWindow._itemsPerPage) + localIndex + 1;
      return globalIndex.ToString();
         }
    }
  }
  return "?";
  }

 public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
   {
  throw new NotImplementedException();
 }

    private static T? FindParent<T>(DependencyObject child) where T : DependencyObject
    {
   DependencyObject parentObject = VisualTreeHelper.GetParent(child);
  if (parentObject == null) return null;

       if (parentObject is T parent)
  return parent;
       else
   return FindParent<T>(parentObject);
   }
    }
}