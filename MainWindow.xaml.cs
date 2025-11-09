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

   public MainWindow()
        {
       InitializeComponent();
       _httpClient = new HttpClient();
    _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
      _imageItems = new ObservableCollection<ImageDownloadItem>();
 ImageList.ItemsSource = _imageItems;
          
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

        private async void ScanButton_Click(object sender, RoutedEventArgs e)
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
      ScanButton.IsEnabled = false;
            CancelButton.IsEnabled = true;
            _imageItems.Clear();
   DownloadProgress.Value = 0;

   try
 {
     StatusText.Text = "Scanning webpage...";
    
     // Get image URLs - try Selenium first for SPA sites
        var imageUrls = await GetImageUrlsWithSeleniumAsync(url, _cancellationTokenSource.Token);
    
    if (_cancellationTokenSource.Token.IsCancellationRequested)
       {
           StatusText.Text = "Operation cancelled.";
       return;
       }

   if (imageUrls.Count == 0)
       {
MessageBox.Show("No images found on the webpage.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
              StatusText.Text = "No images found.";
    return;
  }

          StatusText.Text = $"Found {imageUrls.Count} images. Starting download...";
    
        // Download images
    int downloadedCount = await DownloadImagesAsync(imageUrls, _cancellationTokenSource.Token);
    
       if (_cancellationTokenSource.Token.IsCancellationRequested)
        {
    StatusText.Text = $"Cancelled. Downloaded {downloadedCount} of {imageUrls.Count} images.";
          MessageBox.Show($"Operation cancelled. Downloaded {downloadedCount} of {imageUrls.Count} images.", 
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
          StatusText.Text = "Operation cancelled by user.";
          }
 catch (Exception ex)
    {
   MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
    StatusText.Text = "Error occurred during download.";
  }
    finally
    {
  ScanButton.IsEnabled = true;
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

        private async Task<List<string>> GetImageUrlsWithSeleniumAsync(string url, CancellationToken cancellationToken)
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
      options.AddArgument("--headless=new"); // Run in background
        options.AddArgument("--disable-gpu");
         options.AddArgument("--no-sandbox");
        options.AddArgument("--disable-dev-shm-usage");
      options.AddArgument("--window-size=1920,1080");
             options.AddArgument("user-agent=Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

   // Create Chrome driver
    driver = new ChromeDriver(options);
           driver.Manage().Timeouts().PageLoad = TimeSpan.FromSeconds(30);

        StatusText.Dispatcher.Invoke(() => StatusText.Text = "Loading page with JavaScript...");

 // Navigate to URL
         driver.Navigate().GoToUrl(url);

      // Wait for page to load and JavaScript to execute
   System.Threading.Thread.Sleep(5000); // Wait 5 seconds for JS to load images

    // Try to scroll down to trigger lazy loading
          var js = (IJavaScriptExecutor)driver;
        js.ExecuteScript("window.scrollTo(0, document.body.scrollHeight);");
               System.Threading.Thread.Sleep(2000); // Wait 2 more seconds

          StatusText.Dispatcher.Invoke(() => StatusText.Text = "Extracting image URLs...");

     // Get the rendered HTML after JavaScript execution
     var renderedHtml = driver.PageSource;

             // Save debug HTML
                    var debugPath = IOPath.Combine(_downloadFolder, "debug_rendered_page.html");
 File.WriteAllText(debugPath, renderedHtml);

         // Parse the rendered HTML
      var htmlDoc = new HtmlDocument();
        htmlDoc.LoadHtml(renderedHtml);
  var baseUri = new Uri(url);

          // METHOD 1: Get all IMG elements directly from Selenium
 var imgElements = driver.FindElements(By.TagName("img"));
              foreach (var img in imgElements)
               {
      if (cancellationToken.IsCancellationRequested) break;

       try
    {
         var src = img.GetAttribute("src");
     if (!string.IsNullOrEmpty(src) && !src.StartsWith("data:"))
     {
         if (Uri.TryCreate(baseUri, src, out Uri? absoluteUri))
      {
              imageUrls.Add(absoluteUri.ToString());
             }
        }
           }
catch { /* Skip invalid elements */ }
    }

         // METHOD 2: Parse rendered HTML with all previous methods
          var imgNodes = htmlDoc.DocumentNode.SelectNodes("//img");
        if (imgNodes != null)
           {
     foreach (var img in imgNodes)
    {
         if (cancellationToken.IsCancellationRequested) break;

       string[] possibleAttributes = { "src", "data-src", "data-lazy-src", "data-original" };
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

          // METHOD 3: Find direct image links
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

          // METHOD 4: Regex scan of rendered HTML
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
    StatusText.Text = $"Selenium error: {ex.Message}");
    }
    finally
      {
   driver?.Quit();
  driver?.Dispose();
       }
    }, cancellationToken);

            return imageUrls.ToList();
        }

        private async Task<int> DownloadImagesAsync(List<string> imageUrls, CancellationToken cancellationToken)
        {
            // Create download folder if it doesn't exist
    if (!Directory.Exists(_downloadFolder))
   {
 Directory.CreateDirectory(_downloadFolder);
    }

   int totalImages = imageUrls.Count;
            int downloadedCount = 0;
     int skippedCount = 0;

     foreach (var imageUrl in imageUrls)
    {
   cancellationToken.ThrowIfCancellationRequested();

      var item = new ImageDownloadItem
       {
     Url = imageUrl,
   Status = "Downloading...",
   FileName = ""
     };
       
  _imageItems.Add(item);

        try
      {
      // Get the image filename from URL
     var uri = new Uri(imageUrl);
       var fileName = IOPath.GetFileName(uri.LocalPath);
      
   // If no filename or extension, generate one
    if (string.IsNullOrEmpty(fileName) || !IOPath.HasExtension(fileName))
         {
      fileName = $"image_{DateTime.Now:yyyyMMddHHmmss}_{downloadedCount + 1}.jpg";
    }
    
    // Check file type filters before downloading
    var extension = IOPath.GetExtension(fileName).ToLowerInvariant();
          if (_settings.FilterJpgOnly && extension != ".jpg" && extension != ".jpeg")
{
             item.Status = "⊘ Skipped (not JPG)";
              item.FileName = $"Filtered: {extension}";
          skippedCount++;
   continue;
               }
                if (_settings.FilterPngOnly && extension != ".png")
 {
    item.Status = "⊘ Skipped (not PNG)";
          item.FileName = $"Filtered: {extension}";
        skippedCount++;
   continue;
     }

           // Sanitize filename - remove invalid characters
         fileName = SanitizeFileName(fileName);
 
    // Ensure unique filename
       var filePath = IOPath.Combine(_downloadFolder, fileName);
  int counter = 1;
             while (File.Exists(filePath))
       {
            var nameWithoutExt = IOPath.GetFileNameWithoutExtension(fileName);
var extension2 = IOPath.GetExtension(fileName);
            fileName = $"{nameWithoutExt}_{counter}{extension2}";
   filePath = IOPath.Combine(_downloadFolder, fileName);
           counter++;
             }

          // Download the image
    var imageBytes = await _httpClient.GetByteArrayAsync(imageUrl, cancellationToken);
  
   // Filter by size if setting is enabled
   if (_settings.FilterBySize && imageBytes.Length < _settings.MinimumImageSize)
                 {
 item.Status = "⊘ Skipped (too small)";
            item.FileName = $"{imageBytes.Length} bytes";
   skippedCount++;
         continue;
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

        item.FileName = fileName;
     item.Status = "✓ Done";
   downloadedCount++;
   }
        catch (OperationCanceledException)
         {
                 item.Status = "⊘ Cancelled";
    throw;
      }
    catch (HttpRequestException ex)
         {
         item.Status = "✗ Failed";
    item.FileName = $"Network error: {ex.Message}";
  skippedCount++;
           }
            catch (Exception ex)
        {
      item.Status = "✗ Failed";
                 item.FileName = ex.Message;
  skippedCount++;
          }

       // Update progress
                int processedCount = downloadedCount + skippedCount;
  DownloadProgress.Value = (processedCount * 100.0) / totalImages;
              StatusText.Text = $"Downloaded: {downloadedCount} | Skipped: {skippedCount} | Remaining: {totalImages - processedCount}";

             // Small delay to allow UI to update
          await Task.Delay(10, cancellationToken);
          }
     
   return downloadedCount;
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

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
   {
            // FIX #3: Ensure the initial directory exists, otherwise use parent or user folder
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

        protected override void OnClosing(CancelEventArgs e)
        {
            // Cancel any ongoing operations
_cancellationTokenSource?.Cancel();
    
            base.OnClosing(e);
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