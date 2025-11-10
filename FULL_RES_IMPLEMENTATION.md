# Full-Resolution Image Detection - Implementation Summary

## ? Implementation Complete!

The app now attempts to download full-resolution images instead of previews/thumbnails.

---

## How It Works

### 1. **Automatic Detection** (When enabled)

Before downloading each image, the app tries to find the full-resolution version by:

#### Pattern Matching:
- **Removes `/thumbnail/`** from URLs  
  Example: `https://site.com/thumbnail/data/image.jpg` ? `https://site.com/data/image.jpg`

- **Removes size suffixes**  
  Patterns checked: `_800x800`, `_small`, `_medium`, `_thumb`, `_preview`, `-thumb`, `-preview`  
  Example: `image_800x800.jpg` ? `image.jpg`

- **Removes "thumb" or "preview" text**  
  Example: `thumb_image.jpg` ? `_image.jpg`

#### URL Validation:
- Sends a **HEAD request** to check if the full-resolution URL exists
- If it exists (HTTP 200), uses that URL
- If it fails, falls back to the original preview URL

---

## New Status Indicators

### ? Done
Full-resolution image downloaded successfully

### ? Backup  
Preview/thumbnail was downloaded instead of full-resolution because:
- Full-resolution detection failed
- Full-resolution URL didn't exist
- Network error during full-res check

---

## Settings

### **Skip full-resolution check** (NEW)
- **Checkbox in Settings** ? "Full Resolution Detection"
- **Default**: Unchecked (full-res detection enabled)
- **When checked**: Downloads original URLs found during scan (faster, no extra network checks)
- **When unchecked**: Attempts to find and download full-resolution versions

**Use Case for Checking:**
- Faster downloads (no extra HEAD requests)
- Sites where preview URLs are already full-resolution
- Bandwidth concerns (HEAD requests use bandwidth)

---

## User Workflow

### Scenario 1: Full-Resolution Found
1. Scan finds: `https://site.com/thumbnail/data/image_800x800.jpg`
2. App detects pattern `/thumbnail/` and `_800x800`
3. Tests: `https://site.com/data/image.jpg`
4. ? URL exists ? Downloads full-resolution
5. Status: **"? Done"**

### Scenario 2: Full-Resolution Not Found
1. Scan finds: `https://site.com/preview_image.jpg`
2. App tries removing "preview"
3. Tests: `https://site.com/_image.jpg`
4. ? URL returns 404
5. Falls back to original: `https://site.com/preview_image.jpg`
6. Status: **"? Backup"** (downloaded preview)

### Scenario 3: Skip Check Enabled
1. Scan finds: `https://site.com/thumbnail/image.jpg`
2. Setting "Skip full-resolution check" is ON
3. Downloads original URL directly (no pattern matching)
4. Status: **"? Done"**

---

## Technical Implementation

### New Methods Added

#### `TryFindFullResolutionUrlAsync(string previewUrl, CancellationToken)`
```csharp
private async Task<string?> TryFindFullResolutionUrlAsync(string previewUrl, CancellationToken cancellationToken)
{
    // Tries multiple URL transformation patterns
    // Returns full-res URL if found, otherwise returns original
}
```

#### `TestUrlExistsAsync(string url, CancellationToken)`
```csharp
private async Task<bool> TestUrlExistsAsync(string url, CancellationToken cancellationToken)
{
    // Sends HEAD request to check if URL exists
   // Returns true if HTTP 200, false otherwise
}
```

### Modified Methods

#### `DownloadSingleItemAsync`
```csharp
// Before downloading:
if (!_settings.SkipFullResolutionCheck)
{
    var fullResUrl = await TryFindFullResolutionUrlAsync(item.Url, cancellationToken);
    if (fullResUrl != item.Url)
    {
   urlToDownload = fullResUrl; // Use full-res
    }
    else
    {
        usedBackup = true; // Flag for "Backup" status
    }
}

// After download:
if (usedBackup)
    item.Status = "? Backup";
else
    item.Status = "? Done";
```

---

## Patterns Detected

| Original URL | Transformed URL | Pattern |
|-------------|----------------|---------|
| `/thumbnail/data/img.jpg` | `/data/img.jpg` | Remove `/thumbnail/` |
| `image_800x800.jpg` | `image.jpg` | Remove size suffix |
| `thumb_photo.jpg` | `_photo.jpg` | Remove "thumb" text |
| `preview-image.jpg` | `-image.jpg` | Remove "preview" text |
| `photo_small.jpg` | `photo.jpg` | Remove "_small" suffix |
| `pic_medium.png` | `pic.png` | Remove "_medium" suffix |

---

## Performance Impact

### With Full-Resolution Check (Default):
- **Extra HEAD request** per image (~100-200ms each)
- **Total time**: Slightly longer (seconds to minutes depending on image count)
- **Benefit**: Higher quality images

### Without Full-Resolution Check (Skipped):
- **No extra requests**
- **Faster downloads**
- **Trade-off**: May download lower quality previews

---

## Testing Recommendations

### Test Case 1: Kemono.cr
1. Scan a Kemono.cr gallery post
2. Check Status column:
   - **"? Done"**: Full-resolution downloaded
   - **"? Backup"**: Preview downloaded
3. Compare file sizes (full-res should be larger)
4. Check image resolution (right-click ? Properties ? Details)

### Test Case 2: With Skip Enabled
1. Go to Settings ? Check "Skip full-resolution check"
2. Scan the same Kemono.cr post
3. Downloads should be faster
4. All status should show **"? Done"**
5. File sizes should be smaller (previews)

### Test Case 3: Unknown Site
1. Scan a site you haven't tested
2. Mix of "? Done" and "? Backup" is normal
3. "? Backup" means pattern matching didn't find full-res

---

## Troubleshooting

### All Images Show "? Backup"
**Possible Causes:**
- Site uses unique URL patterns not covered by detection
- Full-resolution images behind authentication
- Site blocks HEAD requests

**Solutions:**
- Enable "Skip full-resolution check" to download previews
- Inspect URLs manually in browser DevTools
- Add custom pattern matching for specific sites

### Downloads Very Slow
**Cause:** HEAD requests adding extra time per image

**Solutions:**
- Enable "Skip full-resolution check" for faster downloads
- Use "Fast Scan" mode
- Reduce number of images with size filters

### Mix of "Done" and "Backup"
**This is normal!** It means:
- Some images: Full-resolution found and downloaded
- Some images: Pattern matching failed, downloaded preview
- Check file sizes to verify quality

---

## Future Enhancements

Potential improvements:

### Site-Specific Patterns
Add detection for specific popular sites:
```csharp
// Kemono-specific
if (url.Contains("kemono.cr"))
{
    // Custom pattern logic
}
```

### HTML Parsing Method
Instead of URL patterns, parse the HTML for:
- `<a href="full.jpg"><img src="preview.jpg"></a>`
- `data-full`, `data-original` attributes

### User-Defined Patterns
Allow users to add custom URL transformation rules in settings

---

## Summary

? **Implemented**: Full-resolution detection with URL pattern matching  
? **Settings UI**: Toggle to skip full-res checks  
? **Status Indicators**: "? Done" vs "? Backup"  
? **Fallback Logic**: Always downloads something (never fails silently)  
? **Performance Option**: Skip checks for faster downloads  

**Result**: Higher quality images when available, with graceful fallback to previews.

---

**Version**: 1.3  
**Build Status**: ? Successful  
**New Setting**: Skip Full-Resolution Check  
**New Status**: ? Backup
