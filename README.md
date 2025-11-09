# Website Image Pilfer

A powerful WPF application for scanning webpages and downloading images locally, built with .NET 8.

## Features Implemented

### ? Core Functionality (Steps 1-3)

- **Clean UI Layout**: 900x600 window with intuitive controls
- **URL Input**: Text box for entering webpage URLs
- **Scan & Download**: Automated image detection and download
- **Progress Tracking**: Real-time progress bar and status updates
- **Image List**: ListView showing download status, URLs, and filenames

### ? Advanced Download Logic (Step 4)

- **Smart File Naming**: Extracts filenames from URLs with fallback generation
- **Filename Sanitization**: Removes invalid characters from filenames
- **Duplicate Prevention**: Automatically handles filename conflicts with counters
- **Unique Image Detection**: Uses HashSet to prevent downloading duplicates
- **Sequential Downloads**: Downloads images one at a time with proper error handling
- **Progress Updates**: Shows "Downloaded: X | Skipped: Y | Remaining: Z"

### ? Event Handling (Step 5)

- **Button State Management**: Scan button disabled during downloads
- **Real-time Status Updates**: Status bar shows current operation
- **Error Handling**: Try-catch blocks for network failures and broken links
- **URL Validation**: Checks for valid URLs before scanning
- **Settings Persistence**: Remembers last URL and download folder

### ? Optional Enhancements (Step 6)

#### A. Thumbnail Previews
- **Visual Preview Column**: Shows 60x60 thumbnail images in the list
- **Toggle Option**: Can be enabled/disabled in settings

#### B. Cancel Option
- **Cancel Button**: Stop downloads mid-process using CancellationToken
- **Graceful Cancellation**: Shows how many images were downloaded before cancellation
- **Status Tracking**: Marks cancelled items with "? Cancelled" status

#### C. Advanced Settings
- **Settings Dialog**: Dedicated settings window with multiple options
- **Custom Save Directory**: User-selectable download folder with Browse button
- **Size Filtering**: Option to ignore images smaller than specified size (adjustable 1KB-100KB)
- **File Type Filters**: Options for JPG-only or PNG-only downloads
- **Persistent Settings**: Saves download folder and last URL between sessions

### ? Testing & Edge Cases (Step 7)

- **Empty URL Validation**: Prevents scanning without URL
- **Malformed URLs**: Shows error for invalid URLs
- **Broken Links**: Catches HttpRequestException and marks as failed
- **Network Errors**: Handles timeout and connection errors gracefully
- **Duplicate URLs**: HashSet prevents duplicate downloads
- **Protected Images**: Catches authentication/403 errors
- **Data URIs**: Filters out data: URIs that aren't downloadable
- **Invalid Filenames**: Sanitizes filenames to prevent file system errors

### ? Next Steps Features (Step 8)

- **Settings Persistence**: Uses `Properties.Settings` to remember:
  - Last download folder
  - Last entered URL
- **Settings Dialog**: Clean dialog for configuring options
- **Status Messages**: Comprehensive status updates for all operations
- **Proper Cleanup**: Cancels operations on window closing
- **Error Logging**: Failed items show detailed error messages

## How to Use

### Basic Usage

1. **Enter URL**: Type or paste a webpage URL (e.g., `https://example.com`)
2. **Select Folder** (Optional): Click "Browse..." to choose download location
3. **Click "Scan & Download"**: Application will:
   - Fetch the webpage HTML
   - Extract all image URLs (including CSS background images)
   - Download each image sequentially
   - Show progress in real-time
4. **Monitor Progress**: Watch the progress bar and list update
5. **Cancel Anytime**: Click "Cancel" to stop downloads mid-process

### Settings Configuration

Click the **"Settings..."** button to configure:

- **Filter images by minimum size**: Ignore small icons and thumbnails
  - Adjustable from 1KB to 100KB
  - Useful for skipping tiny images
  
- **Show thumbnail previews**: Display image previews in the list
  - Toggle on/off based on preference
  
- **File type filters**: Download only specific formats
  - JPG/JPEG only
  - PNG only
  - Or all types (default)

### Download Results

Each image in the list shows:
- **Preview**: Thumbnail of downloaded image (if enabled)
- **Status**: 
  - "Downloading..." - In progress
  - "? Done" - Successfully downloaded
  - "? Failed" - Download failed (shows error)
  - "? Skipped" - Filtered out (too small)
  - "? Cancelled" - Operation cancelled
- **Image URL**: Source URL of the image
- **Filename**: Local filename where image was saved

### Status Bar Information

The status bar shows real-time information:
- "Scanning webpage..." - Fetching HTML
- "Found X images. Starting download..." - Images detected
- "Downloaded: X | Skipped: Y | Remaining: Z" - Progress
- "Download complete! X images saved to [folder]" - Success
- "Cancelled. Downloaded X of Y images" - Cancelled operation
- "Error occurred during download" - Error state

## Technical Details

### NuGet Packages Used

- **HtmlAgilityPack** (v1.12.4): HTML parsing and XPath queries
- **CommunityToolkit.Mvvm** (v8.4.0): MVVM helpers and utilities

### Architecture

- **MainWindow.xaml**: Primary UI layout
- **MainWindow.xaml.cs**: Core logic for scanning and downloading
- **SettingsWindow.xaml**: Settings dialog UI
- **SettingsWindow.xaml.cs**: Settings management logic
- **ImageDownloadItem**: Data model for list items (implements INotifyPropertyChanged)
- **DownloadSettings**: Settings data model
- **Properties/Settings**: User settings persistence

### Key Methods

- `GetImageUrlsAsync()`: Fetches HTML and extracts image URLs
  - Parses `<img src>` attributes
  - Extracts CSS `background-image` URLs
  - Converts relative URLs to absolute
  - Filters data URIs and duplicates
  
- `DownloadImagesAsync()`: Downloads images with progress tracking
  - Creates download folder if needed
  - Sanitizes filenames
  - Handles duplicates
  - Applies size filters
- Updates UI in real-time
  - Supports cancellation
  
- `SanitizeFileName()`: Removes invalid filesystem characters

### Error Handling

Comprehensive error handling for:
- Network failures (timeout, DNS, connection)
- HTTP errors (404, 403, 500)
- File system errors (permissions, disk space)
- Invalid URLs and malformed HTML
- Cancellation requests
- Thread safety with async/await

## Future Enhancements

Potential additions for future versions:

### Parallel Downloads
- Download 3-5 images simultaneously for faster operation
- Implement rate limiting to avoid overwhelming servers

### Recursive Scanning
- Follow links to scan subpages
- Depth limit configuration
- Domain filtering

### Built-in Image Viewer
- Preview images in-app before downloading
- Zoom and pan functionality
- Image metadata display

### Log Panel
- Console-style log of all operations
- Export log to file
- Filtering and search

### Dark Mode
- Theme toggle
- System theme detection
- Custom color schemes

### Advanced Filtering
- Minimum/maximum resolution
- Aspect ratio filtering
- File extension whitelist/blacklist
- Regex pattern matching for URLs

### Batch Operations
- Process multiple URLs from a list
- Import URLs from file
- Export results to CSV

## System Requirements

- Windows 10 or later
- .NET 8 Runtime
- Internet connection for downloading images

## License

This is an educational project created for learning WPF and asynchronous programming concepts.

---

**Version**: 1.0  
**Target Framework**: .NET 8.0  
**Platform**: Windows WPF Application
