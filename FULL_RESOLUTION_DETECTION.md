# Full-Resolution Image Detection

## The Problem

Many image gallery websites display small **preview/thumbnail** images (e.g., 800x800) on the page, but clicking them reveals the **full-resolution** originals. Currently, we're downloading the previews instead of the full-resolution images.

## Common Patterns

### Pattern 1: Link-Wrapped Images
The most common pattern:
```html
<a href="full-image.jpg">
  <img src="preview-image-800x800.jpg">
</a>
```

### Pattern 2: Data Attributes
Some sites store the full-resolution URL in a data attribute:
```html
<img src="preview.jpg" 
     data-full="full-image.jpg"
     data-original="full-image.jpg"
     data-large="full-image.jpg">
```

### Pattern 3: URL Pattern Substitution
Sites like Kemono often use predictable URL patterns:
```
Preview:  https://img.kemono.cr/thumbnail/data/abc/image.jpg
Full-Res: https://img.kemono.cr/data/abc/image.jpg
```

## Solution Strategy

### Priority 1: Check Parent Links
For each `<img>` tag:
1. Check if it's wrapped in an `<a>` tag
2. If yes, check if the `href` points to an image file
3. If yes, use that URL instead of the `src`

```csharp
// Check if image is inside a link
var parentLink = img.ParentNode;
if (parentLink != null && parentLink.Name == "a")
{
    var href = parentLink.GetAttributeValue("href", "");
    if (!string.IsNullOrEmpty(href) && IsImageUrl(href))
    {
      // Use the link href (full-resolution) instead of img src
        imageUrls.Add(ConvertToAbsoluteUrl(href));
        continue; // Skip checking the img src
    }
}
```

### Priority 2: Check Full-Resolution Data Attributes
Check for data attributes that might contain full-resolution URLs:
```csharp
string[] fullResAttributes = { 
    "data-full", 
    "data-original", 
    "data-full-src",
    "data-fullsize",
    "data-large"
};
```

### Priority 3: Fallback to Standard Src
Only if no full-resolution version is found, use the `src` attribute.

## Implementation Steps

To add this feature, modify the `GetImageUrlsWithSeleniumAsync` method:

1. **Find the HTML parsing section** (around line 530):
   ```csharp
   var imgNodes = htmlDoc.DocumentNode.SelectNodes("//img");
   if (imgNodes != null)
   {
       foreach (var img in imgNodes)
       {
   ```

2. **Add full-resolution detection** before checking `src`:
   ```csharp
   // PRIORITY 1: Check parent link
   var parentLink = img.ParentNode;
   if (parentLink != null && parentLink.Name == "a")
   {
  var href = parentLink.GetAttributeValue("href", "");
       if (!string.IsNullOrEmpty(href))
       {
        // Check if it's an image URL
           if (href.Contains(".jpg") || href.Contains(".png") || 
          href.Contains(".webp") || href.Contains(".gif"))
           {
 if (Uri.TryCreate(baseUri, href, out Uri? absoluteUri))
        {
            imageUrls.Add(absoluteUri.ToString());
                continue; // Skip src check
      }
       }
       }
   }
 
   // PRIORITY 2: Check data attributes
   string[] fullResAttrs = { "data-full", "data-original", "data-large" };
   foreach (var attr in fullResAttrs)
   {
       var fullSrc = img.GetAttributeValue(attr, "");
  if (!string.IsNullOrEmpty(fullSrc))
       {
   if (Uri.TryCreate(baseUri, fullSrc, out Uri? absoluteUri))
     {
      imageUrls.Add(absoluteUri.ToString());
           continue; // Skip src check
           }
    }
   }
   
   // PRIORITY 3: Fallback to src (existing code)
   string[] possibleAttributes = { "src", "data-src"... };
   ```

## Testing

After implementing, test with:
1. **Right-click an image** on the gallery page
2. **"Open image in new tab"** - note the URL
3. **Compare** with what the app downloads
4. **Full-res should be**: larger file size, higher resolution

## Expected Results

### Before:
- Downloads: `thumbnail_800x800.jpg` (50KB)
- Resolution: 800x800 pixels

### After:
- Downloads: `full_image_3000x3000.jpg` (2MB)
- Resolution: 3000x3000 pixels

## Site-Specific Notes

### Kemono.cr
- Uses pattern: `/thumbnail/` vs `/data/`
- Preview: `https://img.kemono.cr/thumbnail/data/...`
- Full: `https://img.kemono.cr/data/...`

You could add URL transformation logic:
```csharp
// If URL contains "/thumbnail/", try removing it
if (imageUrl.Contains("/thumbnail/"))
{
    var fullUrl = imageUrl.Replace("/thumbnail/", "/");
 imageUrls.Add(fullUrl);
}
```

## Benefits

? **Higher Quality**: Get original resolution images  
? **Better Detail**: No compression artifacts from thumbnails  
? **Proper Archives**: Save images as originally uploaded  
? **File Size**: Larger files indicate better quality  

## Limitations

? **JavaScript Listeners**: Some sites use `onclick` handlers - can't detect  
? **API Calls**: Images loaded via AJAX after click won't be detected  
? **Login Required**: Full-resolution might be behind authentication  

## Alternative: Manual URL Inspection

If automatic detection fails:
1. Open browser Dev Tools (F12)
2. Click an image on the page
3. Check the Network tab for the full-resolution URL
4. Look for patterns (e.g., `/thumbnail/` ?  `/data/`)
5. Apply URL transformation manually in code

---

**Status**: Not yet implemented (would require careful code modification)  
**Priority**: High - significantly improves download quality  
**Complexity**: Medium - requires understanding HTML structure
