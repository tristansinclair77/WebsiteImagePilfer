# MVVM Architecture Quick Reference Guide

## ??? Architecture Overview

```
???????????????????????????????????????????????????????????
?       View Layer     ?
?  ????????????????????        ????????????????????      ?
?  ? MainWindow.xaml  ??????????MainWindow.xaml.cs?      ?
?  ?  (Declarative)   ?        ?  (Thin View)     ?      ?
?  ????????????????????        ????????????????????      ?
?           ?   ?               ?
? ? DataBinding      ? Events     ?
?           ?        ?        ?
?????????????????????????????????????????????????????????
 ?         ?
  ?       ?
???????????????????????????????????????????????????????????
?   ViewModel Layer         ?
?  ??????????????????????????????????????????????????  ?
?  ?         MainWindowViewModel         ?    ?
?  ?  • Properties (28)          ?    ?
??  • Commands (17)           ?    ?
?  ?  • Business Logic       ?    ?
?  ?  • State Management     ?    ?
?  ??????????????????????????????????????????????????    ?
?           ?    ?
?       ? Uses      ?
?    ?           ?
?  ??????????????????????????????????????????????????    ?
?  ?     Base Infrastructure  ?    ?
?  ?  • ViewModelBase (INotifyPropertyChanged)     ?    ?
?  ?  • RelayCommand (ICommand)                 ? ?
?  ?  • AsyncRelayCommand (ICommand)  ?    ?
?  ??????????????????????????????????????????????????    ?
????????????????????????????????????????????????????????????
    ?
            ? Orchestrates
           ?
???????????????????????????????????????????????????????????
?      Service Layer   ?
?  ????????????????  ????????????????  ????????????????  ?
?  ?ImageScanner  ?  ?ImageDownloader?  ?PreviewLoader ?  ?
?  ????????????????  ????????????????  ????????????????  ?
???????????????????????????????????????????????????????????
            ?
   ? Updates
     ?
???????????????????????????????????????????????????????????
?          Model Layer  ?
?  ????????????????????         ????????????????????     ?
?  ?ImageDownloadItem ?         ?DownloadSettings  ??
?  ????????????????????   ????????????????????     ?
???????????????????????????????????????????????????????????
```

---

## ?? File Structure

```
WebsiteImagePilfer/
??? ViewModels/
?   ??? ViewModelBase.cs   # Base class for all ViewModels
?   ??? MainWindowViewModel.cs     # Main window business logic
??? Commands/
?   ??? RelayCommand.cs            # Synchronous commands
?   ??? AsyncRelayCommand.cs # Asynchronous commands
??? Views/
?   ??? MainWindow.xaml# UI layout (declarative)
?   ??? MainWindow.xaml.cs         # Thin view (130 lines)
??? Models/
?   ??? ImageDownloadItem.cs      # Data model
?   ??? DownloadSettings.cs     # Settings model
??? Services/   # Business services
??? Helpers/   # Utility classes
??? Constants/          # Application constants
```

---

## ?? Core Concepts

### 1. ViewModelBase

All ViewModels inherit from `ViewModelBase`:

```csharp
public class MyViewModel : ViewModelBase
{
    private string _myProperty;
    
    public string MyProperty
    {
        get => _myProperty;
        set => SetProperty(ref _myProperty, value);
    }
}
```

**Key Methods:**
- `SetProperty<T>()` - Sets property and raises PropertyChanged
- `OnPropertyChanged()` - Manually raises PropertyChanged

---

### 2. Commands

#### Synchronous Commands (RelayCommand)
```csharp
public ICommand MyCommand { get; }

public MyViewModel()
{
    MyCommand = new RelayCommand(
 execute: _ => DoSomething(),
        canExecute: _ => CanDoSomething()
    );
}

private void DoSomething()
{
    // Your logic here
}

private bool CanDoSomething()
{
    return someCondition;
}
```

#### Asynchronous Commands (AsyncRelayCommand)
```csharp
public ICommand MyAsyncCommand { get; }

public MyViewModel()
{
    MyAsyncCommand = new AsyncRelayCommand(
        execute: async _ => await DoSomethingAsync(),
        canExecute: _ => CanDoSomething()
    );
}

private async Task DoSomethingAsync()
{
    // Your async logic here
    await Task.Delay(1000);
}
```

**Benefits:**
- ? Automatically prevents concurrent execution
- ? Updates CanExecute automatically
- ? Integrates with WPF command system

---

### 3. Data Binding in XAML

#### One-Way Binding (ViewModel ? View)
```xaml
<TextBlock Text="{Binding StatusText}"/>
<ProgressBar Value="{Binding ProgressValue}"/>
<Button IsEnabled="{Binding IsScanEnabled}"/>
```

#### Two-Way Binding (View ? ViewModel)
```xaml
<TextBox Text="{Binding Url, UpdateSourceTrigger=PropertyChanged}"/>
<CheckBox IsChecked="{Binding IsFastScan}"/>
```

#### Command Binding
```xaml
<Button Command="{Binding ScanCommand}" Content="Scan"/>
<Button Command="{Binding DownloadCommand}" Content="Download"/>
```

#### Collection Binding
```xaml
<ListView ItemsSource="{Binding CurrentPageItems}">
 <!-- Item template -->
</ListView>
```

---

## ?? Common Tasks

### Adding a New Property

**Step 1:** Add backing field in ViewModel
```csharp
private string _newProperty;
```

**Step 2:** Add public property
```csharp
public string NewProperty
{
    get => _newProperty;
    set => SetProperty(ref _newProperty, value);
}
```

**Step 3:** Bind in XAML
```xaml
<TextBox Text="{Binding NewProperty, UpdateSourceTrigger=PropertyChanged}"/>
```

---

### Adding a New Command

**Step 1:** Declare command property in ViewModel
```csharp
public ICommand NewCommand { get; }
```

**Step 2:** Initialize in constructor
```csharp
public MainWindowViewModel()
{
    NewCommand = new AsyncRelayCommand(async _ => await ExecuteNewCommandAsync());
}
```

**Step 3:** Implement command method
```csharp
private async Task ExecuteNewCommandAsync()
{
    // Your business logic here
    StatusText = "Executing...";
    await DoWorkAsync();
    StatusText = "Complete";
}
```

**Step 4:** Bind in XAML
```xaml
<Button Command="{Binding NewCommand}" Content="New Action"/>
```

**Optional Step 5:** Add CanExecute logic
```csharp
NewCommand = new AsyncRelayCommand(
    execute: async _ => await ExecuteNewCommandAsync(),
    canExecute: _ => CanExecuteNewCommand()
);

private bool CanExecuteNewCommand()
{
return !string.IsNullOrEmpty(Url) && IsScanEnabled;
}
```

---

### Adding a New Filter

**Step 1:** Add backing field
```csharp
private bool _filterNewStatus = true;
```

**Step 2:** Add property that triggers filtering
```csharp
public bool FilterNewStatus
{
    get => _filterNewStatus;
    set 
  { 
    if (SetProperty(ref _filterNewStatus, value))
   ApplyStatusFilter();
    }
}
```

**Step 3:** Update ApplyStatusFilter method
```csharp
private void ApplyStatusFilter()
{
    // ...existing filters...
    
    bool include = // ...existing conditions... ||
    (FilterNewStatus && item.Status == Status.NewStatus);
    
    if (include)
        FilteredImageItems.Add(item);
}
```

**Step 4:** Add to XAML
```xaml
<CheckBox Style="{StaticResource FilterCheckBox}" 
 Content="New Status" 
 IsChecked="{Binding FilterNewStatus}"/>
```

---

### Showing Dialogs

**Current Approach:**
```csharp
private void ShowInfo(string message, string title = "Info")
{
    MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
}

// Usage:
ShowInfo("Operation complete!", "Success");
```

**Future Approach (Dialog Service):**
```csharp
private readonly IDialogService _dialogService;

// In constructor:
_dialogService = dialogService;

// Usage:
await _dialogService.ShowInfoAsync("Operation complete!", "Success");
```

---

### Updating UI from Background Thread

**Wrong:**
```csharp
await Task.Run(() => 
{
    StatusText = "Working..."; // ? Cross-thread exception!
});
```

**Correct:**
```csharp
await Task.Run(() => 
{
    // Do background work here
});

// Update UI on UI thread (PropertyChanged handles this):
StatusText = "Work complete"; // ? Correct
```

**Why it works:** INotifyPropertyChanged marshals to UI thread automatically.

---

## ?? MainWindowViewModel Reference

### Properties (28 total)

#### Basic Properties
- `Url: string` - Scan URL
- `DownloadFolder: string` - Save location
- `IsFastScan: bool` - Scan mode
- `StatusText: string` - Status bar text
- `ProgressValue: double` - Progress (0-100)
- `PageInfoText: string` - Pagination info

#### Collections
- `ImageItems: ObservableCollection<ImageDownloadItem>` - All images
- `FilteredImageItems: ObservableCollection<ImageDownloadItem>` - Filtered
- `CurrentPageItems: ObservableCollection<ImageDownloadItem>` - Current page
- `SelectedItems: ObservableCollection<ImageDownloadItem>` - Selected

#### Filters (8)
- `FilterReady: bool`
- `FilterDone: bool`
- `FilterBackup: bool`
- `FilterDuplicate: bool`
- `FilterFailed: bool`
- `FilterSkipped: bool`
- `FilterCancelled: bool`
- `FilterDownloading: bool`

#### Button States (6)
- `IsScanEnabled: bool`
- `IsDownloadEnabled: bool`
- `IsDownloadSelectedEnabled: bool`
- `IsCancelEnabled: bool`
- `IsPrevPageEnabled: bool`
- `IsNextPageEnabled: bool`

#### Other
- `Settings: DownloadSettings` - App settings
- `CurrentPage: int` - Current page number
- `ItemsPerPage: int` - Items per page

---

### Commands (17 total)

#### Main Actions
- `ScanCommand` - Scan webpage for images
- `DownloadCommand` - Download all ready images
- `DownloadSelectedCommand` - Download selected images
- `CancelCommand` - Cancel current operation

#### Folder Management
- `BrowseFolderCommand` - Browse for folder
- `UpFolderCommand` - Navigate to parent
- `NewFolderCommand` - Create new folder
- `OpenFolderCommand` - Open in Explorer

#### Filters
- `SelectAllStatusCommand` - Select all filters
- `ClearAllStatusCommand` - Clear all filters

#### Pagination
- `PrevPageCommand` - Previous page
- `NextPageCommand` - Next page

#### Item Actions
- `ItemDoubleClickCommand` - Handle double-click
- `DownloadSingleCommand` - Download single item
- `ReloadPreviewCommand` - Reload preview
- `CancelSingleCommand` - Cancel single download

#### Settings
- `SettingsCommand` - Open settings dialog

---

## ?? Testing Examples

### Testing Properties
```csharp
[Test]
public void Url_WhenSet_RaisesPropertyChanged()
{
    // Arrange
    var vm = new MainWindowViewModel();
    bool eventRaised = false;
    vm.PropertyChanged += (s, e) => 
    {
        if (e.PropertyName == "Url") eventRaised = true;
    };
  
    // Act
    vm.Url = "https://example.com";
  
    // Assert
    Assert.IsTrue(eventRaised);
}
```

### Testing Commands
```csharp
[Test]
public async Task ScanCommand_WithValidUrl_ScansImages()
{
    // Arrange
    var vm = new MainWindowViewModel();
    vm.Url = "https://example.com";
    
    // Act
    await ((AsyncRelayCommand)vm.ScanCommand).ExecuteAsync(null);
    
    // Assert
    Assert.Greater(vm.ImageItems.Count, 0);
    Assert.IsTrue(vm.IsDownloadEnabled);
}
```

### Testing Filters
```csharp
[Test]
public void FilterReady_WhenDisabled_RemovesReadyItems()
{
    // Arrange
    var vm = new MainWindowViewModel();
  vm.ImageItems.Add(new ImageDownloadItem { Status = "Ready" });
    vm.ImageItems.Add(new ImageDownloadItem { Status = "Done" });
    
    // Act
    vm.FilterReady = false;
    
    // Assert
    Assert.AreEqual(1, vm.FilteredImageItems.Count);
    Assert.AreEqual("Done", vm.FilteredImageItems[0].Status);
}
```

---

## ?? Common Pitfalls

### ? Mistake 1: Accessing View from ViewModel
```csharp
// DON'T DO THIS:
private void DoSomething()
{
    var window = Application.Current.MainWindow; // ? View dependency
    window.StatusBar.Text = "Working...";
}

// DO THIS INSTEAD:
private void DoSomething()
{
    StatusText = "Working..."; // ? Property binding
}
```

---

### ? Mistake 2: Not Using SetProperty
```csharp
// DON'T DO THIS:
private string _url;
public string Url
{
    get => _url;
  set { _url = value; } // ? No PropertyChanged
}

// DO THIS INSTEAD:
private string _url;
public string Url
{
    get => _url;
    set => SetProperty(ref _url, value); // ? Raises PropertyChanged
}
```

---

### ? Mistake 3: Blocking UI Thread
```csharp
// DON'T DO THIS:
private void DoWork()
{
    Thread.Sleep(5000); // ? Blocks UI
}

// DO THIS INSTEAD:
private async Task DoWorkAsync()
{
    await Task.Delay(5000); // ? Async
}
```

---

### ? Mistake 4: Not Implementing CanExecute
```csharp
// LESS IDEAL:
MyCommand = new RelayCommand(_ => Execute());

// BETTER:
MyCommand = new RelayCommand(
    execute: _ => Execute(),
    canExecute: _ => CanExecute() // ? Enables/disables automatically
);
```

---

## ?? Best Practices

### ? 1. Always Use Data Binding
```xaml
<!-- Good -->
<TextBlock Text="{Binding StatusText}"/>

<!-- Avoid -->
<TextBlock x:Name="StatusText"/> <!-- Then set in code-behind -->
```

### ? 2. Keep ViewModels Pure
```csharp
// ViewModel should not:
// ? Reference View classes
// ? Use MessageBox directly
// ? Access Application.Current
// ? Use Dispatcher

// ViewModel should:
// ? Use properties
// ? Use commands
// ? Use services
// ? Be testable
```

### ? 3. Use Async Commands for Long Operations
```csharp
// For I/O, network, or slow operations:
public ICommand LongOperationCommand { get; }

LongOperationCommand = new AsyncRelayCommand(async _ => 
{
    await Task.Delay(1000);
    await DownloadImageAsync();
});
```

### ? 4. Organize ViewModel with Regions
```csharp
#region Fields
private readonly HttpClient _httpClient;
#endregion

#region Properties
public string Url { get; set; }
#endregion

#region Commands
public ICommand ScanCommand { get; }
#endregion

#region Command Implementations
private async Task ScanAsync() { }
#endregion

#region Helper Methods
private void UpdateStatus() { }
#endregion
```

### ? 5. Dispose Resources
```csharp
public class MainWindowViewModel : ViewModelBase, IDisposable
{
    public void Dispose()
    {
        _cancellationTokenSource?.Dispose();
        // Dispose other resources
    }
}
```

---

## ?? Advanced Patterns

### Debouncing User Input
```csharp
private Timer _searchDebounceTimer;

public string SearchText
{
    get => _searchText;
    set
    {
        if (SetProperty(ref _searchText, value))
   {
 _searchDebounceTimer?.Stop();
   _searchDebounceTimer = new Timer(300);
            _searchDebounceTimer.Elapsed += (s, e) => PerformSearch();
    _searchDebounceTimer.Start();
        }
    }
}
```

### Progress Reporting
```csharp
private async Task DownloadWithProgressAsync()
{
    var progress = new Progress<int>(percent => 
    {
        ProgressValue = percent;
    });
    
  await _downloader.DownloadAsync(url, progress);
}
```

### Cancellation Support
```csharp
private CancellationTokenSource _cts;

public ICommand StartCommand { get; }
public ICommand CancelCommand { get; }

StartCommand = new AsyncRelayCommand(async _ => 
{
    _cts = new CancellationTokenSource();
    await DoWorkAsync(_cts.Token);
});

CancelCommand = new RelayCommand(_ => _cts?.Cancel());
```

---

## ?? Additional Resources

### Learning MVVM
- [Microsoft MVVM Documentation](https://docs.microsoft.com/wpf/mvvm)
- [MVVM Light Toolkit](https://github.com/lbugnion/mvvmlight)
- [Prism Framework](https://prismlibrary.com/)

### WPF Data Binding
- [Data Binding Overview](https://docs.microsoft.com/wpf/data-binding)
- [INotifyPropertyChanged](https://docs.microsoft.com/dotnet/api/system.componentmodel.inotifypropertychanged)

### Commands
- [ICommand Interface](https://docs.microsoft.com/dotnet/api/system.windows.input.icommand)
- [RelayCommand Pattern](https://docs.microsoft.com/archive/msdn-magazine/2009/february/patterns-wpf-apps-with-the-model-view-viewmodel-design-pattern)

---

## ?? Quick Decision Guide

**Need to display data?**
? Add property to ViewModel ? Bind in XAML

**Need to handle button click?**
? Create command in ViewModel ? Bind in XAML

**Need to update UI?**
? Set property (PropertyChanged handles it)

**Need to validate input?**
? Add logic to property setter or CanExecute

**Need to show dialog?**
? Call method in ViewModel (future: use dialog service)

**Need async operation?**
? Use AsyncRelayCommand

**Need to test logic?**
? Create unit test for ViewModel method

---

## ? Checklist for New Features

When adding a feature:

- [ ] Business logic in ViewModel, not code-behind
- [ ] Properties use SetProperty for change notification
- [ ] Commands for all user actions
- [ ] CanExecute logic for button states
- [ ] Async commands for I/O operations
- [ ] Data binding in XAML, not code
- [ ] No view references in ViewModel
- [ ] Unit tests for ViewModel logic
- [ ] Dispose resources if needed
- [ ] Follow existing patterns

---

**Remember:** The goal of MVVM is **separation of concerns**. Keep View simple, ViewModel testable, and Model pure. Happy coding! ??
