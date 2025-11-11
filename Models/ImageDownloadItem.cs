using System.ComponentModel;
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

        public string ErrorMessage
  {
          get => _errorMessage;
          set { _errorMessage = value; OnPropertyChanged(nameof(ErrorMessage)); }
        }

public BitmapImage? PreviewImage
        {
            get => _previewImage;
        set { _previewImage = value; OnPropertyChanged(nameof(PreviewImage)); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged(string propertyName)
     {
         PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
