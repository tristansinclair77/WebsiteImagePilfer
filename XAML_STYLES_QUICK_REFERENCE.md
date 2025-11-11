# XAML Styles Quick Reference Guide

## ?? Available Styles - Cheat Sheet

### ?? Buttons

| Style Name | Width | Height | Use For |
|------------|-------|--------|---------|
| `StandardButton` | 80px | 30px | Default action buttons |
| `NarrowButton` | 60px | 30px | Compact buttons |
| `WideButton` | 100px | 30px | Buttons with longer text |
| `ExtraWideButton` | 130px | 30px | Buttons with very long text |
| `SmallButton` | 80px | 25px | Secondary/toolbar buttons |
| `SmallWideButton` | 100px | 25px | Small buttons with more text |
| `SmallNarrowButton` | 60px | 25px | Compact toolbar buttons |
| `TinyButton` | 80px | 22px | Filter controls, inline actions |
| `PrimaryButton` | 80px | 30px | Primary action (blue) |
| `PrimaryWideButton` | 100px | 30px | Primary wide action (blue) |
| `PrimaryExtraWideButton` | 130px | 30px | Primary extra wide (blue) |

**Usage:**
```xaml
<Button Style="{StaticResource PrimaryButton}" Content="Save" Click="..."/>
<Button Style="{StaticResource StandardButton}" Content="Cancel" Click="..."/>
```

---

### ?? TextBox

| Style Name | Height | Features |
|------------|--------|----------|
| `StandardTextBox` | 30px | Centered content, hover/focus effects |
| `SmallTextBox` | 25px | Smaller variant for compact layouts |

**Usage:**
```xaml
<TextBox Style="{StaticResource StandardTextBox}" Text="{Binding Url}"/>
```

---

### ?? CheckBox

| Style Name | Margin | Use For |
|------------|--------|---------|
| `StandardCheckBox` | 0,0,15,5 | General use |
| `FilterCheckBox` | 0,0,15,5 | Status filters |
| `SimpleCheckBox` | 0,0,0,5 | Settings/forms |

**Usage:**
```xaml
<CheckBox Style="{StaticResource FilterCheckBox}" Content="Ready" IsChecked="True"/>
```

---

### ?? TextBlock

| Style Name | Features | Use For |
|------------|----------|---------|
| `BaseTextBlockStyle` | Default font, 12px | Base for all text |
| `HeaderText` | 16px, Bold | Page headers |
| `LabelText` | Centered, right margin | Form labels |
| `HelpText` | 10px, Italic, Gray | Help/hint text |
| `ErrorText` | Red, ellipsis | Error messages |
| `PageInfoText` | Centered, min-width | Pagination info |

**Usage:**
```xaml
<TextBlock Style="{StaticResource HeaderText}" Text="Settings"/>
<TextBlock Style="{StaticResource LabelText}" Text="Save to:"/>
<TextBlock Style="{StaticResource HelpText}" Text="This is a helpful hint..."/>
```

---

### ?? Other Controls

| Style Name | Control | Features |
|------------|---------|----------|
| `StandardGroupBox` | GroupBox | Standard margins, padding, border |
| `StandardProgressBar` | ProgressBar | 20px height, 0-100 range |
| `StandardStatusBar` | StatusBar | 25px height |
| `StandardExpander` | Expander | Bottom margin |
| `FilterBorder` | Border | Light gray bg, border |
| `StandardListView` | ListView | Bottom margin |
| `StandardSlider` | Slider | Centered vertically |

**Usage:**
```xaml
<GroupBox Style="{StaticResource StandardGroupBox}" Header="Options">
    <!-- content -->
</GroupBox>
<ProgressBar Style="{StaticResource StandardProgressBar}" Value="50"/>
```

---

## ?? Dimension Resources

### Button Dimensions
```xaml
{StaticResource ButtonHeightStandard}     <!-- 30px -->
{StaticResource ButtonHeightSmall}        <!-- 25px -->
{StaticResource ButtonHeightTiny}    <!-- 22px -->
{StaticResource ButtonWidthNarrow}        <!-- 60px -->
{StaticResource ButtonWidthStandard}      <!-- 80px -->
{StaticResource ButtonWidthWide}          <!-- 100px -->
{StaticResource ButtonWidthExtraWide}     <!-- 130px -->
```

### TextBox Dimensions
```xaml
{StaticResource TextBoxHeightStandard}  <!-- 30px -->
{StaticResource TextBoxHeightSmall}       <!-- 25px -->
```

### GridView Column Widths
```xaml
{StaticResource ColumnWidthTiny}        <!-- 40px - for # column -->
{StaticResource ColumnWidthNarrow}  <!-- 80px - for Status -->
{StaticResource ColumnWidthMedium}        <!-- 150px - for Preview, Filename -->
{StaticResource ColumnWidthWide}       <!-- 280px - for Error -->
{StaticResource ColumnWidthExtraWide}     <!-- 320px - for URL -->
```

---

## ?? Margin & Padding Resources

### Uniform Spacing
```xaml
{StaticResource MarginTiny}       <!-- 2 on all sides -->
{StaticResource MarginSmall}        <!-- 5 on all sides -->
{StaticResource MarginStandard}         <!-- 10 on all sides -->
{StaticResource MarginLarge}   <!-- 15 on all sides -->

{StaticResource PaddingSmall}      <!-- 5 on all sides -->
{StaticResource PaddingStandard}       <!-- 10 on all sides -->
{StaticResource PaddingLarge}             <!-- 15 on all sides -->
```

### Directional Margins
```xaml
<!-- Right Margins -->
{StaticResource MarginRightTiny}          <!-- 0,0,2,0 -->
{StaticResource MarginRightSmall}     <!-- 0,0,5,0 -->
{StaticResource MarginRightStandard}      <!-- 0,0,10,0 -->
{StaticResource MarginRightLarge}         <!-- 0,0,15,0 -->

<!-- Bottom Margins -->
{StaticResource MarginBottomSmall}        <!-- 0,0,0,5 -->
{StaticResource MarginBottomStandard}     <!-- 0,0,0,10 -->
{StaticResource MarginBottomLarge}        <!-- 0,0,0,15 -->

<!-- Top Margins -->
{StaticResource MarginTopSmall}   <!-- 0,5,0,0 -->
{StaticResource MarginTopStandard}        <!-- 0,10,0,0 -->

<!-- Special -->
{StaticResource MarginCheckBox}           <!-- 0,0,15,5 - for filters -->
```

**Usage:**
```xaml
<Button Margin="{StaticResource MarginRightSmall}" Content="OK"/>
<GroupBox Margin="{StaticResource MarginBottomStandard}" Header="..."/>
```

---

## ?? Color Resources

### Primary Colors
```xaml
{StaticResource PrimaryBrush}     <!-- #007ACC - Main brand color -->
{StaticResource PrimaryHoverBrush}        <!-- #005A9E - Hover state -->
{StaticResource PrimaryPressedBrush}  <!-- #004578 - Pressed state -->
```

### Text Colors
```xaml
{StaticResource TextPrimaryBrush}  <!-- #000000 - Main text -->
{StaticResource TextSecondaryBrush}       <!-- #666666 - Secondary text -->
{StaticResource TextDisabledBrush}     <!-- #AAAAAA - Disabled text -->
{StaticResource TextErrorBrush}  <!-- #D13438 - Error text -->
```

### Background Colors
```xaml
{StaticResource BackgroundBrush}          <!-- #FFFFFF - Main background -->
{StaticResource BackgroundAltBrush}       <!-- #F9F9F9 - Alt background -->
{StaticResource BackgroundHoverBrush}     <!-- #E5F3FF - Hover background -->
```

### Border Colors
```xaml
{StaticResource BorderBrush}    <!-- #CCCCCC - Default border -->
{StaticResource BorderHoverBrush}       <!-- #007ACC - Hover border -->
{StaticResource BorderFocusBrush}         <!-- #007ACC - Focus border -->
```

### Status Colors
```xaml
{StaticResource SuccessBrush}      <!-- #107C10 - Success/Done -->
{StaticResource WarningBrush}             <!-- #FFC83D - Warning -->
{StaticResource ErrorBrush}        <!-- #E81123 - Error/Failed -->
{StaticResource InfoBrush}            <!-- #007ACC - Info -->
```

**Usage:**
```xaml
<Border Background="{StaticResource BackgroundAltBrush}" 
        BorderBrush="{StaticResource BorderBrush}"/>
```

---

## ?? Common Patterns

### Form Row (Label + Control)
```xaml
<Grid>
    <Grid.ColumnDefinitions>
        <ColumnDefinition Width="Auto"/>
    <ColumnDefinition Width="*"/>
    </Grid.ColumnDefinitions>
    
    <TextBlock Grid.Column="0" 
      Style="{StaticResource LabelText}"
 Text="URL:"/>
  
    <TextBox Grid.Column="1" 
    Style="{StaticResource StandardTextBox}"/>
</Grid>
```

### Button Group
```xaml
<StackPanel Orientation="Horizontal">
    <Button Style="{StaticResource PrimaryButton}" 
     Content="OK" 
            Margin="{StaticResource MarginRightStandard}"/>
    <Button Style="{StaticResource StandardButton}" 
         Content="Cancel"/>
</StackPanel>
```

### Filter CheckBox List
```xaml
<WrapPanel>
    <CheckBox Style="{StaticResource FilterCheckBox}" Content="Ready"/>
    <CheckBox Style="{StaticResource FilterCheckBox}" Content="Done"/>
    <CheckBox Style="{StaticResource FilterCheckBox}" Content="Failed"/>
</WrapPanel>
```

### Settings Section
```xaml
<GroupBox Style="{StaticResource StandardGroupBox}" Header="Options">
    <StackPanel Margin="{StaticResource PaddingStandard}">
        <CheckBox Style="{StaticResource SimpleCheckBox}" Content="Enable feature"/>
        <TextBlock Style="{StaticResource HelpText}" 
            Text="This feature does something helpful"/>
    </StackPanel>
</GroupBox>
```

---

## ?? How to Extend

### Add a New Button Style
In `Styles/ButtonStyles.xaml`:
```xaml
<Style x:Key="MyCustomButton" TargetType="Button" 
  BasedOn="{StaticResource BaseButtonStyle}">
    <Setter Property="Width" Value="120"/>
    <Setter Property="Background" Value="Green"/>
</Style>
```

### Add a New Color
In `Styles/Colors.xaml`:
```xaml
<SolidColorBrush x:Key="MyCustomBrush" Color="#FF5733"/>
```

### Add a New Dimension
In `Styles/Dimensions.xaml`:
```xaml
<sys:Double x:Key="MyCustomWidth">150</sys:Double>
```

### Add a New Margin Pattern
In `Styles/Dimensions.xaml`:
```xaml
<Thickness x:Key="MarginLeftStandard">10,0,0,0</Thickness>
```

---

## ? Best Practices

### DO ?
- Always use style references instead of hardcoded values
- Use semantic style names (`PrimaryButton` not `BlueButton`)
- Extend existing styles with `BasedOn` when possible
- Use appropriate margin resources for consistent spacing
- Reference colors by brush names, never hex codes

### DON'T ?
- Don't hardcode `Width`, `Height`, `Margin`, or colors in XAML
- Don't create inline styles in windows
- Don't duplicate style definitions
- Don't use numeric values directly (use resources)
- Don't mix resource and hardcoded values

### Example - Before & After

? **Before (Bad):**
```xaml
<Button Content="Save" Width="80" Height="30" Margin="0,0,5,0" 
        Background="#007ACC" Foreground="White"/>
```

? **After (Good):**
```xaml
<Button Style="{StaticResource PrimaryButton}" Content="Save" 
        Margin="{StaticResource MarginRightSmall}"/>
```

---

## ?? Quick Lookup Table

**Need a button for...?**
- Primary action ? `PrimaryButton`
- Secondary action ? `StandardButton`
- Cancel/Close ? `StandardButton`
- Toolbar action ? `SmallButton`
- Quick filter ? `TinyButton`

**Need spacing of...?**
- 2px ? `MarginTiny`
- 5px ? `MarginSmall` or `MarginRightSmall`, etc.
- 10px ? `MarginStandard` or `MarginRightStandard`, etc.
- 15px ? `MarginLarge` or `MarginRightLarge`, etc.

**Need text for...?**
- Page title ? `HeaderText`
- Form label ? `LabelText`
- Help hint ? `HelpText`
- Error message ? `ErrorText`

**Need a color for...?**
- Primary action ? `PrimaryBrush`
- Error text ? `ErrorBrush` or `TextErrorBrush`
- Border ? `BorderBrush`
- Background ? `BackgroundBrush`

---

## ?? Need Help?

1. Check if a style exists in `Styles/ButtonStyles.xaml`, `TextStyles.xaml`, or `ControlStyles.xaml`
2. Check if a dimension exists in `Styles/Dimensions.xaml`
3. Check if a color exists in `Styles/Colors.xaml`
4. If not found, add it following the existing patterns
5. Always test your changes with `run_build`

---

**Remember:** The goal is consistency, maintainability, and semantic meaning. When in doubt, follow the existing patterns! ??
