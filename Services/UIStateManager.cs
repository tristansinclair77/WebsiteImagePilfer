using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace WebsiteImagePilfer.Services
{
    /// <summary>
    /// Manages UI state for the main window, coordinating button states, 
    /// progress bar, and status text updates
    /// </summary>
    public class UIStateManager
    {
        private readonly Button _scanButton;
     private readonly Button _downloadButton;
        private readonly Button _downloadSelectedButton;
   private readonly Button _cancelButton;
  private readonly CheckBox _fastScanCheckBox;
 private readonly TextBlock _statusText;
        private readonly ProgressBar _progressBar;
  private readonly Func<int> _getSelectedReadyItemsCount;

        public UIStateManager(
            Button scanButton,
    Button downloadButton,
        Button downloadSelectedButton,
       Button cancelButton,
      CheckBox fastScanCheckBox,
            TextBlock statusText,
            ProgressBar progressBar,
            Func<int> getSelectedReadyItemsCount)
        {
    _scanButton = scanButton ?? throw new ArgumentNullException(nameof(scanButton));
        _downloadButton = downloadButton ?? throw new ArgumentNullException(nameof(downloadButton));
            _downloadSelectedButton = downloadSelectedButton ?? throw new ArgumentNullException(nameof(downloadSelectedButton));
            _cancelButton = cancelButton ?? throw new ArgumentNullException(nameof(cancelButton));
       _fastScanCheckBox = fastScanCheckBox ?? throw new ArgumentNullException(nameof(fastScanCheckBox));
            _statusText = statusText ?? throw new ArgumentNullException(nameof(statusText));
   _progressBar = progressBar ?? throw new ArgumentNullException(nameof(progressBar));
 _getSelectedReadyItemsCount = getSelectedReadyItemsCount ?? throw new ArgumentNullException(nameof(getSelectedReadyItemsCount));
        }

        /// <summary>
        /// Set UI to Ready state - ready to start scanning
        /// </summary>
        public void SetReadyState()
        {
            _scanButton.IsEnabled = true;
   _downloadButton.IsEnabled = false;
     _downloadSelectedButton.IsEnabled = false;
            _cancelButton.IsEnabled = false;
            _fastScanCheckBox.IsEnabled = true;
    _progressBar.Value = 0;
            UpdateStatus("Ready");
        }

    /// <summary>
        /// Set UI to Scanning state - scan in progress
        /// </summary>
        public void SetScanningState()
        {
            _scanButton.IsEnabled = false;
            _downloadButton.IsEnabled = false;
    _downloadSelectedButton.IsEnabled = false;
            _cancelButton.IsEnabled = true;
_fastScanCheckBox.IsEnabled = false;
_progressBar.Value = 0;
            UpdateStatus("Scanning webpage...");
        }

      /// <summary>
        /// Set UI to Scan Complete state - scan finished, ready to download
        /// </summary>
        public void SetScanCompleteState(int imageCount, bool hasImages)
    {
    _scanButton.IsEnabled = true;
     _downloadButton.IsEnabled = hasImages;
            _downloadSelectedButton.IsEnabled = false; // Will be updated by selection change
            _cancelButton.IsEnabled = false;
         _fastScanCheckBox.IsEnabled = true;
   _progressBar.Value = 0;
            
        if (hasImages)
   {
    UpdateStatus($"Found {imageCount} images. Click 'Download' to save them.");
      }
      else
          {
              UpdateStatus("No images found.");
    }
        }

        /// <summary>
      /// Set UI to Downloading state - download in progress
    /// </summary>
        public void SetDownloadingState()
        {
   _scanButton.IsEnabled = false;
    _downloadButton.IsEnabled = false;
            _downloadSelectedButton.IsEnabled = false;
    _cancelButton.IsEnabled = true;
    _progressBar.Value = 0;
      UpdateStatus("Starting download...");
        }

      /// <summary>
        /// Set UI to Download Complete state - download finished
     /// </summary>
        public void SetDownloadCompleteState()
        {
            _scanButton.IsEnabled = true;
        _downloadButton.IsEnabled = true;
   _cancelButton.IsEnabled = false;
   _progressBar.Value = 100;
         // Status will be set by caller with specific message
        }

      /// <summary>
      /// Set UI to Cancelled state - operation was cancelled
        /// </summary>
 public void SetCancelledState()
        {
    _scanButton.IsEnabled = true;
            _downloadButton.IsEnabled = true;
            _cancelButton.IsEnabled = false;
        _fastScanCheckBox.IsEnabled = true;
    // Status and progress will be set by caller
 }

        /// <summary>
   /// Set UI to Cancelling state - cancellation in progress
      /// </summary>
        public void SetCancellingState()
     {
  _cancelButton.IsEnabled = false;
    UpdateStatus("Cancelling...");
        }

        /// <summary>
        /// Update status text
        /// </summary>
        public void UpdateStatus(string message)
        {
   if (_statusText.Dispatcher.CheckAccess())
{
          _statusText.Text = message;
        }
            else
    {
           _statusText.Dispatcher.Invoke(() => _statusText.Text = message);
            }
        }

        /// <summary>
        /// Update progress bar value (0-100)
        /// </summary>
        public void UpdateProgress(double value)
      {
            if (_progressBar.Dispatcher.CheckAccess())
            {
     _progressBar.Value = value;
   }
        else
            {
       _progressBar.Dispatcher.Invoke(() => _progressBar.Value = value);
  }
   }

        /// <summary>
   /// Update download progress with detailed statistics
        /// </summary>
        public void UpdateDownloadProgress(int downloaded, int skipped, int duplicates, int remaining, int total)
 {
     int processed = downloaded + skipped;
    double progressValue = total > 0 ? (processed * 100.0) / total : 0;
            
    UpdateProgress(progressValue);
  UpdateStatus($"Downloaded: {downloaded} | Skipped: {skipped} (Duplicates: {duplicates}) | Remaining: {remaining}");
    }

        /// <summary>
        /// Update the enabled state of the Download Selected button based on selection
        /// </summary>
        public void UpdateDownloadSelectedButtonState()
        {
       int selectedReadyCount = _getSelectedReadyItemsCount();
    _downloadSelectedButton.IsEnabled = selectedReadyCount > 0;
        }

     /// <summary>
     /// Enable or disable the Download button
    /// </summary>
 public void SetDownloadButtonEnabled(bool enabled)
        {
      _downloadButton.IsEnabled = enabled;
        }

        /// <summary>
        /// Reset progress bar to zero
        /// </summary>
   public void ResetProgress()
        {
  UpdateProgress(0);
  }
    }
}
