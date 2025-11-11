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
        private const int FAST_SCAN_WAIT_MS = 5000;
        private const int THOROUGH_SCAN_CHECK_INTERVAL_MS = 5000;
        private const int THOROUGH_SCAN_MAX_STABLE_CHECKS = 3;
        private const int SCROLL_DELAY_MS = 1000;
        private const int RETRY_DELAY_MS = 2000;
        private const int MAX_RETRY_COUNT = 3;
        private const int PAGE_LOAD_TIMEOUT_SECONDS = 60;
        private const int IMPLICIT_WAIT_SECONDS = 10;

        private static readonly string[] IMAGE_ATTRIBUTES = { "src", "data-src", "data-lazy-src", "data-original", "data-file" };
        private static readonly string[] IMAGE_EXTENSIONS = { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp" };

        private readonly Action<string> _updateStatus;

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
            driver.Manage().Timeouts().PageLoad = TimeSpan.FromSeconds(PAGE_LOAD_TIMEOUT_SECONDS);
            driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(IMPLICIT_WAIT_SECONDS);

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
            _updateStatus("Loading page with JavaScript...");

            for (int retryCount = 0; retryCount < MAX_RETRY_COUNT; retryCount++)
            {
                try
                {
                    driver.Navigate().GoToUrl(url);
                    return;
                }
                catch (WebDriverTimeoutException)
                {
                    if (retryCount >= MAX_RETRY_COUNT - 1) throw;
                    _updateStatus($"Page load timeout, retrying ({retryCount + 1}/{MAX_RETRY_COUNT})...");
                    Thread.Sleep(RETRY_DELAY_MS);
                }
            }
        }

        private void PerformFastScan(IWebDriver driver, CancellationToken cancellationToken)
        {
            _updateStatus("Fast scan: Waiting for images to load...");
            Thread.Sleep(FAST_SCAN_WAIT_MS);
            ScrollPage(driver);
            Thread.Sleep(RETRY_DELAY_MS);
        }

        private void PerformThoroughScan(IWebDriver driver, HashSet<string> imageUrls, CancellationToken cancellationToken)
        {
            _updateStatus("Thorough scan: Waiting for images to load...");

            int previousCount = 0;
            int stableCount = 0;

            while (stableCount < THOROUGH_SCAN_MAX_STABLE_CHECKS && !cancellationToken.IsCancellationRequested)
            {
                ScrollPage(driver);
                Thread.Sleep(THOROUGH_SCAN_CHECK_INTERVAL_MS);

                var currentImages = ExtractCurrentImages(driver, cancellationToken);
                int currentCount = currentImages.Count;

                if (currentCount > previousCount)
                {
                    stableCount = 0;
                    _updateStatus($"Thorough scan: Found {currentCount} images so far, continuing...");
                    previousCount = currentCount;
                    imageUrls.UnionWith(currentImages);
                }
                else
                {
                    stableCount++;
                    _updateStatus($"Thorough scan: Stable at {currentCount} images ({stableCount}/{THOROUGH_SCAN_MAX_STABLE_CHECKS} checks)...");
                }
            }
        }

        private void ScrollPage(IWebDriver driver)
        {
            try
            {
                var js = (IJavaScriptExecutor)driver;
                js.ExecuteScript("window.scrollTo(0, document.body.scrollHeight);");
                Thread.Sleep(SCROLL_DELAY_MS);
                js.ExecuteScript("window.scrollTo(0, document.body.scrollHeight / 2);");
                Thread.Sleep(SCROLL_DELAY_MS);
                js.ExecuteScript("window.scrollTo(0, 0);");
            }
            catch { /* Scroll failed, continue anyway */ }
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
                    catch { /* Skip invalid elements */ }
                }
            }
            catch { /* Continue if extraction fails */ }

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
                foreach (var attr in IMAGE_ATTRIBUTES)
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
            bool isImageLink = IMAGE_EXTENSIONS.Any(ext => lower.Contains(ext));

            if (isImageLink && Uri.TryCreate(baseUri, href, out Uri? absoluteUri))
                return absoluteUri.ToString();

            return null;
        }

        private void ExtractFromRegex(string renderedHtml, HashSet<string> imageUrls, CancellationToken cancellationToken)
        {
            var extensions = string.Join("|", IMAGE_EXTENSIONS.Select(e => e.TrimStart('.')));
            var imageUrlPattern = $@"https?://[^\s""'<>\\]+?\.(?:{extensions})(?:\?[^\s""'<>\\]*)?";
            var matches = Regex.Matches(renderedHtml, imageUrlPattern, RegexOptions.IgnoreCase);

            foreach (Match match in matches)
            {
                if (cancellationToken.IsCancellationRequested) break;
                if (Uri.TryCreate(match.Value, UriKind.Absolute, out Uri? uri))
                    imageUrls.Add(uri.ToString());
            }
        }

        private void CleanupWebDriver(IWebDriver? driver)
        {
            try { driver?.Quit(); } catch { }
            try { driver?.Dispose(); } catch { }
        }
    }
}
