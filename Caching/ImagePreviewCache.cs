using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using WebsiteImagePilfer.Services;

namespace WebsiteImagePilfer.Caching
{
    /// <summary>
    /// Two-tier caching system for image previews (memory + disk).
    /// Provides fast access to frequently accessed previews and persistent caching across sessions.
    /// </summary>
    public class ImagePreviewCache
    {
        private readonly LruCache<string, BitmapImage> _memoryCache;
        private readonly string _diskCacheFolder;
        private readonly bool _useDiskCache;

        /// <summary>
        /// Initializes a new image preview cache.
        /// </summary>
        /// <param name="maxMemoryItems">Maximum number of images to keep in memory (default: 500).</param>
        /// <param name="useDiskCache">Whether to use disk caching (default: true).</param>
        public ImagePreviewCache(int maxMemoryItems = 500, bool useDiskCache = true)
        {
  _memoryCache = new LruCache<string, BitmapImage>(maxMemoryItems);
            _useDiskCache = useDiskCache;
       
            if (_useDiskCache)
            {
                _diskCacheFolder = Path.Combine(AppContext.BaseDirectory, "cache", "previews");
           try
       {
        Directory.CreateDirectory(_diskCacheFolder);
     }
  catch (Exception ex)
        {
         Logger.Error($"Failed to create disk cache folder: {_diskCacheFolder}", ex);
_useDiskCache = false;
              }
        }
            else
   {
   _diskCacheFolder = string.Empty;
        }
        }

        /// <summary>
        /// Gets the number of items currently in memory cache.
        /// </summary>
   public int MemoryCacheCount => _memoryCache.Count;

        /// <summary>
        /// Gets or loads an image preview with two-tier caching.
        /// Checks memory cache first, then disk cache, and finally calls the loader function.
        /// </summary>
        /// <param name="url">The image URL (used as cache key).</param>
 /// <param name="decodeWidth">The target decode width for caching.</param>
 /// <param name="loader">Function to load the image if not in cache.</param>
     /// <returns>The cached or newly loaded BitmapImage, or null if loading fails.</returns>
      public async Task<BitmapImage?> GetOrLoadAsync(string url, int decodeWidth, Func<Task<BitmapImage?>> loader)
        {
            if (string.IsNullOrEmpty(url))
       return null;

      // Generate cache key including decode width to handle different resolutions
    string cacheKey = GenerateCacheKey(url, decodeWidth);

            // 1. Check memory cache
 if (_memoryCache.TryGetValue(cacheKey, out var cachedImage))
{
        return cachedImage;
   }

       // 2. Check disk cache
            if (_useDiskCache)
            {
      var diskPath = GetDiskCachePath(cacheKey);
             if (File.Exists(diskPath))
                {
    try
      {
    var bitmap = await LoadFromDiskAsync(diskPath, decodeWidth);
    if (bitmap != null)
 {
 _memoryCache.Add(cacheKey, bitmap);
      return bitmap;
               }
     }
        catch (Exception ex)
         {
    Logger.Error($"Failed to load from disk cache: {diskPath}", ex);
        // Delete corrupted cache file
       try { File.Delete(diskPath); } catch { }
           }
     }
    }

         // 3. Load from network using provided loader
      var loaded = await loader();
       if (loaded != null)
            {
         _memoryCache.Add(cacheKey, loaded);
  
     if (_useDiskCache)
      {
                 _ = SaveToDiskAsync(GetDiskCachePath(cacheKey), loaded);
 }
            }

      return loaded;
   }

     /// <summary>
        /// Clears both memory and disk caches.
        /// </summary>
      public void ClearAll()
  {
            _memoryCache.Clear();

if (_useDiskCache)
    {
      try
          {
 if (Directory.Exists(_diskCacheFolder))
   {
              Directory.Delete(_diskCacheFolder, recursive: true);
      Directory.CreateDirectory(_diskCacheFolder);
     }
         }
        catch (Exception ex)
         {
            Logger.Error("Failed to clear disk cache", ex);
   }
        }
     }

        /// <summary>
        /// Clears only the memory cache, keeping disk cache intact.
        /// </summary>
        public void ClearMemoryCache()
        {
            _memoryCache.Clear();
        }

        /// <summary>
        /// Generates a cache key from URL and decode width.
        /// </summary>
        private string GenerateCacheKey(string url, int decodeWidth)
     {
            return $"{url}_{decodeWidth}";
        }

        /// <summary>
        /// Gets the disk cache file path for a cache key.
        /// </summary>
      private string GetDiskCachePath(string cacheKey)
   {
            // Use SHA256 hash to create safe filename from cache key
         using var sha256 = SHA256.Create();
            var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(cacheKey));
            var hashString = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
          return Path.Combine(_diskCacheFolder, $"{hashString}.cache");
  }

   /// <summary>
        /// Loads a BitmapImage from disk cache.
   /// </summary>
        private async Task<BitmapImage?> LoadFromDiskAsync(string diskPath, int decodeWidth)
        {
          return await Task.Run(() =>
            {
   try
         {
 var bitmap = new BitmapImage();
       bitmap.BeginInit();
     bitmap.CacheOption = BitmapCacheOption.OnLoad;
      bitmap.UriSource = new Uri(diskPath, UriKind.Absolute);
  bitmap.DecodePixelWidth = decodeWidth;
      bitmap.EndInit();
    bitmap.Freeze();
      return bitmap;
    }
         catch
       {
          return null;
            }
            }).ConfigureAwait(false);
        }

/// <summary>
        /// Saves a BitmapImage to disk cache asynchronously.
        /// Fire-and-forget operation that doesn't block the caller.
        /// </summary>
     private async Task SaveToDiskAsync(string diskPath, BitmapImage bitmap)
     {
            try
            {
 await Task.Run(() =>
   {
    var encoder = new PngBitmapEncoder();
                  encoder.Frames.Add(BitmapFrame.Create(bitmap));
           
   using var fileStream = new FileStream(diskPath, FileMode.Create, FileAccess.Write);
      encoder.Save(fileStream);
       }).ConfigureAwait(false);
            }
 catch (Exception ex)
    {
         Logger.Error($"Failed to save to disk cache: {diskPath}", ex);
   }
        }
    }
}
