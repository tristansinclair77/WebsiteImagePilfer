# WebsiteImagePilfer - Debugging Guide

## Debugging Image Scanning Issues

If the scanner isn't finding images on a website (like Sora), follow these steps to debug:

### 1. Check the Logs
After running a scan, check the log file located at:
```
<Application Directory>/logs/app_YYYYMMDD.log
```

The logs will show:
- How many URLs were found
- Sample URLs that were detected
- Whether specific patterns were found in the page
- Any errors that occurred

### 2. Check the Saved HTML
After each scan, the page source is saved to:
```
<Application Directory>/debug/page_source_YYYYMMDD_HHMMSS.html
```

Open this file in a text editor and search for:
- Image URLs you expect to find
- Patterns like `/g/gen_` for Sora
- `<img` tags
- `background-image` CSS
- JavaScript data like `__NEXT_DATA__`

### 3. Common Issues

#### Issue: Website requires authentication
**Solution**: The scanner cannot log in to websites. You may need to:
- Use a browser extension to export cookies
- Or scan a public version of the site

#### Issue: Images loaded via Canvas/WebGL
**Solution**: Some sites render images on `<canvas>` elements. These cannot be extracted as URLs.

#### Issue: Images loaded via API after interaction
**Solution**: Some sites only load images after clicking/hovering. The scanner may not trigger these interactions.

#### Issue: Bot detection
**Solution**: The website may detect the headless browser and block content. Try:
- Adding more realistic user agent strings
- Adding delays between requests
- Using a regular (non-headless) browser

### 4. Sora-Specific Issues

The Sora website (`sora.chatgpt.com`) likely:
1. Requires authentication (ChatGPT Plus subscription)
2. Uses React with client-side routing
3. Loads images dynamically via API
4. May use bot detection

If you're getting only 3 images (logos), the page is probably showing a login/landing page instead of the actual gallery.

### 5. Testing Steps

1. **Try with a different website** first to confirm the scanner works (e.g., `https://unsplash.com`)
2. **Check if you can access Sora** without logging in through a regular browser
3. **Look at the saved HTML file** to see what was actually loaded
4. **Check the logs** for specific error messages

