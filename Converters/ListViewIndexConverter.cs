using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using WebsiteImagePilfer.Constants;
using WebsiteImagePilfer.Services;
using static WebsiteImagePilfer.Constants.AppConstants;

namespace WebsiteImagePilfer.Converters
{
    /// <summary>
    /// Converts a ListViewItem to its global index across paginated results.
    /// Uses an attached property to cache pagination context and avoid repeated visual tree traversals.
    /// </summary>
    public class ListViewIndexConverter : IValueConverter
    {
        // Constant removed - using AppConstants

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
   {
   try
 {
     if (value is ListViewItem listViewItem)
      {
      var listView = FindParent<ListView>(listViewItem);
    if (listView != null)
  {
      // Try to get cached pagination context first (avoids tree traversal to MainWindow)
   var paginationContext = ListViewHelper.GetPaginationContext(listView);
  
  if (paginationContext == null)
     {
   // Fallback: traverse to MainWindow if context not set
      var mainWindow = FindParent<MainWindow>(listView);
          if (mainWindow != null)
   {
   paginationContext = new PaginationContext
      {
         CurrentPage = mainWindow.CurrentPage,
        ItemsPerPage = mainWindow.ItemsPerPage
        };

   // Cache the context for future conversions
      ListViewHelper.SetPaginationContext(listView, paginationContext);
      }
  }

         if (paginationContext != null)
    {
       int localIndex = listView.ItemContainerGenerator.IndexFromContainer(listViewItem);

    if (localIndex >= 0)
     {
   // Calculate global index based on current page
  int globalIndex = ((paginationContext.CurrentPage - 1) * paginationContext.ItemsPerPage) + localIndex + 1;
        return globalIndex.ToString();
  }
       }
         }
    }
        }
  catch (Exception ex)
   {
    // Log error for diagnostics but don't crash the UI
    Logger.Error($"ListViewIndexConverter error", ex);
   }

   // Return unknown index indicator if anything goes wrong
  return AppConstants.Converters.UnknownIndex;
     }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
     {
        throw new NotImplementedException();
}

   /// <summary>
        /// Recursively finds a parent of the specified type in the visual tree.
        /// </summary>
  /// <typeparam name="T">The type of parent to find.</typeparam>
 /// <param name="child">The child element to start from.</param>
    /// <returns>The parent of type T, or null if not found.</returns>
private static T? FindParent<T>(DependencyObject child) where T : DependencyObject
        {
    if (child == null)
   return null;

         try
{
         DependencyObject parentObject = VisualTreeHelper.GetParent(child);
  if (parentObject == null) 
    return null;

        if (parentObject is T parent)
      return parent;
else
   return FindParent<T>(parentObject);
       }
        catch (Exception ex)
 {
    // Handle cases where visual tree operations might fail
  Logger.Error($"FindParent error", ex);
        return null;
    }
 }
    }

    /// <summary>
  /// Helper class providing attached properties for ListView pagination context.
    /// This allows caching pagination information to avoid repeated visual tree traversals.
    /// </summary>
    public static class ListViewHelper
    {
        public static readonly DependencyProperty PaginationContextProperty =
  DependencyProperty.RegisterAttached(
    "PaginationContext",
    typeof(PaginationContext),
       typeof(ListViewHelper),
 new PropertyMetadata(null));

        public static void SetPaginationContext(DependencyObject obj, PaginationContext value)
 => obj.SetValue(PaginationContextProperty, value);

   public static PaginationContext GetPaginationContext(DependencyObject obj)
          => (PaginationContext)obj.GetValue(PaginationContextProperty);
    }

    /// <summary>
    /// Contains pagination information for calculating global indices.
    /// </summary>
    public class PaginationContext
    {
    public int CurrentPage { get; set; }
   public int ItemsPerPage { get; set; }
    }
}
