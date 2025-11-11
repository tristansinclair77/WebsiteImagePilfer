using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using HtmlAgilityPack;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using IOPath = System.IO.Path;

namespace WebsiteImagePilfer.Services
{
    public class ImageScanner
    {
        private readonly Action<string> _updateStatus;

public ImageScanner(Action<string> updateStatus)
        {
            _updateStatus = updateStatus;
        }

 public async Task<List<string>> ScanForImagesAsync(string url, CancellationToken cancellationToken, bool useFastScan = false)
        {
         var imageUrls = new HashSet<string>();
   IWebDriver? driver = null;

   await Task.Run(() =>
       {
     try
          {
      _updateStatus("Launching browser...");

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

         // Bring main window to front after Selenium window opens
            Application.Current.Dispatcher.Invoke(() =>
         {
  var mainWindow = Application.Current.MainWindow;
    if (mainWindow != null)
  {
             mainWindow.Activate();
     mainWindow.Topmost = true;
            mainWindow.Topmost = false;
        mainWindow.Focus();
    }
    });

        _updateStatus("Loading page with JavaScript...");

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
          _updateStatus($"Page load timeout, retrying ({retryCount}/3)...");
       Thread.Sleep(2000);
     }
          }

 if (useFastScan)
      {
        PerformFastScan(driver, cancellationToken);
      }
        else
     {
   PerformThoroughScan(driver, imageUrls, cancellationToken);
        }

         _updateStatus("Extracting final image URLs...");

             // Final comprehensive extraction
   var renderedHtml = driver.PageSource;
               ExtractImagesFromHtml(renderedHtml, url, imageUrls, cancellationToken);
      }
       catch (Exception ex)
        {
        _updateStatus($"Browser error: {ex.Message}");
          }
                finally
      {
     try { driver?.Quit(); } catch { }
      try { driver?.Dispose(); } catch { }
   }
       }, cancellationToken);

    return imageUrls.ToList();
 }

      private void PerformFastScan(IWebDriver driver, CancellationToken cancellationToken)
     {
          _updateStatus("Fast scan: Waiting for images to load...");
      Thread.Sleep(5000); // Wait 5 seconds

      // Try to scroll down to trigger lazy loading
            try
            {
           var js = (IJavaScriptExecutor)driver;
     js.ExecuteScript("window.scrollTo(0, document.body.scrollHeight);");
        Thread.Sleep(2000);
            }
            catch { /* Scroll failed, continue anyway */ }
     }

        private void PerformThoroughScan(IWebDriver driver, HashSet<string> imageUrls, CancellationToken cancellationToken)
{
            _updateStatus("Thorough scan: Waiting for images to load...");

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
   Thread.Sleep(1000);
 js.ExecuteScript("window.scrollTo(0, document.body.scrollHeight / 2);");
           Thread.Sleep(1000);
           js.ExecuteScript("window.scrollTo(0, 0);");
      }
          catch { /* Scroll failed, continue anyway */ }

   // Wait for images to load
      Thread.Sleep(checkInterval);

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
        _updateStatus($"Thorough scan: Found {currentCount} images so far, continuing...");
         previousCount = currentCount;
             imageUrls.UnionWith(currentImages);
        }
 else
      {
         stableCount++; // No new images found
            _updateStatus($"Thorough scan: Stable at {currentCount} images ({stableCount}/{maxStableChecks} checks)...");
   }
            }
        }

        private void ExtractImagesFromHtml(string renderedHtml, string baseUrl, HashSet<string> imageUrls, CancellationToken cancellationToken)
 {
            var htmlDoc = new HtmlDocument();
          htmlDoc.LoadHtml(renderedHtml);
            var baseUri = new Uri(baseUrl);

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
    }
}
