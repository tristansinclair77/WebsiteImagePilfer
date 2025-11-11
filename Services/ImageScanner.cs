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
using WebsiteImagePilfer.Constants;
using static WebsiteImagePilfer.Constants.AppConstants;
using IOPath = System.IO.Path;

namespace WebsiteImagePilfer.Services
{
    public class ImageScanner
    {
        private readonly Action<string> _updateStatus;

        // Static compiled regex for image URL extraction - improves performance by caching the pattern
        private static readonly Lazy<Regex> _imageUrlRegex = new Lazy<Regex>(() =>
        {
            var extensions = string.Join("|", Images.Extensions.Select(e => e.TrimStart('.')));
            var pattern = $@"https?://[^\s""'<>\\]+?\.(?:{extensions})(?:\?[^\s""'<>\\]*)?";
            return new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
        });

        public ImageScanner(Action<string> updateStatus) => _updateStatus = updateStatus;

        public async Task<List<string>> ScanForImagesAsync(string url, CancellationToken cancellationToken, bool useFastScan = false)
        {
            var imageUrls = new HashSet<string>();
            IWebDriver? driver = null;

            await Task.Run(() =>
            {
                try
                {
                    driver = InitializeWebDriver(url);
                    if (useFastScan)
                        PerformFastScan(driver, cancellationToken);
                    else
                        PerformThoroughScan(driver, imageUrls, cancellationToken);

                    _updateStatus("Extracting final image URLs...");
                    ExtractImagesFromHtml(driver.PageSource, url, imageUrls, cancellationToken);
                }
                catch (Exception ex)
                {
                    _updateStatus($"Browser error: {ex.Message}");
                }
                finally
                {
                    CleanupWebDriver(driver);
                }
            }, cancellationToken);

            return imageUrls.ToList();
        }

        private IWebDriver InitializeWebDriver(string url)
        {
            _updateStatus("Launching browser...");

            var options = new ChromeOptions();
            options.AddArguments(
                "--headless=new",
                "--disable-gpu",
                "--no-sandbox",
                "--disable-dev-shm-usage",
                "--window-size=1920,1080",
                "user-agent=Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36",
                "--disable-blink-features=AutomationControlled"
            );
            options.PageLoadStrategy = PageLoadStrategy.Normal;

            var driver = new ChromeDriver(options);
            driver.Manage().Timeouts().PageLoad = TimeSpan.FromSeconds(Scanning.PageLoadTimeoutSeconds);
            driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(Network.ImplicitWaitSeconds);

            BringMainWindowToFront();
            LoadPageWithRetry(driver, url);

            return driver;
        }

        private void BringMainWindowToFront()
        {
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
        }

        private void LoadPageWithRetry(IWebDriver driver, string url)
        {
            _updateStatus("Loading page with JavaScript ...");

            for (int retryCount = 0; retryCount < Scanning.MaxRetryCount; retryCount++)
            {
                try
                {
                    driver.Navigate().GoToUrl(url);
                    return;
                }
                catch (WebDriverTimeoutException)
                {
                    if (retryCount >= Scanning.MaxRetryCount - 1) throw;
                    _updateStatus($"Page load timeout, retrying ({retryCount + 1}/{Scanning.MaxRetryCount}) ...");
                    Thread.Sleep(Scanning.RetryDelayMs);
                }
            }
        }

        private void PerformFastScan(IWebDriver driver, CancellationToken cancellationToken)
        {
            _updateStatus("Fast scan: Waiting for images to load ...");
            Thread.Sleep(Scanning.FastScanWaitMs);
            ScrollPage(driver);
            Thread.Sleep(Scanning.RetryDelayMs);
        }

        private void PerformThoroughScan(IWebDriver driver, HashSet<string> imageUrls, CancellationToken cancellationToken)
        {
            _updateStatus("Thorough scan: Waiting for images to load ...");

            int previousCount = 0;
            int stableCount = 0;

            while (stableCount < Scanning.ThoroughScanMaxStableChecks && !cancellationToken.IsCancellationRequested)
            {
                ScrollPage(driver);
                Thread.Sleep(Scanning.ThoroughScanCheckIntervalMs);

                var currentImages = ExtractCurrentImages(driver, cancellationToken);
                int currentCount = currentImages.Count;

                if (currentCount > previousCount)
                {
                    stableCount = 0;
                    _updateStatus($"Thorough scan: Found {currentCount} images so far, continuing ...");
                    previousCount = currentCount;
                    imageUrls.UnionWith(currentImages);
                }
                else
                {
                    stableCount++;
                    _updateStatus($"Thorough scan: Stable at {currentCount} images ({stableCount}/{Scanning.ThoroughScanMaxStableChecks} checks) ...");
                }
            }
        }

        private void ScrollPage(IWebDriver driver)
        {
            try
            {
                var js = (IJavaScriptExecutor)driver;
                js.ExecuteScript("window.scrollTo(0, document.body.scrollHeight);");
                Thread.Sleep(Scanning.ScrollDelayMs);
                js.ExecuteScript("window.scrollTo(0, document.body.scrollHeight / 2);");
                Thread.Sleep(Scanning.ScrollDelayMs);
                js.ExecuteScript("window.scrollTo(0, 0);");
            }
            catch (Exception ex)
            {
                // Scroll failed - not critical, continue anyway
                Logger.Warning($"Page scroll operation failed but continuing scan: {ex.Message}");
            }
        }

        private HashSet<string> ExtractCurrentImages(IWebDriver driver, CancellationToken cancellationToken)
        {
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
                            currentImages.Add(src);
                    }
                    catch (Exception ex)
                    {
                        // Skip invalid element - not critical
                        Logger.Warning($"Failed to extract image attribute from element: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                // Continue if extraction fails - not critical
                Logger.Warning($"Failed to extract images from page but continuing: {ex.Message}");
            }

            return currentImages;
        }

        private void ExtractImagesFromHtml(string renderedHtml, string baseUrl, HashSet<string> imageUrls, CancellationToken cancellationToken)
        {
            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(renderedHtml);
            var baseUri = new Uri(baseUrl);

            ExtractFromImageTags(htmlDoc, baseUri, imageUrls, cancellationToken);
            ExtractFromRegex(renderedHtml, imageUrls, cancellationToken);
        }

        private void ExtractFromImageTags(HtmlDocument htmlDoc, Uri baseUri, HashSet<string> imageUrls, CancellationToken cancellationToken)
        {
            var imgNodes = htmlDoc.DocumentNode.SelectNodes("//img");
            if (imgNodes == null) return;

            foreach (var img in imgNodes)
            {
                if (cancellationToken.IsCancellationRequested) break;

                // Check if image is wrapped in a link to full-resolution
                string? fullResUrl = GetFullResolutionUrlFromParentLink(img, baseUri);
                if (fullResUrl != null)
                {
                    imageUrls.Add(fullResUrl);
                    continue;
                }

                // Check img attributes for image URLs
                foreach (var attr in Images.Attributes)
                {
                    var src = img.GetAttributeValue(attr, "");
                    if (!string.IsNullOrEmpty(src) && !src.StartsWith("data:") &&
                        Uri.TryCreate(baseUri, src, out Uri? absoluteUri))
                    {
                        imageUrls.Add(absoluteUri.ToString());
                        break;
                    }
                }
            }
        }

        private string? GetFullResolutionUrlFromParentLink(HtmlNode img, Uri baseUri)
        {
            var parentLink = img.ParentNode;
            if (parentLink?.Name != "a") return null;

            var href = parentLink.GetAttributeValue("href", "");
            if (string.IsNullOrEmpty(href)) return null;

            var lower = href.ToLowerInvariant();
            bool isImageLink = Images.Extensions.Any(ext => lower.Contains(ext));

            if (isImageLink && Uri.TryCreate(baseUri, href, out Uri? absoluteUri))
                return absoluteUri.ToString();

            return null;
        }

        private void ExtractFromRegex(string renderedHtml, HashSet<string> imageUrls, CancellationToken cancellationToken)
        {
            // Use cached compiled regex for better performance
            var matches = _imageUrlRegex.Value.Matches(renderedHtml);

            foreach (Match match in matches)
            {
                if (cancellationToken.IsCancellationRequested) break;
                if (Uri.TryCreate(match.Value, UriKind.Absolute, out Uri? uri))
                    imageUrls.Add(uri.ToString());
            }
        }

        private void CleanupWebDriver(IWebDriver? driver)
        {
            try { driver?.Quit(); } 
            catch (Exception ex) 
            { 
                // Quit failed - try disposal anyway
                Logger.Warning($"WebDriver quit failed during cleanup: {ex.Message}");
            }
            
            try { driver?.Dispose(); } 
            catch (Exception ex) 
            { 
                 // Disposal failed - nothing more we can do
                Logger.Warning($"WebDriver disposal failed during cleanup: {ex.Message}");
            }
        }
    }
}
