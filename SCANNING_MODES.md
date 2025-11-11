# WebsiteImagePilfer - Fast vs Thorough Scan

## Overview

The scanner now has two distinct modes:

### **Fast Scan** (Truly Fast!)
- **No Selenium/Browser** - Uses simple HTTP request
- **No JavaScript execution** - Just parses static HTML
- **Basic extraction only** - Finds `<img>` tags with `src` attributes
- **Quick regex search** - Looks for image URLs in HTML
- **Perfect for**: Static websites, simple image galleries, quick previews

### **Thorough Scan** (Configurable Layers)
- **Optional Selenium** - Can run with or without browser automation
- **Configurable features** - Enable only the detection layers you need
- **JavaScript execution** - For SPAs and dynamic content (when Selenium is enabled)
- **Perfect for**: JavaScript-heavy sites, SPAs, sites with lazy-loading

## Settings Options

All thorough scan options are in **Settings > Thorough Scan Options**:

| Option | Description | Performance Impact |
|--------|-------------|-------------------|
| **Use browser automation** | Launches Chrome to execute JavaScript | High (5-10s overhead) |
| **Check CSS background-image** | Finds images in CSS styles | Low |
| **Check lazy-load data attributes** | Checks data-src, data-url, etc. | Low |
| **Search script tags** | Searches JavaScript code for URLs | Medium (slower parsing) |
| **Check Shadow DOM** | Scans web component shadow roots | Low (experimental) |
| **Save HTML source and screenshot** | Creates debug files | Low (disk I/O) |

## Recommendations

### For Most Websites (Blogs, News, Portfolios)
```
? Fast Scan - Fast, efficient, finds most images
```

### For JavaScript Sites (React, Vue, Angular)
```
? Thorough Scan with:
   ? Use browser automation
   ? Check CSS background-image
   ? Check lazy-load data attributes
   ? Search script tags (usually not needed)
   ? Check Shadow DOM (rarely needed)
   ? Save debug files (only when debugging)
```

### For Troubleshooting
```
? Thorough Scan with:
   ? Use browser automation
   ? All detection options enabled
   ? Save debug files
```
Then check the `debug` folder for HTML source and screenshots.

## Performance Comparison

| Scan Type | Typical Speed | Browser Launch | JavaScript Execution |
|-----------|---------------|----------------|---------------------|
| **Fast** | 1-3 seconds | ? No | ? No |
| **Thorough (no Selenium)** | 2-4 seconds | ? No | ? No |
| **Thorough (with Selenium)** | 15-30 seconds | ? Yes | ? Yes |

## Tips

1. **Start with Fast Scan** - Try this first on any website
2. **Use Thorough only when needed** - If Fast scan finds images, you're done!
3. **Disable unneeded features** - Each detection layer adds processing time
4. **Enable debug files** - Only when troubleshooting specific sites
5. **Selenium is expensive** - Use only for JavaScript-heavy sites

## Example Workflows

### Workflow 1: Quick Image Download
1. Enter URL
2. Use **Fast Scan** ?
3. Download images
4. Done in seconds!

### Workflow 2: JavaScript Site
1. Enter URL  
2. Try **Fast Scan** first ?
3. If few/no results ? Try **Thorough Scan** with Selenium
4. Review results and download

### Workflow 3: Troubleshooting
1. Enter URL
2. Use **Thorough Scan** with all options enabled
3. Enable "Save debug files"
4. Check `debug/` folder for HTML and screenshot
5. Identify if site requires authentication or has bot protection

## Technical Details

### Fast Scan Process
```
1. HTTP GET request to URL
2. Parse HTML with HtmlAgilityPack
3. Extract <img src="..."> URLs
4. Regex search for image URLs
5. Return results
```

### Thorough Scan Process (with Selenium)
```
1. Launch headless Chrome
2. Navigate to URL
3. Wait for JavaScript to execute
4. Scroll page to trigger lazy-loading
5. Extract images using configured layers:
   - <img> tags
   - CSS background-images (if enabled)
   - Data attributes (if enabled)
   - Script tags (if enabled)
   - Shadow DOM (if enabled)
6. Save debug files (if enabled)
7. Close browser
8. Return results
```

