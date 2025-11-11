using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;

namespace WebsiteImagePilfer.Services
{
    public class ImagePreviewLoader
    {
        private readonly HttpClient _httpClient;

        public ImagePreviewLoader(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<BitmapImage?> LoadPreviewImageAsync(string imageUrl, double columnWidth, double dpiScale)
        {
            try
            {
                // Download image data
                var imageBytes = await _httpClient.GetByteArrayAsync(imageUrl);

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

        public async Task<BitmapImage?> LoadPreviewImageFromColumnWidthAsync(string imageUrl, double columnWidth)
        {
            double dpiScale = 1.0;

            // Get DPI from current application
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                var mainWindow = Application.Current.MainWindow;
                if (mainWindow != null)
                {
                    var source = PresentationSource.FromVisual(mainWindow);
                    if (source?.CompositionTarget != null)
                    {
                        dpiScale = source.CompositionTarget.TransformToDevice.M11;
                    }
                }
            });

            return await LoadPreviewImageAsync(imageUrl, columnWidth, dpiScale);
        }
    }
}
