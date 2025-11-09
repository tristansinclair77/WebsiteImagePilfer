# Website Image Pilfer - Quick Start Guide

## ?? Getting Started in 3 Steps

### 1. Enter a URL
Type or paste any webpage URL in the text box at the top.
```
Example: https://en.wikipedia.org/wiki/Photography
```

### 2. Choose Where to Save (Optional)
- Default location: `My Pictures\WebsiteImages`
- Click **Browse...** to choose a different folder
- Your choice is remembered for next time

### 3. Click "Scan & Download"
Watch as the app:
- Finds all images on the page
- Downloads them automatically
- Shows progress in real-time

---

## ??? Advanced Features

### Cancel Downloads
Click the **Cancel** button anytime to stop downloading.
- Shows how many images were successfully downloaded
- Partially downloaded images are not saved

### Adjust Settings
Click **Settings...** to configure:

#### ?? Filter by Size
- Skip tiny images (icons, logos)
- Adjust slider from 1KB to 100KB
- Useful for getting only full-size images

#### ??? Thumbnail Previews
- See small previews of downloaded images
- Toggle on/off based on preference
- Helps verify downloads visually

#### ?? File Type Filters
- **JPG Only**: Download only JPEG images
- **PNG Only**: Download only PNG images
- Leave both unchecked for all image types

---

## ?? Understanding the Results

### Status Indicators
Each image shows one of these statuses:

| Status | Meaning |
|--------|---------|
| Downloading... | Currently being downloaded |
| ? Done | Successfully downloaded |
| ? Failed | Download failed (network error, 404, etc.) |
| ? Skipped (too small) | Image smaller than minimum size setting |
| ? Skipped (not JPG/PNG) | Filtered out by file type setting |
| ? Cancelled | Download cancelled by user |

### Progress Information
Bottom status bar shows:
- **Downloaded**: Number of successful downloads
- **Skipped**: Number of filtered/failed images
- **Remaining**: Images left to process

---

## ?? Tips & Tricks

### Best Practices
1. **Test with smaller pages first** - Try simple pages before large galleries
2. **Use size filtering** - Set minimum size to 10KB to skip icons and thumbnails
3. **Check your folder** - Verify save location before scanning large galleries
4. **Cancel if needed** - Don't hesitate to cancel if too many unwanted images appear

### Common Issues

#### "No images found"
- Page might use JavaScript to load images (not supported yet)
- Images might be in iframes (not detected)
- Page might have no actual images

#### Downloads are slow
- Website might have rate limiting
- Your internet connection might be slow
- Consider cancelling and trying a different page

#### Some images fail
- Image links might be broken (404 errors)
- Website might require authentication
- Images might be protected/blocked
- Network timeout (after 30 seconds)

---

## ?? Example Workflows

### Downloading Wallpapers
1. Go to wallpaper website
2. Enter URL of gallery page
3. Enable size filter (set to 50KB+)
4. Scan & Download
5. Result: Only high-quality wallpapers downloaded

### Saving Blog Images
1. Enter blog post URL
2. Keep default settings
3. Scan & Download
4. Result: All inline images saved

### Collecting Icons
1. Enter webpage URL
2. Disable size filter (or set very low)
3. Optional: Filter by PNG if icons are PNG
4. Scan & Download
5. Result: All small images including icons

---

## ?? Settings Reference

### Filter images by minimum size
- **When to use**: Skip thumbnails, icons, and small images
- **Recommended values**:
  - 5KB: Skip tiny icons
  - 20KB: Skip most thumbnails
  - 50KB: Get only substantial images
  - 100KB: Get only large images

### Show thumbnail previews
- **Enable**: See what you downloaded at a glance
- **Disable**: Faster performance, cleaner list

### File type filters
- **JPG Only**: Photography, photos, complex images
- **PNG Only**: Graphics, logos, transparent images
- **Neither**: Get all image types (recommended)

---

## ?? Troubleshooting

### App won't start
- Ensure .NET 8 runtime is installed
- Check Windows version (Windows 10+ required)

### Can't select folder
- Check folder permissions
- Try selecting a different folder
- Run as administrator if needed

### Settings not saved
- App might not have write permission
- Settings stored in user profile folder
- Check AppData\Local folder permissions

### Out of disk space
- Check available disk space
- Choose different download folder
- Cancel download and free up space

---

## ?? Getting Help

### Error Messages
The app provides detailed error messages for:
- Invalid URLs
- Network problems
- File system issues
- Permission problems

### Status Bar
Always check the status bar for current operation status and helpful information.

### Cancellation
If anything goes wrong, click **Cancel** to stop the operation safely.

---

## ?? For Best Results

1. ? **Start small**: Test with simple pages first
2. ? **Configure settings**: Adjust filters to your needs
3. ? **Monitor progress**: Watch the status bar and list
4. ? **Review results**: Check the download folder after completion
5. ? **Experiment**: Try different settings to find what works best

---

## ?? Notes

- Images are saved with their original filenames when possible
- Duplicate filenames get numbers added: `image.jpg`, `image_1.jpg`, `image_2.jpg`
- Invalid characters in filenames are automatically replaced
- Maximum filename length is 200 characters
- Only one download operation can run at a time
- Your last URL and folder choice are remembered

---

**Enjoy downloading! ??**

For detailed technical information, see README.md
