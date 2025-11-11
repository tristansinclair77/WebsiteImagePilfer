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
       SetButtonStates(scan: true, download: false, downloadSelected: false, cancel: false, fastScan: true);
          UpdateProgress(0);
  UpdateStatus("Ready");
        }

    /// <summary>
        /// Set UI to Scanning state - scan in progress
        /// </summary>
        public void SetScanningState()
        {
            SetButtonStates(scan: false, download: false, downloadSelected: false, cancel: true, fastScan: false);
            UpdateProgress(0);
     UpdateStatus("Scanning webpage...");
        }

      /// <summary>
        /// Set UI to Scan Complete state - scan finished, ready to download
        /// </summary>
        public void SetScanCompleteState(int imageCount, bool hasImages)
        {
     SetButtonStates(scan: true, download: hasImages, downloadSelected: false, cancel: false, fastScan: true);
      UpdateProgress(0);
            UpdateStatus(hasImages ? $"Found {imageCount} images. Click 'Download' to save them." : "No images found.");
     }

        /// <summary>
      /// Set UI to Downloading state - download in progress
    /// </summary>
        public void SetDownloadingState()
        {
            SetButtonStates(scan: false, download: false, downloadSelected: false, cancel: true);
            UpdateProgress(0);
        UpdateStatus("Starting download...");
   }

      /// <summary>
        /// Set UI to Download Complete state - download finished
     /// </summary>
        public void SetDownloadCompleteState()
        {
   SetButtonStates(scan: true, download: true, cancel: false);
        UpdateProgress(100);
        }

      /// <summary>
      /// Set UI to Cancelled state - operation was cancelled
        /// </summary>
 public void SetCancelledState()
        {
   SetButtonStates(scan: true, download: true, cancel: false, fastScan: true);
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
     public void UpdateStatus(string message) => 
  InvokeOnUIThread(() => _statusText.Text = message);

        /// <summary>
        /// Update progress bar value (0-100)
        /// </summary>
        public void UpdateProgress(double value) => 
      InvokeOnUIThread(() => _progressBar.Value = value);

        /// <summary>
   /// Update download progress with detailed statistics
        /// </summary>
        public void UpdateDownloadProgress(int downloaded, int skipped, int duplicates, int remaining, int total)
        {
       double progressValue = total > 0 ? (downloaded + skipped) * 100.0 / total : 0;
      UpdateProgress(progressValue);
            UpdateStatus($"Downloaded: {downloaded} | Skipped: {skipped} (Duplicates: {duplicates}) | Remaining: {remaining}");
        }

public void UpdateDownloadSelectedButtonState() => 
  _downloadSelectedButton.IsEnabled = _getSelectedReadyItemsCount() > 0;

        /// <summary>
        /// Enable or disable the Download button
    /// </summary>
 public void SetDownloadButtonEnabled(bool enabled) => 
         _downloadButton.IsEnabled = enabled;

  /// <summary>
        /// Reset progress bar to zero
        /// </summary>
   public void ResetProgress() => UpdateProgress(0);

      private void SetButtonStates(
     bool? scan = null,
     bool? download = null,
     bool? downloadSelected = null,
            bool? cancel = null,
            bool? fastScan = null)
        {
        if (scan.HasValue) _scanButton.IsEnabled = scan.Value;
            if (download.HasValue) _downloadButton.IsEnabled = download.Value;
    if (downloadSelected.HasValue) _downloadSelectedButton.IsEnabled = downloadSelected.Value;
      if (cancel.HasValue) _cancelButton.IsEnabled = cancel.Value;
            if (fastScan.HasValue) _fastScanCheckBox.IsEnabled = fastScan.Value;
}

        private void InvokeOnUIThread(Action action)
        {
            if (_statusText.Dispatcher.CheckAccess())
  action();
    else
            _statusText.Dispatcher.Invoke(action);
        }
    }
}
