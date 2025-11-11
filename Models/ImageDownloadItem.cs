using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media.Imaging;

namespace WebsiteImagePilfer.Models
{
    public class ImageDownloadItem : INotifyPropertyChanged
    {
        private string _url = "";
        private string _status = "";
        private string _fileName = "";
        private string? _thumbnailPath;
        private string _errorMessage = "";
        private BitmapImage? _previewImage;

        public string Url
        {
            get => _url;
            set => SetProperty(ref _url, value);
        }

        public string Status
        {
            get => _status;
            set => SetProperty(ref _status, value);
        }

        public string FileName
        {
            get => _fileName;
            set => SetProperty(ref _fileName, value);
        }

        public string? ThumbnailPath
        {
            get => _thumbnailPath;
            set => SetProperty(ref _thumbnailPath, value);
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            set => SetProperty(ref _errorMessage, value);
        }

        public BitmapImage? PreviewImage
        {
            get => _previewImage;
            set => SetProperty(ref _previewImage, value);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
