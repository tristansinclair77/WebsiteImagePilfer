using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using WebsiteImagePilfer.Models;
using WebsiteImagePilfer.Helpers;
using WebsiteImagePilfer.Constants;
using static WebsiteImagePilfer.Constants.AppConstants;
using IOPath = System.IO.Path;

namespace WebsiteImagePilfer.Services
{
    public class ImageDownloader
    {
        private readonly HttpClient _httpClient;
        private readonly DownloadSettings _settings;
        private readonly string _downloadFolder;

        public ImageDownloader(HttpClient httpClient, DownloadSettings settings, string downloadFolder)
        {
            _httpClient = httpClient;
            _settings = settings;
            _downloadFolder = downloadFolder;
        }

        public async Task DownloadSingleItemAsync(ImageDownloadItem item, CancellationToken cancellationToken, bool forceDownload = false)
        {
            try
            {
                item.Status = Status.Checking;
                var startTime = DateTime.Now;

                // Special handling for Booru detail pages
                var urlToDownload = item.Url;
                bool usedBackup = false;
                
                if (_settings.EnableBooruMode && IsBooruDetailPageUrl(item.Url))
                {
                    Logger.Info($"Booru mode: Fetching detail page to find full-res image: {item.Url}");
                    var extractedImageUrl = await ExtractImageFromBooruDetailPageAsync(item.Url, cancellationToken).ConfigureAwait(false);
                    
                    if (extractedImageUrl != null)
                    {
                        urlToDownload = extractedImageUrl;
                        Logger.Info($"Booru mode: Found image URL: {urlToDownload}");
                    }
                    else
                    {
                        Logger.Warning($"Booru mode: Could not extract image from detail page, using original URL");
                        usedBackup = true;
                    }
                }
                else
                {
                    var result = await ResolveDownloadUrlAsync(item.Url, cancellationToken).ConfigureAwait(false);
                    urlToDownload = result.url;
                    usedBackup = result.usedBackup;
                }

                // Use existing filename or extract from URL
                if (string.IsNullOrEmpty(item.FileName) || item.FileName.StartsWith("image_", StringComparison.Ordinal))
                {
                    // Try to get filename with content type detection for URLs without extensions
                    item.FileName = await FileNameExtractor.ExtractFromUrlWithContentTypeAsync(urlToDownload, _httpClient, cancellationToken).ConfigureAwait(false);
                }

                // Check file type filters
                if (!PassesFileTypeFilter(item.FileName, out string? filterReason))
                {
                    item.Status = filterReason!;
                    item.FileName = $"{item.FileName} (Filtered: {IOPath.GetExtension(item.FileName)})";
                    return;
                }

                // Check for duplicate
                var filePath = IOPath.Combine(_downloadFolder, item.FileName);
                if (File.Exists(filePath))
                {
                    if (!forceDownload)
                    {
                        item.Status = Status.Duplicate;
                        return;
                    }
                    
                    // Force download - generate unique filename
                    filePath = GenerateUniqueFilePath(filePath);
                    item.FileName = IOPath.GetFileName(filePath);
                }

                item.Status = Status.Downloading;
                var downloadStart = DateTime.Now;

                // Download the image
                var imageBytes = await _httpClient.GetByteArrayAsync(urlToDownload, cancellationToken).ConfigureAwait(false);
                var downloadElapsed = (DateTime.Now - downloadStart).TotalMilliseconds;

                // Filter by size if enabled
                if (_settings.FilterBySize && imageBytes.Length < _settings.MinimumImageSize)
                {
                    item.Status = Status.SkippedSize;
                    item.FileName = $"{item.FileName} ({imageBytes.Length} bytes)";
                    return;
                }

                await File.WriteAllBytesAsync(filePath, imageBytes, cancellationToken).ConfigureAwait(false);

                // Set thumbnail path if enabled
                if (_settings.ShowThumbnails)
                {
                    try 
                    { 
                        item.ThumbnailPath = filePath; 
                    }
                    catch (Exception ex)
                    { 
                        // Thumbnail generation failed - not critical, continue without it
                        Logger.Warning($"Failed to set thumbnail path for {item.FileName}: {ex.Message}");
                    }
                }

                item.Status = usedBackup ? Status.Backup : Status.Done;
                item.ErrorMessage = "";

                var totalElapsed = (DateTime.Now - startTime).TotalMilliseconds;
                Logger.Info($"Downloaded {item.FileName} in {totalElapsed}ms (download: {downloadElapsed}ms)");
            }
            catch (OperationCanceledException)
            {
                item.Status = Status.Cancelled;
                throw;
            }
            catch (HttpRequestException ex)
            {
                item.Status = Status.Failed;
                item.ErrorMessage = $"Network error: {ex.Message}";
                Logger.Error($"HTTP error for {item.FileName}", ex);
            }
            catch (Exception ex)
            {
                item.Status = Status.Failed;
                item.ErrorMessage = ex.Message;
                Logger.Error($"Error downloading {item.FileName}", ex);
            }
        }

        /// <summary>
        /// Checks if a URL is a Booru detail/view page rather than a direct image URL.
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
            
            if (isDetailPage)
            {
                Logger.Debug($"Booru: Detected detail page URL: {url}");
                return true;
            }
            
            return false;
        }

        /// <summary>
        /// Fetches a Booru detail page and extracts the full-resolution image URL.
        /// Handles the sample ? full-res transformation.
        /// 
        /// On Booru sites, the detail page initially shows a sample image, but contains
        /// a link (often labeled "here" or "Original image") that reveals the full-res URL.
        /// The full-res URL follows the pattern: /images/XX/HASH.ext (without "sample_" prefix)
        /// </summary>
        private async Task<string?> ExtractImageFromBooruDetailPageAsync(string detailPageUrl, CancellationToken cancellationToken)
        {
            try
            {
                Logger.Info($"Booru: Fetching detail page HTML from: {detailPageUrl}");
                
                // Fetch the detail page HTML
                var response = await _httpClient.GetAsync(detailPageUrl, cancellationToken).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                
                var html = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                
                Logger.Debug($"Booru: Received HTML ({html.Length} characters)");
                
                // Parse HTML to find the full-resolution image link
                var htmlDoc = new HtmlAgilityPack.HtmlDocument();
                htmlDoc.LoadHtml(html);
                
                // PRIORITY Strategy 1: Look for ANY /images/ URL in the HTML (most reliable)
                // This catches the full-res URL regardless of where it appears
                var imagesRegex = new System.Text.RegularExpressions.Regex(
                    @"https?://[^""'\s<>]+?/images/\d+/[^""'\s<>]+\.(jpg|jpeg|png|gif|webp|bmp)",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase
                );
                
                var imagesMatches = imagesRegex.Matches(html);
                if (imagesMatches.Count > 0)
                {
                    // Take the first match (should be the main image)
                    var fullResUrl = imagesMatches[0].Value;
                    // Clean up any query parameters that might have been included
                    fullResUrl = fullResUrl.Split('?')[0];
                    
                    Logger.Info($"Booru: Found /images/ URL via regex: {fullResUrl}");
                    return fullResUrl;
                }
                
                // Strategy 2: Look for sample image in <img> tag and transform it
                var sampleImg = htmlDoc.DocumentNode.SelectSingleNode("//img[contains(@src, '/samples/') or contains(@src, 'sample')]");
                if (sampleImg != null)
                {
                    var src = sampleImg.GetAttributeValue("src", "");
                    if (!string.IsNullOrEmpty(src))
                    {
                        // Remove query parameters first
                        src = src.Split('?')[0];
                        
                        if (Uri.TryCreate(new Uri(detailPageUrl), src, out Uri? sampleUri))
                        {
                            var sampleUrl = sampleUri.ToString();
                            
                            Logger.Debug($"Booru: Found sample image: {sampleUrl}");
                            
                            // Transform sample to full-res: /samples/XX/sample_HASH.ext ? /images/XX/HASH.ext
                            if (sampleUrl.Contains("/samples/", StringComparison.OrdinalIgnoreCase))
                            {
                                var fullResUrl = System.Text.RegularExpressions.Regex.Replace(
                                    sampleUrl,
                                    @"/samples/(\d+)/sample_(.+)",
                                    "/images/$1/$2",
                                    System.Text.RegularExpressions.RegexOptions.IgnoreCase
                                );
                                
                                Logger.Info($"Booru: Transformed sample URL to full-res: {fullResUrl}");
                                return fullResUrl;
                            }
                        }
                    }
                }
                
                // Strategy 3: Look for links with text like "Original", "here", "View full size", etc.
                var originalLinks = htmlDoc.DocumentNode.SelectNodes(
                    "//a[contains(translate(text(), 'ABCDEFGHIJKLMNOPQRSTUVWXYZ', 'abcdefghijklmnopqrstuvwxyz'), 'original') or " +
                    "contains(translate(text(), 'ABCDEFGHIJKLMNOPQRSTUVWXYZ', 'abcdefghijklmnopqrstuvwxyz'), 'here') or " +
                    "contains(translate(text(), 'ABCDEFGHIJKLMNOPQRSTUVWXYZ', 'abcdefghijklmnopqrstuvwxyz'), 'full') or " +
                    "contains(@href, '/images/')]"
                );
                
                if (originalLinks != null)
                {
                    foreach (var link in originalLinks)
                    {
                        var href = link.GetAttributeValue("href", "");
                        if (!string.IsNullOrEmpty(href) && href != "#")
                        {
                            // Check if this is a fragment identifier with onclick handler
                            // In that case, we need to look for the actual image URL elsewhere
                            if (href == "#" || href.StartsWith("#"))
                            {
                                // The image URL should be in the onclick attribute or nearby
                                var onclick = link.GetAttributeValue("onclick", "");
                                if (!string.IsNullOrEmpty(onclick))
                                {
                                    // Extract URL from onclick JavaScript
                                    var urlMatch = System.Text.RegularExpressions.Regex.Match(
                                        onclick,
                                        @"[""']([^""']+/images/[^""']+)[""']",
                                        System.Text.RegularExpressions.RegexOptions.IgnoreCase
                                    );
                                    
                                    if (urlMatch.Success)
                                    {
                                        var url = urlMatch.Groups[1].Value;
                                        if (Uri.TryCreate(new Uri(detailPageUrl), url, out Uri? imageUri))
                                        {
                                            Logger.Info($"Booru: Found image URL in onclick: {imageUri}");
                                            return imageUri.ToString();
                                        }
                                    }
                                }
                                continue;
                            }
                            
                            // Regular link with href
                            if (Uri.TryCreate(new Uri(detailPageUrl), href, out Uri? fullUri))
                            {
                                var urlString = fullUri.ToString();
                                if (urlString.Contains("/images/", StringComparison.OrdinalIgnoreCase))
                                {
                                    Logger.Info($"Booru: Found /images/ link: {urlString}");
                                    return urlString;
                                }
                            }
                        }
                    }
                }
                
                // Strategy 4: Look for img with id="image" (common on Booru sites)
                var imageById = htmlDoc.DocumentNode.SelectSingleNode("//img[@id='image']");
                if (imageById != null)
                {
                    var src = imageById.GetAttributeValue("src", "");
                    if (!string.IsNullOrEmpty(src))
                    {
                        src = src.Split('?')[0]; // Remove query parameters
                        
                        if (Uri.TryCreate(new Uri(detailPageUrl), src, out Uri? imageUri))
                        {
                            var urlString = imageUri.ToString();
                            Logger.Debug($"Booru: Found image via id='image': {urlString}");
                            
                            // Still transform if it's a sample
                            if (urlString.Contains("/samples/", StringComparison.OrdinalIgnoreCase))
                            {
                                urlString = System.Text.RegularExpressions.Regex.Replace(
                                    urlString,
                                    @"/samples/(\d+)/sample_(.+)",
                                    "/images/$1/$2",
                                    System.Text.RegularExpressions.RegexOptions.IgnoreCase
                                );
                                Logger.Info($"Booru: Transformed id='image' to full-res: {urlString}");
                            }
                            
                            return urlString;
                        }
                    }
                }
                
                // Strategy 5: Look in JavaScript variables or data attributes
                var scriptMatches = System.Text.RegularExpressions.Regex.Matches(
                    html,
                    @"(?:image_url|original_url|file_url|image_src)[""']?\s*[:=]\s*[""']([^""']+)[""']",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase
                );
                
                foreach (System.Text.RegularExpressions.Match match in scriptMatches)
                {
                    var url = match.Groups[1].Value;
                    if (url.Contains("/images/", StringComparison.OrdinalIgnoreCase))
                    {
                        if (Uri.TryCreate(new Uri(detailPageUrl), url, out Uri? imageUri))
                        {
                            Logger.Info($"Booru: Found image URL in JavaScript: {imageUri}");
                            return imageUri.ToString();
                        }
                    }
                }
                
                Logger.Warning($"Booru: Could not find full-res image URL in detail page after trying all strategies");
                Logger.Warning($"Booru: Strategies attempted: /images/ regex, sample transformation, link text search, #image element, JavaScript variables");
                
                // Save debug HTML if needed
                if (_settings.ThoroughScan_SaveDebugFiles)
                {
                    try
                    {
                        var debugFolder = IOPath.Combine(AppContext.BaseDirectory, "debug");
                        if (!Directory.Exists(debugFolder))
                            Directory.CreateDirectory(debugFolder);
                        
                        var debugFile = IOPath.Combine(debugFolder, $"booru_detail_{DateTime.Now:yyyyMMdd_HHmmss}.html");
                        File.WriteAllText(debugFile, html);
                        Logger.Info($"Booru: Saved detail page HTML to: {debugFile}");
                        
                        // Also log a snippet of the HTML for quick inspection
                        Logger.Debug($"Booru: HTML snippet (first 500 chars): {html.Substring(0, Math.Min(500, html.Length))}");
                    }
                    catch (Exception ex)
                    {
                        Logger.Warning($"Booru: Could not save debug HTML: {ex.Message}");
                    }
                }
                
                return null;
            }
            catch (Exception ex)
            {
                Logger.Error($"Booru: Failed to extract image from detail page: {detailPageUrl}", ex);
                return null;
            }
        }

        /// <summary>
        /// Generates a unique file path by appending a number to the filename if it already exists.
        /// If the filename already has a number suffix like "(1)", it will increment that number.
        /// </summary>
        private string GenerateUniqueFilePath(string originalPath)
        {
            if (!File.Exists(originalPath))
                return originalPath;

            var directory = IOPath.GetDirectoryName(originalPath) ?? _downloadFolder;
            var fileNameWithoutExt = IOPath.GetFileNameWithoutExtension(originalPath);
            var extension = IOPath.GetExtension(originalPath);
            
            // Check if filename already has a number suffix like "(1)", "(2)", etc.
            var match = System.Text.RegularExpressions.Regex.Match(fileNameWithoutExt, @"^(.+?)\s*\((\d+)\)$");
            
            string baseName;
            int counter;
            
            if (match.Success)
            {
                // File already has a number - use the base name and start from the existing number
                baseName = match.Groups[1].Value;
                counter = int.Parse(match.Groups[2].Value);
            }
            else
            {
                // No existing number - start from 1
                baseName = fileNameWithoutExt;
                counter = 1;
            }
            
            string newPath;
            
            do
            {
                newPath = IOPath.Combine(directory, $"{baseName} ({counter}){extension}");
                counter++;
            }
            while (File.Exists(newPath));
            
            return newPath;
        }

        private async Task<(string url, bool usedBackup)> ResolveDownloadUrlAsync(string originalUrl, CancellationToken cancellationToken)
        {
            if (_settings.SkipFullResolutionCheck)
                return (originalUrl, false);

            try
            {
                var fullResUrl = await TryFindFullResolutionUrlAsync(originalUrl, cancellationToken);

                if (fullResUrl == null)
                    return (originalUrl, true); // No full-res found, use original as backup

                if (fullResUrl == originalUrl)
                    return (originalUrl, false); // URL is already full-res

                return (fullResUrl, false); // Found different full-res URL
            }
            catch (Exception ex)
            {
                // Error finding full-res - use backup URL
                Logger.Warning($"Error finding full resolution URL for {originalUrl}, using backup: {ex.Message}");
                return (originalUrl, true);
            }
        }

        private async Task<string?> TryFindFullResolutionUrlAsync(string previewUrl, CancellationToken cancellationToken)
        {
            try
            {
                // BOORU MODE - SPECIALIZED MULTI-TIER HANDLING
                if (_settings.EnableBooruMode && IsBooruUrl(previewUrl, out string? booruResult))
                {
                    Logger.Info($"Booru mode: Transformed URL from {previewUrl} to {booruResult}");
                    return booruResult;
                }

                // KEMONO.CR SPECIFIC PATTERN - NO HEAD REQUEST NEEDED
                if (IsKemonoUrl(previewUrl, out string? kemonoResult))
                    return kemonoResult;

                // Pattern 1: Remove "/thumbnail/" from path
                if (previewUrl.Contains("/thumbnail/", StringComparison.Ordinal))
                {
                    var fullResUrl = previewUrl.Replace("/thumbnail/", "/");
                    if (await TestUrlExistsAsync(fullResUrl, cancellationToken).ConfigureAwait(false))
                        return fullResUrl;
                }

                // Pattern 2: Remove size suffixes
                foreach (var pattern in Images.SizePatterns)
                {
                    if (previewUrl.Contains(pattern, StringComparison.Ordinal))
                    {
                        var fullResUrl = previewUrl.Replace(pattern, "");
                        if (await TestUrlExistsAsync(fullResUrl, cancellationToken).ConfigureAwait(false))
                            return fullResUrl;
                    }
                }

                // No transformation needed/found - return original URL
                return previewUrl;
            }
            catch (Exception ex)
            {
                Logger.Error($"TryFindFullResolutionUrlAsync exception", ex);
                return previewUrl;
            }
        }

        /// <summary>
        /// Detects and transforms Booru-style image URLs from sample to full resolution.
        /// Supports: safebooru.org, gelbooru.com, danbooru.donmai.us, and similar sites.
        /// 
        /// Booru sites typically have a 3-tier structure:
        /// 1. Thumbnail: /thumbnails/289/thumbnail_86de6d5b7e62bc80ab3122f584d5ecf5cf7a8dd5.jpg
        /// 2. Sample: /samples/289/sample_86de6d5b7e62bc80ab3122f584d5ecf5cf7a8dd5.jpg
        /// 3. Full: /images/289/86de6d5b7e62bc80ab3122f584d5ecf5cf7a8dd5.jpg
        /// 
        /// This method transforms samples/thumbnails to full resolution.
        /// </summary>
        private bool IsBooruUrl(string url, out string? result)
        {
            result = null;
            
            // Check if URL is from a known Booru site
            var lowerUrl = url.ToLowerInvariant();
            bool isBooruSite = lowerUrl.Contains("safebooru") || 
                              lowerUrl.Contains("gelbooru") || 
                              lowerUrl.Contains("danbooru") ||
                              lowerUrl.Contains("konachan") ||
                              lowerUrl.Contains("yande.re") ||
                              lowerUrl.Contains("sankaku");
            
            if (!isBooruSite)
                return false;
            
            // Pattern 1: Transform /samples/XXX/sample_HASH.ext ? /images/XXX/HASH.ext
            if (url.Contains("/samples/", StringComparison.OrdinalIgnoreCase))
            {
                result = System.Text.RegularExpressions.Regex.Replace(
                    url, 
                    @"/samples/(\d+)/sample_(.+)", 
                    "/images/$1/$2",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase
                );
                
                if (result != url)
                {
                    Logger.Debug($"Booru: Transformed sample to full resolution");
                    return true;
                }
            }
            
            // Pattern 2: Transform /thumbnails/XXX/thumbnail_HASH.ext ? /images/XXX/HASH.ext
            if (url.Contains("/thumbnails/", StringComparison.OrdinalIgnoreCase))
            {
                result = System.Text.RegularExpressions.Regex.Replace(
                    url,
                    @"/thumbnails/(\d+)/thumbnail_(.+)",
                    "/images/$1/$2",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase
                );
                
                if (result != url)
                {
                    Logger.Debug($"Booru: Transformed thumbnail to full resolution");
                    return true;
                }
            }
            
            // Pattern 3: Already full resolution - just validate it's an /images/ URL
            if (url.Contains("/images/", StringComparison.OrdinalIgnoreCase))
            {
                result = url;
                Logger.Debug($"Booru: URL is already full resolution");
                return true;
            }
            
            // Booru site detected but no known pattern matched
            Logger.Debug($"Booru site detected but URL doesn't match known patterns: {url}");
            return false;
        }

        private bool IsKemonoUrl(string url, out string? result)
        {
            if (!url.Contains("kemono.cr", StringComparison.OrdinalIgnoreCase))
            {
                result = null;
                return false;
            }

            if (url.Contains("/thumbnail/", StringComparison.Ordinal))
            {
                // Transform preview to full-res
                result = url.Replace("/thumbnail/", "/").Replace("img.kemono.cr", "n4.kemono.cr");
                Logger.Debug("Kemono.cr preview detected, transforming to full-res URL");
                return true;
            }

            if (url.Contains("n4.kemono.cr", StringComparison.Ordinal) || url.Contains("n5.kemono.cr", StringComparison.Ordinal))
            {
                // Already a full-res URL
                result = url;
                Logger.Debug("Kemono.cr full-res URL detected (already full-res)");
                return true;
            }

            result = null;
            return false;
        }

        private async Task<bool> TestUrlExistsAsync(string url, CancellationToken cancellationToken)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Head, url);
                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(Network.HeadRequestTimeoutSeconds));
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

                var response = await _httpClient.SendAsync(request, linkedCts.Token).ConfigureAwait(false);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                // URL test failed - assume URL doesn't exist
                Logger.Debug($"URL existence test failed for {url}: {ex.Message}");
                return false;
            }
        }

        private bool PassesFileTypeFilter(string fileName, out string? filterReason)
        {
            var extension = IOPath.GetExtension(fileName).ToLowerInvariant();

            // If no file types are specified in AllowedFileTypes, allow all types
            if (_settings.AllowedFileTypes.Count == 0)
            {
                filterReason = null;
                return true;
            }

            // If file types are specified, only allow those types
            if (_settings.AllowedFileTypes.Contains(extension))
            {
                filterReason = null;
                return true;
            }

            // File type not in allowed list
            filterReason = $"? Skipped ({extension})";
            return false;
        }
    }
}
