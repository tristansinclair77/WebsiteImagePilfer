using System.Text;
using System.Windows;
using System.Windows.Controls;
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

namespace WebsiteImagePilfer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
     private readonly HttpClient _httpClient;
 private ObservableCollection<ImageDownloadItem> _imageItems;
 private string _downloadFolder;
   private CancellationTokenSource? _cancellationTokenSource;
 private DownloadSettings _settings;
        private List<string> _scannedImageUrls; // Store scanned URLs

   public MainWindow()
     {
       InitializeComponent();
       _httpClient = new HttpClient();
    _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
   _httpClient.Timeout = TimeSpan.FromSeconds(30);
      _imageItems = new ObservableCollection<ImageDownloadItem>();
 ImageList.ItemsSource = _imageItems;
    _scannedImageUrls = new List<string>();
          
     // Load settings
            _settings = new DownloadSettings();

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
   var fileName = IOPath.GetFileName(uri.LocalPath);
   if (string.IsNullOrEmpty(fileName) || !IOPath.HasExtension(fileName))
  {
       fileName = $"image_{_imageItems.Count + 1}.jpg";
      }
          fileName = SanitizeFileName(fileName);

  var item = new ImageDownloadItem
  {
  Url = imageUrl,
     Status = "Ready",
    FileName = fileName
    };
_imageItems.Add(item);
      }

        string scanType = useFastScan ? "Fast" : "Thorough";
          StatusText.Text = $"Found {_scannedImageUrls.Count} images ({scanType} scan). Click 'Download' to save them.";
    DownloadButton.IsEnabled = true;
 MessageBox.Show($"Found {_scannedImageUrls.Count} images using {scanType} scan!\n\nReview the list and click 'Download' when ready.", 
  "Scan Complete", MessageBoxButton.OK, MessageBoxImage.Information);
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
           // Download single image on double-click
    _ = DownloadSingleImageAsync(item);
 }
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
          item.FileName = ex.Message;
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

          return downloadedCount;
 }

        private async Task DownloadSingleItemAsync(ImageDownloadItem item, CancellationToken cancellationToken)
        {
    try
            {
     item.Status = "Checking...";

   // Get the image filename from URL
    var uri = new Uri(item.Url);
                var fileName = item.FileName;

                // Sanitize filename if not already done
    if (string.IsNullOrEmpty(fileName) || fileName.Contains("image_"))
        {
              fileName = IOPath.GetFileName(uri.LocalPath);
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

      // Check if file already exists (DUPLICATE DETECTION)
  var filePath = IOPath.Combine(_downloadFolder, fileName);
     if (File.Exists(filePath))
      {
     item.Status = "⊘ Duplicate";
           return;
         }

           item.Status = "Downloading...";

       // Download the image
     var imageBytes = await _httpClient.GetByteArrayAsync(item.Url, cancellationToken);

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

    item.Status = "✓ Done";
  }
            catch (OperationCanceledException)
       {
         item.Status = "⊘ Cancelled";
   throw;
    }
            catch (HttpRequestException ex)
      {
            item.Status = "✗ Failed";
   item.FileName = $"{item.FileName} - Network error: {ex.Message}";
          }
    catch (Exception ex)
        {
     item.Status = "✗ Failed";
           item.FileName = $"{item.FileName} - {ex.Message}";
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

     string[] possibleAttributes = { "src", "data-src", "data-lazy-src", "data-original", "data-file" };
  foreach (var attr in possibleAttributes)
       {
    var src = img.GetAttributeValue(attr, "");
    if (!string.IsNullOrEmpty(src) && !src.StartsWith("data:"))
 {
     if (Uri.TryCreate(baseUri, src, out Uri? absoluteUri))
   {
      imageUrls.Add(absoluteUri.ToString());
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

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            var settingsWindow = new SettingsWindow(_settings);
    if (settingsWindow.ShowDialog() == true)
            {
  // Settings were updated, refresh if needed
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
    }

    public class ImageDownloadItem : INotifyPropertyChanged
    {
        private string _url = "";
        private string _status = "";
        private string _fileName = "";
        private string? _thumbnailPath;

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
    }
}