using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace WebsiteImagePilfer.Converters
{
    public class ListViewIndexConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ListViewItem listViewItem)
      {
          var listView = FindParent<ListView>(listViewItem);
  if (listView != null)
         {
              var mainWindow = FindParent<MainWindow>(listView);
        int localIndex = listView.ItemContainerGenerator.IndexFromContainer(listViewItem);

         if (mainWindow != null && localIndex >= 0)
           {
         // Calculate global index based on current page
             int globalIndex = ((mainWindow._currentPage - 1) * mainWindow._itemsPerPage) + localIndex + 1;
        return globalIndex.ToString();
      }
        }
  }
            return "?";
        }

   public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        private static T? FindParent<T>(DependencyObject child) where T : DependencyObject
        {
       DependencyObject parentObject = VisualTreeHelper.GetParent(child);
            if (parentObject == null) return null;

        if (parentObject is T parent)
      return parent;
            else
  return FindParent<T>(parentObject);
        }
    }
}
