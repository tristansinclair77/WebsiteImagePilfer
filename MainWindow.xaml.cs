using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.ComponentModel;
using WebsiteImagePilfer.ViewModels;
using WebsiteImagePilfer.Models;
using WebsiteImagePilfer.Constants;
using static WebsiteImagePilfer.Constants.AppConstants;

namespace WebsiteImagePilfer
{
    /// <summary>
    /// Main window view - handles only view-specific concerns.
    /// All business logic is in MainWindowViewModel.
    /// </summary>
    public partial class MainWindow : Window
  {
   private readonly MainWindowViewModel _viewModel;
     private double _lastPreviewColumnWidth = 0;
    private System.Timers.Timer? _columnResizeTimer;

  public MainWindow()
        {
       InitializeComponent();

            // Create and set ViewModel
  _viewModel = new MainWindowViewModel();
   DataContext = _viewModel;

    SetupUIMonitoring();
   }

 private void SetupUIMonitoring()
 {
        // Initialize preview column to minimum decode width
        PreviewColumn.Width = Preview.MinDecodeWidth;
    _lastPreviewColumnWidth = Preview.MinDecodeWidth;

     // Dispose existing timer if present to prevent memory leaks
     _columnResizeTimer?.Stop();
  _columnResizeTimer?.Dispose();

        // Create debounced timer for column resize events
  _columnResizeTimer = new System.Timers.Timer(Preview.ColumnResizeDebounceMs) { AutoReset = false };
    _columnResizeTimer.Elapsed += ColumnResizeTimer_Elapsed;

      // Monitor column width changes at regular intervals
      var columnWidthMonitor = new System.Windows.Threading.DispatcherTimer
  {
              Interval = TimeSpan.FromMilliseconds(Preview.ColumnWidthMonitorIntervalMs)
  };
        columnWidthMonitor.Tick += (s, e) => CheckColumnWidthChanged();
   columnWidthMonitor.Start();
        }

  private void CheckColumnWidthChanged()
        {
            // Trigger reload if column width changed by more than threshold
      if (PreviewColumn.ActualWidth > 0 && 
                Math.Abs(PreviewColumn.ActualWidth - _lastPreviewColumnWidth) > Preview.ColumnWidthChangeThreshold)
            {
  _columnResizeTimer?.Stop();
     _columnResizeTimer?.Start();
     }
  }

        private async void ColumnResizeTimer_Elapsed(object? sender, System.Timers.ElapsedEventArgs e)
        {
            await Dispatcher.InvokeAsync(async () =>
 {
        if (PreviewColumn.ActualWidth > 0 && _viewModel.Settings.LoadPreviews)
                {
         _lastPreviewColumnWidth = PreviewColumn.ActualWidth;
      _viewModel.StatusText = "Reloading previews at new resolution...";
         await ReloadAllPreviewsAsync();
        _viewModel.StatusText = "Ready";
      }
            });
        }

        private async Task ReloadAllPreviewsAsync()
        {
   var itemsWithPreviews = _viewModel.ImageItems
    .Where(i => !string.IsNullOrEmpty(i.Url) && _viewModel.Settings.LoadPreviews)
       .ToList();

            if (itemsWithPreviews.Count == 0)
    return;

    var semaphore = new SemaphoreSlim(10);
         var previewLoader = new Services.ImagePreviewLoader(Services.HttpClientFactory.Instance);
        
   var tasks = itemsWithPreviews.Select(async item =>
{
        await semaphore.WaitAsync().ConfigureAwait(false);
   try
            {
       var newPreview = await previewLoader.LoadPreviewImageFromColumnWidthAsync(item.Url, PreviewColumn.ActualWidth).ConfigureAwait(false);
          if (newPreview != null)
      await Dispatcher.InvokeAsync(() => item.PreviewImage = newPreview);
       }
    catch (Exception ex)
     {
         Services.Logger.Error($"Failed to reload preview for {item.Url}", ex);
        }
      finally
       {
    semaphore.Release();
   }
  });

      await Task.WhenAll(tasks).ConfigureAwait(false);
      }

        // Event handler for ListView selection changes
     private void ImageList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
      // Update ViewModel's SelectedItems collection
  _viewModel.SelectedItems.Clear();
  foreach (var item in ImageList.SelectedItems.Cast<ImageDownloadItem>())
            {
            _viewModel.SelectedItems.Add(item);
            }
  }

        // Event handler for ListView double-click
        private void ImageList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
   if (ImageList.SelectedItem is ImageDownloadItem item)
      {
                _viewModel.ItemDoubleClickCommand.Execute(item);
            }
        }

        // Context menu event handlers - route to ViewModel commands
        private void ContextMenu_Download_Click(object sender, RoutedEventArgs e)
        {
          if (ImageList.SelectedItem is ImageDownloadItem item)
            {
      _viewModel.DownloadSingleCommand.Execute(item);
 }
        }

   private void ContextMenu_ReloadPreview_Click(object sender, RoutedEventArgs e)
        {
            if (ImageList.SelectedItem is ImageDownloadItem item)
       {
        _viewModel.ReloadPreviewCommand.Execute(item);
            }
  }

        private void ContextMenu_Cancel_Click(object sender, RoutedEventArgs e)
        {
  if (ImageList.SelectedItem is ImageDownloadItem item)
            {
       _viewModel.CancelSingleCommand.Execute(item);
 }
        }

  protected override void OnClosing(CancelEventArgs e)
        {
_viewModel.Dispose();
            _columnResizeTimer?.Dispose();
        base.OnClosing(e);
        }
    }
}