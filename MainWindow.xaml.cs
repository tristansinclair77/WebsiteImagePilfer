using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.ComponentModel;
using WebsiteImagePilfer.ViewModels;
using WebsiteImagePilfer.Models;

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
        PreviewColumn.Width = 150;
            _lastPreviewColumnWidth = 150;

  _columnResizeTimer = new System.Timers.Timer(300) { AutoReset = false };
    _columnResizeTimer.Elapsed += ColumnResizeTimer_Elapsed;

            var columnWidthMonitor = new System.Windows.Threading.DispatcherTimer
        {
Interval = TimeSpan.FromMilliseconds(100)
            };
        columnWidthMonitor.Tick += (s, e) => CheckColumnWidthChanged();
       columnWidthMonitor.Start();
        }

        private void CheckColumnWidthChanged()
        {
            if (PreviewColumn.ActualWidth > 0 && Math.Abs(PreviewColumn.ActualWidth - _lastPreviewColumnWidth) > 10)
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
        await semaphore.WaitAsync();
   try
            {
       var newPreview = await previewLoader.LoadPreviewImageFromColumnWidthAsync(item.Url, PreviewColumn.ActualWidth);
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

          await Task.WhenAll(tasks);
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