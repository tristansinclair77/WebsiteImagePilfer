# Quick Reference: Using AppConstants and Logger

## AppConstants Usage

### Importing
```csharp
// Option 1: Full qualification
using WebsiteImagePilfer.Constants;
// Use: AppConstants.Status.Done

// Option 2: Static import (recommended)
using static WebsiteImagePilfer.Constants.AppConstants;
// Use: Status.Done
```

### Available Constants

#### Status Strings
```csharp
Status.Ready      // "Ready"
Status.Checking         // "Checking..."
Status.FindingFullRes     // "Finding full-res..."
Status.Downloading        // "Downloading..."
Status.Done               // "? Done"
Status.Backup             // "? Backup"
Status.Duplicate  // "? Duplicate"
Status.Cancelled        // "? Canceled"
Status.Failed        // "? Failed"
Status.Skipped     // "? Skipped"
Status.SkippedSize        // "? Skipped (too small)"
Status.SkippedJpg         // "? Skipped (not JPG)"
Status.SkippedPng         // "? Skipped (not PNG)"
```

#### Network Timeouts
```csharp
Network.HttpTimeoutSeconds           // 30
Network.HeadRequestTimeoutSeconds    // 5
Network.PageLoadTimeoutSeconds       // 60
Network.ImplicitWaitSeconds        // 10
```

#### Scanning Configuration
```csharp
Scanning.FastScanWaitMs         // 5000
Scanning.ThoroughScanCheckIntervalMs  // 5000
Scanning.ThoroughScanMaxStableChecks     // 3
Scanning.ScrollDelayMs  // 1000
Scanning.RetryDelayMs       // 2000
Scanning.MaxRetryCount         // 3
Scanning.PageLoadTimeoutSeconds          // 60
```

#### File System
```csharp
Files.MaxFilenameLength   // 200
Files.GuidShortLength         // 8
Files.FallbackExtension       // ".jpg"
Files.QueryParamFilename      // "f"
```

#### Preview Configuration
```csharp
Preview.MinDecodeWidth              // 200
Preview.QualityMultiplier    // 2
Preview.ColumnResizeDebounceMs   // 500
Preview.ColumnWidthMonitorIntervalMs // 100
Preview.ColumnWidthChangeThreshold   // 10
```

#### Validation Limits
```csharp
Validation.MinImageSize          // 100
Validation.MaxImageSize          // 1_000_000_000
Validation.MinItemsPerPage       // 1
Validation.MaxItemsPerPage       // 1000
Validation.MinMaxImagesToScan    // 1
Validation.MaxMaxImagesToScan    // 10000
```

#### Image Processing
```csharp
Images.SizePatterns  // string[] { "_800x800", "_small", ... }
Images.Attributes      // string[] { "src", "data-src", ... }
Images.Extensions      // string[] { ".jpg", ".jpeg", ... }
```

#### Other
```csharp
Converters.UnknownIndex    // "?"
Settings.FileName       // "settings.json"
```

---

## Logger Usage

### Basic Logging
```csharp
// Info - general application flow
Logger.Info("Application started");
Logger.Info($"Scanning URL: {url}");

// Debug - detailed diagnostic info
Logger.Debug("Entering validation loop");
Logger.Debug($"Processing item {i} of {total}");

// Warning - recoverable issues
Logger.Warning("Cache miss, loading from disk");
Logger.Warning($"Retrying operation, attempt {retry}/{maxRetries}");

// Error - failures (with optional exception)
Logger.Error("Failed to save settings");
Logger.Error("Download failed", ex);
```

### Automatic Source Tracking
```csharp
// The [CallerMemberName] attribute automatically captures the method name:

public async Task DownloadAsync()
{
    Logger.Info("Starting download");  
    // Logs: [Info] [DownloadAsync] Starting download
}
```

### Exception Logging
```csharp
try
{
    await SomeOperationAsync();
}
catch (HttpRequestException ex)
{
    // Logs full exception details including stack trace
    Logger.Error("Network request failed", ex);
}
catch (Exception ex)
{
 Logger.Error("Unexpected error", ex);
    throw; // Re-throw if needed
}
```

### Log File Location
```csharp
// Logs are written to:
// {AppDirectory}/logs/app_YYYYMMDD.log

// Get current log file path:
string logPath = Logger.GetLogFilePath();

// Get logs directory:
string logDir = Logger.GetLogDirectory();
```

### Log Format
```
[2024-12-19 14:32:15.123] [Info] [MethodName] Your message here
[2024-12-19 14:32:16.456] [Error] [MethodName] Error message
  Exception: HttpRequestException: Network error
  StackTrace: at System.Net.Http.HttpClient...
```

---

## Migration Patterns

### Before vs After

#### Status Checks
```csharp
// BEFORE:
private const string STATUS_DONE = "? Done";
if (item.Status == STATUS_DONE) { ... }

// AFTER:
using static WebsiteImagePilfer.Constants.AppConstants;
if (item.Status == Status.Done) { ... }
```

#### Logging
```csharp
// BEFORE:
System.Diagnostics.Debug.WriteLine($"Downloaded {fileName} in {elapsed}ms");

// AFTER:
Logger.Info($"Downloaded {fileName} in {elapsed}ms");
```

#### Error Handling
```csharp
// BEFORE:
catch (Exception ex)
{
    System.Diagnostics.Debug.WriteLine($"Error: {ex.Message}");
    item.Status = STATUS_FAILED;
}

// AFTER:
catch (Exception ex)
{
Logger.Error($"Download failed for {item.FileName}", ex);
    item.Status = Status.Failed;
}
```

---

## Best Practices

### Constants
? **DO:** Use static import for cleaner code  
? **DO:** Use constants for all status strings  
? **DO:** Use constants for timeouts and limits  
? **DON'T:** Hard-code magic strings or numbers  
? **DON'T:** Create local constants for shared values  

### Logging
? **DO:** Log at appropriate levels (Debug < Info < Warning < Error)  
? **DO:** Include context in messages (filenames, URLs, counts)  
? **DO:** Log exceptions with full details  
? **DO:** Use structured messages (not just "Error!")  
? **DON'T:** Log sensitive data (passwords, tokens)  
? **DON'T:** Log in tight loops (causes performance issues)  
? **DON'T:** Swallow exceptions without logging  

### Performance
? **DO:** Logger writes asynchronously (fire-and-forget)  
? **DO:** Constants are compile-time (zero runtime cost)  
? **DON'T:** Call Logger in loops over large collections  
? **DON'T:** Log Debug messages in production hot paths  

---

## Troubleshooting

### Constants Not Found
```csharp
// Error: CS0103: The name 'Status' does not exist
// Solution: Add using directive
using static WebsiteImagePilfer.Constants.AppConstants;
```

### Logger Not Found
```csharp
// Error: CS0103: The name 'Logger' does not exist
// Solution: Logger is in Services namespace
using WebsiteImagePilfer.Services;
```

### Log Files Not Created
- Check app has write permissions to its directory
- Logger creates `/logs/` subdirectory automatically
- Logs write async, may take a moment to appear
- Check `Logger.GetLogFilePath()` for actual location

---

## Examples from Codebase

### Status Filtering
```csharp
bool include = 
    (FilterReadyCheckBox.IsChecked.GetValueOrDefault() && item.Status == Status.Ready) ||
    (FilterDoneCheckBox.IsChecked.GetValueOrDefault() && item.Status == Status.Done) ||
    (FilterFailedCheckBox.IsChecked.GetValueOrDefault() && item.Status == Status.Failed);
```

### Timeout Configuration
```csharp
_httpClient.Timeout = TimeSpan.FromSeconds(Network.HttpTimeoutSeconds);
driver.Manage().Timeouts().PageLoad = TimeSpan.FromSeconds(Scanning.PageLoadTimeoutSeconds);
```

### Error Logging
```csharp
catch (HttpRequestException ex)
{
 item.Status = Status.Failed;
    item.ErrorMessage = $"Network error: {ex.Message}";
    Logger.Error($"HTTP error for {item.FileName}", ex);
}
```

### Info Logging
```csharp
var totalElapsed = (DateTime.Now - startTime).TotalMilliseconds;
Logger.Info($"Downloaded {item.FileName} in {totalElapsed}ms (download: {downloadElapsed}ms)");
```

---

## Quick Command Reference

### View Current Logs
```powershell
# Open log file in notepad
notepad (Join-Path $PWD "logs\app_$(Get-Date -Format 'yyyyMMdd').log")

# Tail logs in PowerShell
Get-Content "logs\app_*.log" -Wait -Tail 20
```

### Find All Constant Usage
```powershell
# Search for Status constant usage
Select-String -Path *.cs -Pattern "Status\." -Recurse

# Search for Logger usage
Select-String -Path *.cs -Pattern "Logger\." -Recurse
```

---

## Summary

- **AppConstants:** Centralized, type-safe, IntelliSense-friendly constants
- **Logger:** Simple, file-based logging with automatic source tracking
- **No Dependencies:** Both use only .NET built-in libraries
- **Zero Runtime Cost:** Constants are compile-time, logging is async
- **Production Ready:** Logs enable post-mortem debugging
