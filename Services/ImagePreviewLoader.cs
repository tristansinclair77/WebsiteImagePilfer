using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using WebsiteImagePilfer.Constants;
using static WebsiteImagePilfer.Constants.AppConstants;

namespace WebsiteImagePilfer.Services
{
    public class ImagePreviewLoader
    {
  private readonly HttpClient _httpClient;
  private double _cachedDpiScale = 1.0;
        private bool _dpiScaleCached = false;

        public ImagePreviewLoader(HttpClient httpClient) => _httpClient = httpClient;

public async Task<BitmapImage?> LoadPreviewImageAsync(string imageUrl, double columnWidth, double dpiScale)
        {
            try
       {
      var imageBytes = await _httpClient.GetByteArrayAsync(imageUrl).ConfigureAwait(false);
       int decodeWidth = CalculateDecodeWidth(columnWidth, dpiScale);

   var bitmap = new BitmapImage();
     using (var stream = new MemoryStream(imageBytes))
        {
  bitmap.BeginInit();
     bitmap.CacheOption = BitmapCacheOption.OnLoad;
       bitmap.StreamSource = stream;
     bitmap.DecodePixelWidth = decodeWidth;
      bitmap.EndInit();
     bitmap.Freeze();
     }

      return bitmap;
 }
  catch (Exception ex)
       {
     Logger.Error($"Preview load failed for {imageUrl}", ex);
     return null;
   }
   }

        public async Task<BitmapImage?> LoadPreviewImageFromColumnWidthAsync(string imageUrl, double columnWidth)
        {
       double dpiScale = await GetOrCacheDpiScaleAsync().ConfigureAwait(false);
            return await LoadPreviewImageAsync(imageUrl, columnWidth, dpiScale).ConfigureAwait(false);
}

   private async Task<double> GetOrCacheDpiScaleAsync()
     {
    if (_dpiScaleCached)
       return _cachedDpiScale;

    await Application.Current.Dispatcher.InvokeAsync(() =>
 {
   var mainWindow = Application.Current.MainWindow;
    if (mainWindow != null)
      {
     var source = PresentationSource.FromVisual(mainWindow);
       if (source?.CompositionTarget != null)
  {
      _cachedDpiScale = source.CompositionTarget.TransformToDevice.M11;
       _dpiScaleCached = true;
   }
  }
    });

       return _cachedDpiScale;
 }

   private int CalculateDecodeWidth(double columnWidth, double dpiScale) => 
          (int)Math.Max(Preview.MinDecodeWidth, (columnWidth - 4) * dpiScale * Preview.QualityMultiplier);
    }
}
