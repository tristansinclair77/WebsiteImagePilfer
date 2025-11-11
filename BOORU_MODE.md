# Booru Mode

## Overview

**Booru Mode** is a specialized feature designed to handle image boards (Booru-style sites) that use a multi-tier image resolution system. This mode automatically navigates the complex URL structure to download full-resolution images instead of thumbnails or samples.

## Supported Sites

Booru Mode currently supports the following image board platforms:
- **Safebooru** (safebooru.org)
- **Gelbooru** (gelbooru.com)
- **Danbooru** (danbooru.donmai.us)
- **Konachan** (konachan.com)
- **Yande.re** (yande.re)
- **Sankaku Complex** (sankaku channels)

## How It Works

### Understanding Booru Site Structure

Booru sites typically have a 3-tier image resolution hierarchy:

1. **Thumbnail** - Small preview shown on list pages
   - Example: `/thumbnails/289/thumbnail_86de6d5b7e62bc80ab3122f584d5ecf5cf7a8dd5.jpg`

2. **Sample** - Medium-sized version shown on detail pages
   - Example: `/samples/289/sample_86de6d5b7e62bc80ab3122f584d5ecf5cf7a8dd5.jpg`

3. **Full Resolution** - Original high-quality image
   - Example: `/images/289/86de6d5b7e62bc80ab3122f584d5ecf5cf7a8dd5.jpg`

### Processing Flow

#### During Scanning (ImageScanner)

When Booru Mode is enabled, the scanner will:

1. **Detect Booru Links**: Identify links on list pages that point to detail/view pages
   - Example: `https://safebooru.org/index.php?page=post&s=view&id=6221306`

2. **Extract Post IDs**: Parse the post ID from the URL for reference

3. **Add to Download Queue**: Queue the detail page URL for processing

#### During Download (ImageDownloader)

When processing a queued item:

1. **Detect Detail Page**: Check if the URL is a Booru detail/view page

2. **Fetch Detail Page HTML**: Download and parse the detail page

3. **Extract Full-Resolution URL**: Use multiple strategies to find the full-res image:
   - Look for "Original image" links
   - Find sample images and transform to full-res
   - Search for `/images/` URLs in HTML
   - Check JavaScript/data attributes

4. **Transform Sample ? Full**: Apply regex patterns to convert:
   ```
   /samples/289/sample_HASH.jpg  ?  /images/289/HASH.jpg
   /thumbnails/289/thumbnail_HASH.jpg  ?  /images/289/HASH.jpg
   ```

5. **Download Full-Resolution**: Download the transformed URL

## Usage

### Enabling Booru Mode

1. Open **Settings** from the main window
2. Navigate to the **"Full Resolution Detection"** section
3. Check the box: **"Enable Booru Mode (for Safebooru, Gelbooru, Danbooru, etc.)"**
4. Click **OK** to save

### Best Practices

For optimal results when scanning Booru sites:

1. **Enable Booru Mode** ?
2. **Disable "Skip full-resolution check"** for automatic URL transformation
3. **Use Thorough Scan** for JavaScript-heavy Booru sites
4. Consider enabling:
   - ? Check data attributes
   - ? Check script tags
5. Set appropriate file type filters (JPG, PNG, etc.)

### Example Workflow

**Scenario**: Download images from Safebooru tag page

1. Navigate to: `https://safebooru.org/index.php?page=post&s=list&tags=reze_%28chainsaw_man%29`
2. Enable Booru Mode in Settings
3. Click "Scan for Images" (Thorough Scan recommended)
4. The app will:
   - Find all thumbnail links on the list page
   - Extract detail page URLs
   - Queue them for download
5. Click "Download Selected" or "Download All"
6. During download:
   - Fetch each detail page
   - Extract full-resolution image URL
   - Download the full-res image (not sample/thumbnail)

## Technical Details

### URL Transformation Patterns

Booru Mode uses regex patterns to transform URLs:

```csharp
// Sample to Full-res
Pattern: /samples/(\d+)/sample_(.+)
Result:  /images/$1/$2

// Thumbnail to Full-res  
Pattern: /thumbnails/(\d+)/thumbnail_(.+)
Result:  /images/$1/$2
```

### Detail Page Extraction Strategies

When fetching a detail page, the app uses multiple fallback strategies:

**Strategy 1**: Look for "Original" link text
```html
<a href="/images/289/hash.jpg">Original image</a>
```

**Strategy 2**: Transform sample image src
```html
<img src="/samples/289/sample_hash.jpg" />
```

**Strategy 3**: Regex search for `/images/` URLs
```regex
https?://[^"'\s]+/images/\d+/[^"'\s]+\.(jpg|jpeg|png|gif|webp)
```

**Strategy 4**: Check JavaScript and data attributes
```javascript
var imageUrl = "https://safebooru.org/images/289/hash.jpg";
```

### Settings Integration

Booru Mode is controlled by:
- **Setting**: `DownloadSettings.EnableBooruMode` (bool)
- **Persistence**: Saved in `settings.json`
- **Default**: `false` (opt-in feature)

## Compatibility Notes

### Works With
- ? "Skip full-resolution check" disabled (recommended)
- ? File type filters
- ? Size filtering
- ? Thorough Scan mode
- ? Fast Scan mode (with limitations)

### Limitations
- Fast Scan may miss some images if they require JavaScript to load
- Sites with heavy anti-bot protection may block detail page fetching
- Custom Booru implementations may use different URL patterns

## Troubleshooting

### No Images Found
- **Solution**: Enable Thorough Scan and browser automation
- Check if the site requires login/authentication
- Enable "Save HTML source and screenshot for debugging" to inspect page

### Downloading Samples Instead of Full-Res
- **Solution**: Ensure Booru Mode is enabled
- Check that "Skip full-resolution check" is disabled
- Verify the site matches known Booru patterns

### HTTP Errors
- **Solution**: Site may have rate limiting or anti-bot measures
- Try adding delays between requests
- Check if the site structure has changed

## Future Enhancements

Potential improvements for Booru Mode:
- Auto-detect Booru sites without manual toggle
- Custom pattern configuration for unsupported Booru clones
- Batch detail page fetching with rate limiting
- Authentication support for restricted boards
- Parallel detail page processing

## Code References

Key files implementing Booru Mode:

- **Models/DownloadSettings.cs**: `EnableBooruMode` property
- **Services/ImageDownloader.cs**: 
  - `IsBooruDetailPageUrl()`
  - `ExtractImageFromBooruDetailPageAsync()`
  - `IsBooruUrl()`
- **Services/ImageScanner.cs**:
  - `GetBooruFullResolutionUrl()`
- **SettingsWindow.xaml**: UI checkbox for enabling mode
- **SettingsWindow.xaml.cs**: Settings binding

---

**Last Updated**: November 2025  
**Version**: 1.0.0
