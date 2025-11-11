using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using HtmlAgilityPack;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using WebsiteImagePilfer.Constants;
using WebsiteImagePilfer.Models;
using static WebsiteImagePilfer.Constants.AppConstants;
using IOPath = System.IO.Path;

namespace WebsiteImagePilfer.Services
{
    public class ImageScanner
    {
        private readonly Action<string> _updateStatus;
        private readonly DownloadSettings _settings;
        private readonly HttpClient _httpClient;

        // Static compiled regex for image URL extraction - improves performance by caching the pattern
        private static readonly Lazy<Regex> _imageUrlRegex = new Lazy<Regex>(() =>
        {
            var extensions = string.Join("|", Images.Extensions.Select(e => e.TrimStart('.')));
            var pattern = $@"https?://[^\s""'<>\\]+?\.(?:{extensions})(?:\?[^\s""'<>\\]*)?";
            return new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
        });
        
        // Regex for finding URLs that might be images even without extensions
        private static readonly Lazy<Regex> _potentialImageUrlRegex = new Lazy<Regex>(() =>
        {
            // Match URLs that look like they might be images (contain 'image', 'img', 'photo', 'pic', or have image-like paths)
            var pattern = @"https?://[^\s""'<>\\]+?(?:/(?:image|img|photo|pic|media|assets|cdn|upload|gen_)[^\s""'<>\\]*)";
            return new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
        });
        
        // Regex for extracting URLs from background-image CSS properties
        private static readonly Lazy<Regex> _backgroundImageRegex = new Lazy<Regex>(() =>
        {
            var pattern = @"background(?:-image)?:\s*url\([""']?([^""')]+)[""']?\)";
            return new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
        });

        public ImageScanner(Action<string> updateStatus, DownloadSettings settings)
        {
            _updateStatus = updateStatus;
            _settings = settings;
            _httpClient = HttpClientFactory.Instance;
        }

        public async Task<List<string>> ScanForImagesAsync(string url, CancellationToken cancellationToken, bool useFastScan = false)
        {
            var imageUrls = new HashSet<string>();

            if (useFastScan)
            {
                // Fast Scan: Simple HTTP request, no Selenium, just parse HTML
                await PerformFastScanAsync(url, imageUrls, cancellationToken);
            }
            else
            {
                // Thorough Scan: Use configurable layers based on settings
                if (_settings.ThoroughScan_UseSelenium)
                {
                    await PerformThoroughScanWithSeleniumAsync(url, imageUrls, cancellationToken);
                }
                else
                {
                    // Thorough but without Selenium - just more extensive HTML parsing
                    await PerformThoroughScanWithoutSeleniumAsync(url, imageUrls, cancellationToken);
                }
            }

            // Apply file type filter before returning
            var filteredUrls = FilterImageUrlsByFileType(imageUrls);
            Logger.Info($"After filtering: {filteredUrls.Count} URLs (filtered out {imageUrls.Count - filteredUrls.Count})");
            return filteredUrls.ToList();
        }
        
        /// <summary>
        /// Fast Scan: Quick HTTP-only scan that extracts basic image URLs from HTML
        /// No JavaScript execution, no Selenium overhead
        /// </summary>
        private async Task PerformFastScanAsync(string url, HashSet<string> imageUrls, CancellationToken cancellationToken)
        {
            try
            {
                _updateStatus("Fast scan: Downloading HTML...");
                
                var response = await _httpClient.GetAsync(url, cancellationToken);
                response.EnsureSuccessStatusCode();
                
                var html = await response.Content.ReadAsStringAsync(cancellationToken);
                
                _updateStatus("Fast scan: Extracting image URLs...");
                
                var htmlDoc = new HtmlDocument();
                htmlDoc.LoadHtml(html);
                var baseUri = new Uri(url);
                
                // Extract from <img> tags only
                var imgNodes = htmlDoc.DocumentNode.SelectNodes("//img");
                if (imgNodes != null)
                {
                    foreach (var img in imgNodes)
                    {
                        if (cancellationToken.IsCancellationRequested) break;
                        
                        var src = img.GetAttributeValue("src", "");
                        if (!string.IsNullOrEmpty(src) && !src.StartsWith("data:") &&
                            Uri.TryCreate(baseUri, src, out Uri? absoluteUri))
                        {
                            imageUrls.Add(absoluteUri.ToString());
                        }
                    }
                }
                
                // Also use regex to find image URLs in the HTML
                ExtractFromRegex(html, imageUrls, cancellationToken);
                
                Logger.Info($"Fast scan complete: Found {imageUrls.Count} URLs");
                _updateStatus($"Fast scan complete: Found {imageUrls.Count} images");
            }
            catch (HttpRequestException ex)
            {
                Logger.Error($"HTTP error during fast scan: {ex.Message}", ex);
                _updateStatus($"Error: {ex.Message}");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error during fast scan", ex);
                _updateStatus($"Error: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Thorough scan without Selenium - more extensive HTML parsing
        /// </summary>
        private async Task PerformThoroughScanWithoutSeleniumAsync(string url, HashSet<string> imageUrls, CancellationToken cancellationToken)
        {
            try
            {
                _updateStatus("Thorough scan: Downloading HTML...");
                
                var response = await _httpClient.GetAsync(url, cancellationToken);
                response.EnsureSuccessStatusCode();
                
                var html = await response.Content.ReadAsStringAsync(cancellationToken);
                
                _updateStatus("Thorough scan: Analyzing HTML...");
                
                ExtractImagesFromHtml(html, url, imageUrls, cancellationToken);
                
                Logger.Info($"Thorough scan (no Selenium) complete: Found {imageUrls.Count} URLs");
                _updateStatus($"Thorough scan complete: Found {imageUrls.Count} images");
            }
            catch (HttpRequestException ex)
            {
                Logger.Error($"HTTP error during thorough scan: {ex.Message}", ex);
                _updateStatus($"Error: {ex.Message}");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error during thorough scan", ex);
                _updateStatus($"Error: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Thorough scan with Selenium - executes JavaScript and uses configured detection layers
        /// </summary>
        private async Task PerformThoroughScanWithSeleniumAsync(string url, HashSet<string> imageUrls, CancellationToken cancellationToken)
        {
            IWebDriver? driver = null;

            await Task.Run(() =>
            {
                try
                {
                    driver = InitializeWebDriver(url);
                    PerformThoroughScanWithSelenium(driver, imageUrls, cancellationToken);

                    _updateStatus("Extracting final image URLs...");
                    ExtractImagesFromHtml(driver.PageSource, url, imageUrls, cancellationToken);
                    
                    // Log what we found
                    Logger.Info($"Scan complete: Found {imageUrls.Count} total URLs before filtering");
                    
                    // Show first few URLs for debugging
                    if (imageUrls.Count > 0)
                    {
                        var sampleUrls = imageUrls.Take(5).ToList();
                        Logger.Info($"Sample URLs found:");
                        foreach (var imgUrl in sampleUrls)
                        {
                            Logger.Info($"  - {imgUrl}");
                        }
                        
                        if (imageUrls.Count > 5)
                            Logger.Info($"  ... and {imageUrls.Count - 5} more");
                    }
                    else
                    {
                        Logger.Warning("No image URLs found! Check the debug HTML file to see what was loaded.");
                    }
                }
                catch (Exception ex)
                {
                    _updateStatus($"Browser error: {ex.Message}");
                    Logger.Error("Scanner error", ex);
                }
                finally
                {
                    CleanupWebDriver(driver);
                }
            }, cancellationToken);
        }

        /// <summary>
        /// Filters image URLs based on AllowedFileTypes setting.
        /// If AllowedFileTypes is empty, all images are allowed.
        /// URLs without extensions are always allowed (will be verified during download).
        /// </summary>
        private HashSet<string> FilterImageUrlsByFileType(HashSet<string> imageUrls)
        {
            // If no file type filter is set, return all URLs
            if (_settings.AllowedFileTypes.Count == 0)
            {
                return imageUrls;
            }

            var filteredUrls = new HashSet<string>();
            
            foreach (var url in imageUrls)
            {
                // Extract extension from URL (handle query strings)
                var uri = new Uri(url);
                var path = uri.AbsolutePath;
                var extension = IOPath.GetExtension(path).ToLowerInvariant();
                
                // If URL has no extension, allow it (will be verified during download by content-type)
                if (string.IsNullOrEmpty(extension))
                {
                    filteredUrls.Add(url);
                    continue;
                }
                
                // Check if this extension is allowed
                if (_settings.AllowedFileTypes.Contains(extension))
                {
                    filteredUrls.Add(url);
                }
            }

            int filteredCount = imageUrls.Count - filteredUrls.Count;
            if (filteredCount > 0)
            {
                var allowedTypes = string.Join(", ", _settings.AllowedFileTypes);
                _updateStatus($"Filtered {filteredCount} images (showing: {allowedTypes} + extensionless URLs)");
            }

            return filteredUrls;
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
            _updateStatus("Fast scan: Waiting for initial page load ...");
            Thread.Sleep(3000); // Give JavaScript apps time to initialize
            
            _updateStatus("Fast scan: Scrolling to load images ...");
            ScrollPage(driver);
            Thread.Sleep(Scanning.FastScanWaitMs);
            
            // Scroll again to catch any lazy-loaded content
            ScrollPage(driver);
            Thread.Sleep(Scanning.RetryDelayMs);
        }

        private void PerformThoroughScanWithSelenium(IWebDriver driver, HashSet<string> imageUrls, CancellationToken cancellationToken)
        {
            _updateStatus("Thorough scan: Waiting for JavaScript to load ...");
            
            // Wait for page to be ready
            var js = (IJavaScriptExecutor)driver;
            for (int i = 0; i < 10; i++)
            {
                Thread.Sleep(500);
                var readyState = js.ExecuteScript("return document.readyState").ToString();
                if (readyState == "complete")
                    break;
            }
            
            // Additional wait for React/Vue/Angular apps to mount and render
            Thread.Sleep(5000);
            
            // Check if page has actual content
            var bodyText = js.ExecuteScript("return document.body ? document.body.innerText.length : 0");
            Logger.Debug($"Page body text length: {bodyText}");
            
            var elementCount = js.ExecuteScript("return document.querySelectorAll('*').length");
            Logger.Debug($"Total DOM elements: {elementCount}");
            
            _updateStatus("Thorough scan: Detecting images ...");

            int previousCount = 0;
            int stableCount = 0;
            int scrollAttempts = 0;
            const int maxScrollAttempts = 10; // Scroll more times for infinite scroll sites

            while (stableCount < Scanning.ThoroughScanMaxStableChecks && scrollAttempts < maxScrollAttempts && !cancellationToken.IsCancellationRequested)
            {
                // Scroll multiple times to trigger lazy loading
                ScrollPageAggressively(driver);
                scrollAttempts++;
                Thread.Sleep(Scanning.ThoroughScanCheckIntervalMs);

                var currentImages = ExtractCurrentImages(driver, cancellationToken);
                int currentCount = currentImages.Count;

                if (currentCount > previousCount)
                {
                    stableCount = 0;
                    _updateStatus($"Thorough scan: Found {currentCount} images so far (scroll {scrollAttempts}/{maxScrollAttempts}) ...");
                    previousCount = currentCount;
                    imageUrls.UnionWith(currentImages);
                }
                else
                {
                    stableCount++;
                    _updateStatus($"Thorough scan: Stable at {currentCount} images ({stableCount}/{Scanning.ThoroughScanMaxStableChecks} checks, scroll {scrollAttempts}/{maxScrollAttempts}) ...");
                }
            }
            
            // Optionally save debug files
            if (_settings.ThoroughScan_SaveDebugFiles)
            {
                SaveDebugFiles(driver);
            }
        }
        
        private void SaveDebugFiles(IWebDriver driver)
        {
            try
            {
                var pageSource = driver.PageSource;
                Logger.Debug($"Page source length: {pageSource.Length} characters");
                
                // Save page source to file for inspection
                var debugFolder = IOPath.Combine(AppContext.BaseDirectory, "debug");
                if (!Directory.Exists(debugFolder))
                    Directory.CreateDirectory(debugFolder);
                
                var debugFile = IOPath.Combine(debugFolder, $"page_source_{DateTime.Now:yyyyMMdd_HHmmss}.html");
                File.WriteAllText(debugFile, pageSource);
                Logger.Info($"Saved page source to: {debugFile}");
                
                // Take a screenshot to see what the page actually looked like
                try
                {
                    var screenshot = ((ITakesScreenshot)driver).GetScreenshot();
                    var screenshotFile = IOPath.Combine(debugFolder, $"screenshot_{DateTime.Now:yyyyMMdd_HHmmss}.png");
                    screenshot.SaveAsFile(screenshotFile);
                    Logger.Info($"Saved screenshot to: {screenshotFile}");
                }
                catch (Exception screenshotEx)
                {
                    Logger.Warning($"Could not save screenshot: {screenshotEx.Message}");
                }
                
                // Check if specific patterns exist
                if (pageSource.Contains("sora.chatgpt.com", StringComparison.OrdinalIgnoreCase))
                    Logger.Debug("Detected Sora website");
                if (pageSource.Contains("/g/gen_", StringComparison.OrdinalIgnoreCase))
                {
                    Logger.Debug("Found /g/gen_ pattern in page source");
                    
                    // Count how many times it appears
                    var genCount = Regex.Matches(pageSource, @"/g/gen_[a-zA-Z0-9]+", RegexOptions.IgnoreCase).Count;
                    Logger.Debug($"Found {genCount} instances of /g/gen_ pattern");
                }
                if (pageSource.Contains("background-image", StringComparison.OrdinalIgnoreCase))
                    Logger.Debug("Found background-image in page source");
                    
                // Check for common React/SPA patterns
                if (pageSource.Contains("__NEXT_DATA__", StringComparison.OrdinalIgnoreCase))
                    Logger.Debug("Detected Next.js application");
                if (pageSource.Contains("react", StringComparison.OrdinalIgnoreCase))
                    Logger.Debug("Detected React application");
                    
                // Check if page might require authentication
                if (pageSource.Contains("sign in", StringComparison.OrdinalIgnoreCase) || 
                    pageSource.Contains("log in", StringComparison.OrdinalIgnoreCase) ||
                    pageSource.Contains("login", StringComparison.OrdinalIgnoreCase))
                {
                    Logger.Warning("Page may require authentication - login/signin text detected");
                }
            }
            catch (Exception ex)
            {
                Logger.Warning($"Could not save debug files: {ex.Message}");
            }
        }

        private void ScrollPageAggressively(IWebDriver driver)
        {
            try
            {
                var js = (IJavaScriptExecutor)driver;
                
                // Scroll to bottom multiple times to trigger infinite scroll
                for (int i = 0; i < 3; i++)
                {
                    js.ExecuteScript("window.scrollTo(0, document.body.scrollHeight);");
                    Thread.Sleep(500);
                }
                
                // Scroll back to middle
                js.ExecuteScript("window.scrollTo(0, document.body.scrollHeight / 2);");
                Thread.Sleep(Scanning.ScrollDelayMs);
                
                // Scroll to bottom again
                js.ExecuteScript("window.scrollTo(0, document.body.scrollHeight);");
                Thread.Sleep(Scanning.ScrollDelayMs);
            }
            catch (Exception ex)
            {
                // Scroll failed - not critical, continue anyway
                Logger.Warning($"Page scroll operation failed but continuing scan: {ex.Message}");
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
                // Always extract from <img> tags
                var imgElements = driver.FindElements(By.TagName("img"));
                Logger.Debug($"Found {imgElements.Count} img elements");
                foreach (var img in imgElements)
                {
                    if (cancellationToken.IsCancellationRequested) break;
                    try
                    {
                        var src = img.GetAttribute("src");
                        if (!string.IsNullOrEmpty(src) && !src.StartsWith("data:"))
                            currentImages.Add(src);
                        
                        // Check data attributes if enabled
                        if (_settings.ThoroughScan_CheckDataAttributes)
                        {
                            foreach (var attr in Images.Attributes)
                            {
                                var attrValue = img.GetAttribute(attr);
                                if (!string.IsNullOrEmpty(attrValue) && !attrValue.StartsWith("data:"))
                                    currentImages.Add(attrValue);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Warning($"Failed to extract image attribute from element: {ex.Message}");
                    }
                }
                
                // Check video elements
                var videoElements = driver.FindElements(By.TagName("video"));
                Logger.Debug($"Found {videoElements.Count} video elements");
                foreach (var video in videoElements)
                {
                    if (cancellationToken.IsCancellationRequested) break;
                    try
                    {
                        var poster = video.GetAttribute("poster");
                        if (!string.IsNullOrEmpty(poster) && !poster.StartsWith("data:"))
                            currentImages.Add(poster);
                    }
                    catch { /* Skip */ }
                }
                
                // Advanced extraction features (optional)
                if (_settings.ThoroughScan_CheckBackgroundImages || _settings.ThoroughScan_CheckDataAttributes || 
                    _settings.ThoroughScan_CheckScriptTags || _settings.ThoroughScan_CheckShadowDOM)
                {
                    var js = (IJavaScriptExecutor)driver;
                    var extractedUrls = ExecuteAdvancedExtraction(js, cancellationToken);
                    
                    if (extractedUrls != null)
                    {
                        Logger.Debug($"JavaScript extraction found {extractedUrls.Count} URLs");
                        foreach (var urlObj in extractedUrls)
                        {
                            if (cancellationToken.IsCancellationRequested) break;
                            var url = urlObj?.ToString() ?? "";
                            if (!string.IsNullOrEmpty(url) && !url.StartsWith("data:"))
                                currentImages.Add(url);
                        }
                    }
                }
                
                Logger.Debug($"Total unique URLs collected: {currentImages.Count}");
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to extract images from page", ex);
            }

            return currentImages;
        }
        
        private System.Collections.ObjectModel.ReadOnlyCollection<object>? ExecuteAdvancedExtraction(IJavaScriptExecutor js, CancellationToken cancellationToken)
        {
            var scriptParts = new List<string>();
            
            scriptParts.Add("var urls = new Set();");
            
            // Build JavaScript based on enabled settings
            if (_settings.ThoroughScan_CheckBackgroundImages || _settings.ThoroughScan_CheckDataAttributes || _settings.ThoroughScan_CheckShadowDOM)
            {
                scriptParts.Add(@"
                    function extractFromElement(el) {");
                
                if (_settings.ThoroughScan_CheckBackgroundImages)
                {
                    scriptParts.Add(@"
                        var style = window.getComputedStyle(el);
                        var bgImage = style.backgroundImage;
                        if(bgImage && bgImage !== 'none') {
                            var match = bgImage.match(/url\(['""]?([^'"")]+)['""]?\)/);
                            if(match && match[1]) {
                                urls.add(match[1]);
                            }
                        }");
                }
                
                if (_settings.ThoroughScan_CheckDataAttributes)
                {
                    scriptParts.Add(@"
                        var attrs = ['data-src', 'data-url', 'data-image', 'data-original', 'data-lazy-src', 'data-srcset', 'href'];
                        for(var j = 0; j < attrs.length; j++) {
                            var val = el.getAttribute(attrs[j]);
                            if(val && val.length > 0 && !val.startsWith('data:')) {
                                if(val.includes('/g/gen_') || val.includes('/image') || val.includes('/img') || 
                                   val.includes('/photo') || val.includes('/media') || val.match(/\.(jpg|jpeg|png|gif|webp|bmp)/i)) {
                                    urls.add(val);
                                }
                            }
                        }");
                }
                
                if (_settings.ThoroughScan_CheckShadowDOM)
                {
                    scriptParts.Add(@"
                        if(el.shadowRoot) {
                            var shadowElements = el.shadowRoot.querySelectorAll('*');
                            for(var k = 0; k < shadowElements.length; k++) {
                                extractFromElement(shadowElements[k]);
                            }
                        }");
                }
                
                scriptParts.Add(@"
                    }
                    
                    var elements = document.querySelectorAll('*');
                    for(var i = 0; i < elements.length; i++) {
                        extractFromElement(elements[i]);
                    }");
            }
            
            // Check links
            scriptParts.Add(@"
                var links = document.querySelectorAll('a[href]');
                for(var i = 0; i < links.length; i++) {
                    var href = links[i].href;
                    if(href && (href.includes('/g/gen_') || href.includes('/image') || href.includes('/img') || href.includes('/photo'))) {
                        urls.add(href);
                    }
                }");
            
            if (_settings.ThoroughScan_CheckScriptTags)
            {
                scriptParts.Add(@"
                    var scripts = document.querySelectorAll('script');
                    for(var i = 0; i < scripts.length; i++) {
                        var content = scripts[i].textContent || scripts[i].innerHTML;
                        var matches = content.match(/https?:\/\/[^\s""'<>\\]+?\/(?:g\/gen_|image|img|photo|media)[^\s""'<>\\]*/gi);
                        if(matches) {
                            for(var j = 0; j < matches.length; j++) {
                                var url = matches[j].replace(/['"",;}\]\\]+$/, '');
                                urls.add(url);
                            }
                        }
                    }
                    
                    try {
                        var nextData = document.getElementById('__NEXT_DATA__');
                        if(nextData) {
                            var data = JSON.parse(nextData.textContent);
                            var dataStr = JSON.stringify(data);
                            var matches = dataStr.match(/https?:\/\/[^\s""\\]+?\/(?:g\/gen_|image|img|photo|media)[^\s""\\]*/gi);
                            if(matches) {
                                for(var j = 0; j < matches.length; j++) {
                                    var url = matches[j].replace(/['"",;}\]\\]+$/, '');
                                    urls.add(url);
                                }
                            }
                        }
                    } catch(e) {}
                    
                    try {
                        if(window.__INITIAL_STATE__) {
                            var dataStr = JSON.stringify(window.__INITIAL_STATE__);
                            var matches = dataStr.match(/https?:\/\/[^\s""\\]+?\/(?:g\/gen_|image|img|photo|media)[^\s""\\]*/gi);
                            if(matches) {
                                for(var j = 0; j < matches.length; j++) {
                                    urls.add(matches[j].replace(/['"",;}\]\\]+$/, ''));
                                }
                            }
                        }
                    } catch(e) {}");
            }
            
            scriptParts.Add("return Array.from(urls);");
            
            var fullScript = string.Join("\n", scriptParts);
            return js.ExecuteScript(fullScript) as System.Collections.ObjectModel.ReadOnlyCollection<object>;
        }

        private void ExtractImagesFromHtml(string renderedHtml, string baseUrl, HashSet<string> imageUrls, CancellationToken cancellationToken)
        {
            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(renderedHtml);
            var baseUri = new Uri(baseUrl);

            // BOORU MODE: First, scan for detail page links before processing individual images
            if (_settings.EnableBooruMode)
            {
                ExtractBooruDetailPageLinks(htmlDoc, baseUri, imageUrls, cancellationToken);
            }

            ExtractFromImageTags(htmlDoc, baseUri, imageUrls, cancellationToken);
            
            // Background images - only if checking background images
            if (_settings.ThoroughScan_CheckBackgroundImages)
            {
                ExtractBackgroundImages(htmlDoc, baseUri, imageUrls, cancellationToken);
            }
            
            // Regex extraction
            ExtractFromRegex(renderedHtml, imageUrls, cancellationToken);
        }

        /// <summary>
        /// Booru Mode: Scans for all detail/view page links on a Booru list page.
        /// These links follow the pattern: ?page=post&s=view&id=12345
        /// Each link will be queued for processing during download.
        /// </summary>
        private void ExtractBooruDetailPageLinks(HtmlDocument htmlDoc, Uri baseUri, HashSet<string> imageUrls, CancellationToken cancellationToken)
        {
            Logger.Info("Booru mode: Scanning for detail page links...");
            
            int detailLinksFound = 0;
            
            // Find all <a> links on the page
            var allLinks = htmlDoc.DocumentNode.SelectNodes("//a[@href]");
            if (allLinks == null)
            {
                Logger.Warning("Booru mode: No links found on page");
                return;
            }
            
            foreach (var link in allLinks)
            {
                if (cancellationToken.IsCancellationRequested) break;
                
                var href = link.GetAttributeValue("href", "");
                if (string.IsNullOrEmpty(href)) continue;
                
                // Check if this is a Booru detail/view page link
                var lowerHref = href.ToLowerInvariant();
                bool isBooruDetailPage = lowerHref.Contains("page=post") && 
                                         (lowerHref.Contains("s=view") || lowerHref.Contains("s=show"));
                
                if (isBooruDetailPage)
                {
                    // Convert to absolute URL
                    if (Uri.TryCreate(baseUri, href, out Uri? detailPageUri))
                    {
                        var detailUrl = detailPageUri.ToString();
                        
                        // Extract post ID for logging
                        var idMatch = System.Text.RegularExpressions.Regex.Match(href, @"[?&]id=(\d+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                        var postId = idMatch.Success ? idMatch.Groups[1].Value : "unknown";
                        
                        // Add to image URLs collection
                        if (imageUrls.Add(detailUrl))
                        {
                            detailLinksFound++;
                            Logger.Debug($"Booru mode: Found detail page for post ID {postId}");
                        }
                    }
                }
            }
            
            if (detailLinksFound > 0)
            {
                Logger.Info($"Booru mode: Found {detailLinksFound} detail page links");
                _updateStatus($"Booru mode: Found {detailLinksFound} post detail pages");
            }
            else
            {
                Logger.Warning("Booru mode: No detail page links found. This might not be a Booru list page, or the page structure is different.");
                _updateStatus("Booru mode: No detail pages found - check if you're on a list/search page");
            }
        }

        private void ExtractFromImageTags(HtmlDocument htmlDoc, Uri baseUri, HashSet<string> imageUrls, CancellationToken cancellationToken)
        {
            var imgNodes = htmlDoc.DocumentNode.SelectNodes("//img");
            if (imgNodes == null) return;

            foreach (var img in imgNodes)
            {
                if (cancellationToken.IsCancellationRequested) break;

                // BOORU MODE: Check if image is wrapped in a link to a detail/view page
                // NOTE: We now do this globally in ExtractBooruDetailPageLinks, but keep this as fallback
                if (_settings.EnableBooruMode)
                {
                    string? booruFullResUrl = GetBooruFullResolutionUrl(img, baseUri);
                    if (booruFullResUrl != null)
                    {
                        imageUrls.Add(booruFullResUrl);
                        continue; // Skip normal processing for Booru images
                    }
                }

                // Check if image is wrapped in a link to full-resolution
                string? fullResUrl = GetFullResolutionUrlFromParentLink(img, baseUri);
                if (fullResUrl != null)
                {
                    imageUrls.Add(fullResUrl);
                    continue;
                }

                // Check img attributes for image URLs
                var attributesToCheck = _settings.ThoroughScan_CheckDataAttributes 
                    ? Images.Attributes 
                    : new[] { "src" };
                    
                foreach (var attr in attributesToCheck)
                {
                    var src = img.GetAttributeValue(attr, "");
                    if (!string.IsNullOrEmpty(src) && !src.StartsWith("data:") &&
                        Uri.TryCreate(baseUri, src, out Uri? absoluteUri))
                    {
                        imageUrls.Add(absoluteUri.ToString());
                    }
                }
            }
        }
        
        /// <summary>
        /// Special handling for Booru-style sites where thumbnails link to detail pages.
        /// Extracts the detail page URL or derives the full-resolution image URL.
        /// 
        /// Example flow:
        /// 1. Thumbnail on list page links to: /index.php?page=post&s=view&id=6221306
        /// 2. Detail page contains sample: /samples/289/sample_HASH.jpg
        /// 3. Full resolution is at: /images/289/HASH.jpg
        /// 
        /// This method returns the detail page URL which will be processed during download.
        /// </summary>
        private string? GetBooruFullResolutionUrl(HtmlNode img, Uri baseUri)
        {
            var parentLink = img.ParentNode;
            if (parentLink?.Name != "a") return null;

            var href = parentLink.GetAttributeValue("href", "");
            if (string.IsNullOrEmpty(href)) return null;

            // Check if this is a Booru detail/view page link
            var lowerHref = href.ToLowerInvariant();
            bool isBooruDetailPage = lowerHref.Contains("page=post") && 
                                     (lowerHref.Contains("s=view") || lowerHref.Contains("s=show"));
            
            if (isBooruDetailPage)
            {
                // This is a detail page link - return it for processing during download
                if (Uri.TryCreate(baseUri, href, out Uri? detailPageUri))
                {
                    // Try to extract post ID for logging
                    var idMatch = System.Text.RegularExpressions.Regex.Match(href, @"[?&]id=(\d+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    if (idMatch.Success)
                    {
                        var postId = idMatch.Groups[1].Value;
                        Logger.Debug($"Booru: Found detail page for post ID {postId}: {detailPageUri}");
                    }
                    else
                    {
                        Logger.Debug($"Booru: Found detail page (no ID extracted): {detailPageUri}");
                    }
                    
                    return detailPageUri.ToString();
                }
            }
            else
            {
                // Not a Booru detail page - check if it's a direct image link
                bool isDirectImageLink = Images.Extensions.Any(ext => lowerHref.Contains(ext));
                
                if (isDirectImageLink && Uri.TryCreate(baseUri, href, out Uri? directUri))
                {
                    // Try to transform sample/thumbnail to full resolution
                    var urlString = directUri.ToString();
                    
                    if (urlString.Contains("/samples/", StringComparison.OrdinalIgnoreCase))
                    {
                        var fullResUrl = System.Text.RegularExpressions.Regex.Replace(
                            urlString,
                            @"/samples/(\d+)/sample_(.+)",
                            "/images/$1/$2",
                            System.Text.RegularExpressions.RegexOptions.IgnoreCase
                        );
                        Logger.Debug($"Booru: Transformed sample link to full-res: {fullResUrl}");
                        return fullResUrl;
                    }
                    else if (urlString.Contains("/thumbnails/", StringComparison.OrdinalIgnoreCase))
                    {
                        var fullResUrl = System.Text.RegularExpressions.Regex.Replace(
                            urlString,
                            @"/thumbnails/(\d+)/thumbnail_(.+)",
                            "/images/$1/$2",
                            System.Text.RegularExpressions.RegexOptions.IgnoreCase
                        );
                        Logger.Debug($"Booru: Transformed thumbnail link to full-res: {fullResUrl}");
                        return fullResUrl;
                    }
                    else
                    {
                        Logger.Debug($"Booru: Found direct image link: {urlString}");
                        return urlString;
                    }
                }
            }

            return null;
        }

        private void ExtractBackgroundImages(HtmlDocument htmlDoc, Uri baseUri, HashSet<string> imageUrls, CancellationToken cancellationToken)
        {
            var elementsWithStyle = htmlDoc.DocumentNode.SelectNodes("//*[@style]");
            if (elementsWithStyle != null)
            {
                foreach (var element in elementsWithStyle)
                {
                    if (cancellationToken.IsCancellationRequested) break;
                    
                    var style = element.GetAttributeValue("style", "");
                    if (!string.IsNullOrEmpty(style))
                    {
                        var matches = _backgroundImageRegex.Value.Matches(style);
                        foreach (Match match in matches)
                        {
                            if (match.Groups.Count > 1)
                            {
                                var url = match.Groups[1].Value;
                                if (!string.IsNullOrEmpty(url) && !url.StartsWith("data:") &&
                                    Uri.TryCreate(baseUri, url, out Uri? absoluteUri))
                                {
                                    imageUrls.Add(absoluteUri.ToString());
                                }
                            }
                        }
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
            int beforeCount = imageUrls.Count;
            
            // Use cached compiled regex for better performance - traditional image extensions
            var matches = _imageUrlRegex.Value.Matches(renderedHtml);
            foreach (Match match in matches)
            {
                if (cancellationToken.IsCancellationRequested) break;
                if (Uri.TryCreate(match.Value, UriKind.Absolute, out Uri? uri))
                    imageUrls.Add(uri.ToString());
            }
            Logger.Debug($"Traditional regex found {imageUrls.Count - beforeCount} URLs with extensions");
            
            beforeCount = imageUrls.Count;
            
            // Also search for potential image URLs without extensions (modern SPAs, CDNs)
            var potentialMatches = _potentialImageUrlRegex.Value.Matches(renderedHtml);
            foreach (Match match in potentialMatches)
            {
                if (cancellationToken.IsCancellationRequested) break;
                var url = match.Value;
                
                // Clean up the URL (remove trailing quotes, brackets, etc.)
                url = url.TrimEnd(',', ';', ')', ']', '}', '"', '\'', '\\');
                
                if (Uri.TryCreate(url, UriKind.Absolute, out Uri? uri))
                {
                    // Only add if it looks like it could be an image
                    var uriString = uri.ToString();
                    if (!uriString.EndsWith(".js", StringComparison.OrdinalIgnoreCase) &&
                        !uriString.EndsWith(".css", StringComparison.OrdinalIgnoreCase) &&
                        !uriString.EndsWith(".html", StringComparison.OrdinalIgnoreCase) &&
                        !uriString.EndsWith(".json", StringComparison.OrdinalIgnoreCase) &&
                        !uriString.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                    {
                        imageUrls.Add(uriString);
                    }
                }
            }
            Logger.Debug($"Potential image regex found {imageUrls.Count - beforeCount} URLs without extensions");
            
            // Additional specific pattern for Sora-like URLs: /g/gen_[alphanumeric]
            var soraPattern = new Regex(@"https?://[^\s""'<>\\]+?/g/gen_[a-zA-Z0-9]+", RegexOptions.IgnoreCase);
            var soraMatches = soraPattern.Matches(renderedHtml);
            
            beforeCount = imageUrls.Count;
            foreach (Match match in soraMatches)
            {
                if (cancellationToken.IsCancellationRequested) break;
                var url = match.Value.TrimEnd(',', ';', ')', ']', '}', '"', '\'', '\\');
                if (Uri.TryCreate(url, UriKind.Absolute, out Uri? uri))
                {
                    imageUrls.Add(uri.ToString());
                }
            }
            Logger.Debug($"Sora-specific pattern found {imageUrls.Count - beforeCount} URLs");
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
