# XAML Style Migration Guide

## ?? How to Migrate Existing XAML to Use Styles

This guide helps you convert existing XAML files to use the new resource dictionary styles.

---

## Step 1: Identify Hardcoded Values

### ?? What to Look For

Look for these patterns in your XAML:

```xaml
<!-- Hardcoded button dimensions -->
Width="80" Height="30"
Width="100" Height="25"

<!-- Hardcoded margins -->
Margin="0,0,10,0"
Margin="0,0,5,0"
Margin="10"

<!-- Hardcoded colors -->
Foreground="Red"
Background="#CCCCCC"
BorderBrush="#007ACC"

<!-- Hardcoded text properties -->
FontSize="10" FontStyle="Italic" Foreground="Gray"

<!-- Repeated control properties -->
VerticalContentAlignment="Center" Padding="5"
```

---

## Step 2: Replace with Style References

### Button Migration

#### Pattern 1: Standard Action Button
```xaml
<!-- BEFORE -->
<Button Content="OK" Width="80" Height="30" Click="..."/>

<!-- AFTER -->
<Button Style="{StaticResource StandardButton}" Content="OK" Click="..."/>
```

#### Pattern 2: Primary Action Button
```xaml
<!-- BEFORE -->
<Button Content="Save" Width="80" Height="30" Background="#007ACC" 
Foreground="White" Click="..."/>

<!-- AFTER -->
<Button Style="{StaticResource PrimaryButton}" Content="Save" Click="..."/>
```

#### Pattern 3: Wide Button
```xaml
<!-- BEFORE -->
<Button Content="Download" Width="100" Height="30" Click="..."/>

<!-- AFTER -->
<Button Style="{StaticResource WideButton}" Content="Download" Click="..."/>
```

#### Pattern 4: Small Toolbar Button
```xaml
<!-- BEFORE -->
<Button Content="Browse..." Width="80" Height="25" Click="..."/>

<!-- AFTER -->
<Button Style="{StaticResource SmallButton}" Content="Browse..." Click="..."/>
```

#### Pattern 5: Tiny Filter Button
```xaml
<!-- BEFORE -->
<Button Content="Select All" Width="80" Height="22" Click="..."/>

<!-- AFTER -->
<Button Style="{StaticResource TinyButton}" Content="Select All" Click="..."/>
```

**Decision Tree:**
- Primary action (Save, OK, Scan)? ? `PrimaryButton`, `PrimaryWideButton`, or `PrimaryExtraWideButton`
- Secondary action (Cancel, Close)? ? `StandardButton`, `WideButton`, or `ExtraWideButton`
- Toolbar action? ? `SmallButton`, `SmallWideButton`, or `SmallNarrowButton`
- Filter/inline action? ? `TinyButton`

---

### TextBox Migration

```xaml
<!-- BEFORE -->
<TextBox Height="30" VerticalContentAlignment="Center" Padding="5"/>

<!-- AFTER -->
<TextBox Style="{StaticResource StandardTextBox}"/>
```

```xaml
<!-- BEFORE -->
<TextBox Height="25" VerticalContentAlignment="Center" Padding="5"/>

<!-- AFTER -->
<TextBox Style="{StaticResource SmallTextBox}"/>
```

---

### TextBlock Migration

#### Label Text
```xaml
<!-- BEFORE -->
<TextBlock Text="URL:" VerticalAlignment="Center" Margin="0,0,10,0"/>

<!-- AFTER -->
<TextBlock Style="{StaticResource LabelText}" Text="URL:"/>
```

#### Header Text
```xaml
<!-- BEFORE -->
<TextBlock Text="Settings" FontSize="16" FontWeight="Bold" Margin="0,0,0,15"/>

<!-- AFTER -->
<TextBlock Style="{StaticResource HeaderText}" Text="Settings"/>
```

#### Help Text
```xaml
<!-- BEFORE -->
<TextBlock Text="This is helpful info" FontSize="10" FontStyle="Italic" 
      Foreground="Gray" Margin="0,5,0,0" TextWrapping="Wrap"/>

<!-- AFTER -->
<TextBlock Style="{StaticResource HelpText}" Text="This is helpful info"/>
```

#### Error Text
```xaml
<!-- BEFORE -->
<TextBlock Text="{Binding ErrorMessage}" Foreground="Red" 
           TextTrimming="CharacterEllipsis"/>

<!-- AFTER -->
<TextBlock Style="{StaticResource ErrorText}" Text="{Binding ErrorMessage}"/>
```

---

### CheckBox Migration

#### Filter CheckBox
```xaml
<!-- BEFORE -->
<CheckBox Content="Ready" Margin="0,0,15,5" IsChecked="True"/>

<!-- AFTER -->
<CheckBox Style="{StaticResource FilterCheckBox}" Content="Ready" IsChecked="True"/>
```

#### Simple CheckBox (Settings)
```xaml
<!-- BEFORE -->
<CheckBox Content="Enable feature" Margin="0,0,0,5"/>

<!-- AFTER -->
<CheckBox Style="{StaticResource SimpleCheckBox}" Content="Enable feature"/>
```

---

### GroupBox Migration

```xaml
<!-- BEFORE -->
<GroupBox Header="Options" Margin="0,0,0,10" Padding="10" 
      BorderBrush="#CCCCCC" BorderThickness="1">

<!-- AFTER -->
<GroupBox Style="{StaticResource StandardGroupBox}" Header="Options">
```

---

### Other Control Migrations

#### ProgressBar
```xaml
<!-- BEFORE -->
<ProgressBar Height="20" Margin="0,0,0,10" Minimum="0" Maximum="100"/>

<!-- AFTER -->
<ProgressBar Style="{StaticResource StandardProgressBar}"/>
```

#### StatusBar
```xaml
<!-- BEFORE -->
<StatusBar Height="25">

<!-- AFTER -->
<StatusBar Style="{StaticResource StandardStatusBar}">
```

#### Expander
```xaml
<!-- BEFORE -->
<Expander Header="Filters" Margin="0,0,0,10">

<!-- AFTER -->
<Expander Style="{StaticResource StandardExpander}" Header="Filters">
```

#### ListView
```xaml
<!-- BEFORE -->
<ListView Margin="0,0,0,10">

<!-- AFTER -->
<ListView Style="{StaticResource StandardListView}">
```

---

## Step 3: Replace Margin Values

### Common Margin Patterns

```xaml
<!-- BEFORE: Right margin of 5px -->
Margin="0,0,5,0"
<!-- AFTER -->
Margin="{StaticResource MarginRightSmall}"

<!-- BEFORE: Right margin of 10px -->
Margin="0,0,10,0"
<!-- AFTER -->
Margin="{StaticResource MarginRightStandard}"

<!-- BEFORE: Bottom margin of 10px -->
Margin="0,0,0,10"
<!-- AFTER -->
Margin="{StaticResource MarginBottomStandard}"

<!-- BEFORE: Uniform margin of 10px -->
Margin="10"
<!-- AFTER -->
Margin="{StaticResource MarginStandard}"

<!-- BEFORE: CheckBox margin (filter) -->
Margin="0,0,15,5"
<!-- AFTER -->
Margin="{StaticResource MarginCheckBox}"
```

**Common Replacements:**
- `Margin="0,0,5,0"` ? `{StaticResource MarginRightSmall}`
- `Margin="0,0,10,0"` ? `{StaticResource MarginRightStandard}`
- `Margin="0,0,15,0"` ? `{StaticResource MarginRightLarge}`
- `Margin="0,0,0,5"` ? `{StaticResource MarginBottomSmall}`
- `Margin="0,0,0,10"` ? `{StaticResource MarginBottomStandard}`
- `Margin="0,5,0,0"` ? `{StaticResource MarginTopSmall}`
- `Margin="10"` ? `{StaticResource MarginStandard}`
- `Margin="15"` ? `{StaticResource PaddingLarge}` (for containers)

---

## Step 4: Replace Color Values

```xaml
<!-- BEFORE: Hardcoded hex colors -->
Background="#007ACC"
Foreground="White"
BorderBrush="#CCCCCC"
Foreground="Red"
Foreground="Gray"
Background="#F9F9F9"

<!-- AFTER: Named brushes -->
Background="{StaticResource PrimaryBrush}"
Foreground="White"  <!-- Keep White literal -->
BorderBrush="{StaticResource BorderBrush}"
Foreground="{StaticResource ErrorBrush}"
Foreground="{StaticResource TextSecondaryBrush}"
Background="{StaticResource BackgroundAltBrush}"
```

**Color Mapping:**
- `#007ACC` ? `{StaticResource PrimaryBrush}`
- `#CCCCCC` (border) ? `{StaticResource BorderBrush}`
- `#F9F9F9` (background) ? `{StaticResource BackgroundAltBrush}`
- `Red` (error) ? `{StaticResource ErrorBrush}`
- `Gray` (secondary) ? `{StaticResource TextSecondaryBrush}`

---

## Step 5: Replace GridViewColumn Widths

```xaml
<!-- BEFORE -->
<GridViewColumn Header="#" Width="40"/>
<GridViewColumn Header="Status" Width="80"/>
<GridViewColumn Header="Preview" Width="150"/>
<GridViewColumn Header="Error" Width="280"/>
<GridViewColumn Header="URL" Width="320"/>

<!-- AFTER -->
<GridViewColumn Header="#" Width="{StaticResource ColumnWidthTiny}"/>
<GridViewColumn Header="Status" Width="{StaticResource ColumnWidthNarrow}"/>
<GridViewColumn Header="Preview" Width="{StaticResource ColumnWidthMedium}"/>
<GridViewColumn Header="Error" Width="{StaticResource ColumnWidthWide}"/>
<GridViewColumn Header="URL" Width="{StaticResource ColumnWidthExtraWide}"/>
```

---

## Complete Example: Before & After

### Before (Old Style)
```xaml
<Window>
    <Grid Margin="10">
        <Grid.RowDefinitions>
<RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
      <RowDefinition Height="Auto"/>
        </Grid.RowDefinitions>
        
   <!-- Header -->
        <TextBlock Grid.Row="0" Text="Settings" FontSize="16" 
       FontWeight="Bold" Margin="0,0,0,15"/>
        
        <!-- Content -->
        <GroupBox Grid.Row="1" Header="Options" Margin="0,0,0,10" 
      Padding="10" BorderBrush="#CCCCCC" BorderThickness="1">
    <StackPanel>
                <CheckBox Content="Enable feature" Margin="0,0,0,5"/>
      <TextBlock Text="This feature does something" FontSize="10" 
                FontStyle="Italic" Foreground="Gray" 
       Margin="0,5,0,0" TextWrapping="Wrap"/>
            </StackPanel>
        </GroupBox>
        
      <!-- Buttons -->
        <StackPanel Grid.Row="2" Orientation="Horizontal" 
    HorizontalAlignment="Right">
  <Button Content="OK" Width="80" Height="30" 
  Margin="0,0,10,0" Background="#007ACC" 
           Foreground="White"/>
        <Button Content="Cancel" Width="80" Height="30"/>
        </StackPanel>
    </Grid>
</Window>
```

### After (New Style)
```xaml
<Window>
    <Grid Margin="{StaticResource MarginStandard}">
        <Grid.RowDefinitions>
          <RowDefinition Height="Auto"/>
         <RowDefinition Height="*"/>
 <RowDefinition Height="Auto"/>
      </Grid.RowDefinitions>
    
        <!-- Header -->
        <TextBlock Grid.Row="0" 
    Style="{StaticResource HeaderText}" 
           Text="Settings"/>

        <!-- Content -->
        <GroupBox Grid.Row="1" 
         Style="{StaticResource StandardGroupBox}" 
      Header="Options">
            <StackPanel Margin="{StaticResource PaddingStandard}">
                <CheckBox Style="{StaticResource SimpleCheckBox}" 
                Content="Enable feature"/>
       <TextBlock Style="{StaticResource HelpText}" 
      Text="This feature does something"/>
         </StackPanel>
 </GroupBox>
        
        <!-- Buttons -->
   <StackPanel Grid.Row="2" 
          Orientation="Horizontal" 
          HorizontalAlignment="Right">
            <Button Style="{StaticResource PrimaryButton}" 
        Content="OK" 
        Margin="{StaticResource MarginRightStandard}"/>
   <Button Style="{StaticResource StandardButton}" 
          Content="Cancel"/>
        </StackPanel>
    </Grid>
</Window>
```

**Benefits:**
- 60% fewer attributes
- Semantic meaning clear
- Easy to change globally
- Consistent with rest of app

---

## Migration Checklist

For each XAML file:

### Phase 1: Controls
- [ ] Replace all Button Width/Height with style references
- [ ] Replace all TextBox Height/properties with styles
- [ ] Replace all TextBlock font properties with styles
- [ ] Replace all CheckBox properties with styles
- [ ] Replace all GroupBox properties with styles
- [ ] Replace other control properties (ProgressBar, StatusBar, etc.)

### Phase 2: Spacing
- [ ] Replace all Margin="0,0,5,0" patterns
- [ ] Replace all Margin="0,0,10,0" patterns
- [ ] Replace all Margin="0,0,15,0" patterns
- [ ] Replace all Margin="0,0,0,10" patterns
- [ ] Replace all Margin="10" patterns
- [ ] Replace all Padding values

### Phase 3: Colors
- [ ] Replace all #007ACC (primary blue)
- [ ] Replace all #CCCCCC (borders)
- [ ] Replace all #F9F9F9 (alt backgrounds)
- [ ] Replace all "Red" (errors)
- [ ] Replace all "Gray" (secondary text)

### Phase 4: Dimensions
- [ ] Replace GridViewColumn Width values
- [ ] Replace any remaining hardcoded dimensions

### Phase 5: Testing
- [ ] Build successfully
- [ ] Visual verification
- [ ] Functional testing
- [ ] Compare before/after screenshots

---

## Common Mistakes to Avoid

### ? Mistake 1: Mixing Styles and Properties
```xaml
<!-- DON'T DO THIS -->
<Button Style="{StaticResource StandardButton}" Width="100"/>
<!-- The style already sets Width=80, this creates a conflict -->
```

**Fix:** Choose the right style variant instead
```xaml
<Button Style="{StaticResource WideButton}"/>
```

---

### ? Mistake 2: Not Using Margin Resources
```xaml
<!-- DON'T DO THIS -->
<Button Style="{StaticResource StandardButton}" Margin="0,0,10,0"/>
<!-- Hardcoded margin defeats the purpose -->
```

**Fix:** Use margin resources
```xaml
<Button Style="{StaticResource StandardButton}" 
        Margin="{StaticResource MarginRightStandard}"/>
```

---

### ? Mistake 3: Hardcoding Colors in Styled Controls
```xaml
<!-- DON'T DO THIS -->
<TextBlock Style="{StaticResource LabelText}" Foreground="Red"/>
<!-- Use ErrorText style instead -->
```

**Fix:** Use appropriate style
```xaml
<TextBlock Style="{StaticResource ErrorText}" Text="Error message"/>
```

---

### ? Mistake 4: Creating Inline Styles
```xaml
<!-- DON'T DO THIS -->
<Window.Resources>
    <Style x:Key="MyButton" TargetType="Button">
        <Setter Property="Width" Value="80"/>
    </Style>
</Window.Resources>
```

**Fix:** Add to appropriate resource dictionary file
- Buttons ? `Styles/ButtonStyles.xaml`
- Text ? `Styles/TextStyles.xaml`
- Other ? `Styles/ControlStyles.xaml`

---

## Testing Your Migration

After migrating:

1. **Build Test:**
   ```
   Run Build (should succeed with no errors)
 ```

2. **Visual Test:**
   - Open the window in the designer
   - Compare with original appearance
   - Check all controls render correctly

3. **Interaction Test:**
   - Run the application
   - Test button hover effects
   - Test focus states
   - Test all functionality

4. **Consistency Test:**
   - Compare with other migrated windows
   - Verify spacing is consistent
   - Check that colors match

---

## Need Help?

**If a style doesn't exist:**
1. Check `XAML_STYLES_QUICK_REFERENCE.md` for available styles
2. Look in the appropriate resource dictionary file
3. If needed, add a new style following existing patterns
4. Update the quick reference guide

**If unsure which style to use:**
- Refer to the Quick Reference Guide
- Look at similar controls in MainWindow.xaml or SettingsWindow.xaml
- Follow the decision trees in this guide

---

## Summary

**Migration Pattern:**
1. Identify hardcoded values
2. Find appropriate style/resource
3. Replace with `{StaticResource ...}`
4. Test thoroughly

**Key Benefits:**
- ? Consistency across app
- ? Easy to maintain
- ? Semantic naming
- ? Fewer bugs
- ? Professional appearance

**Remember:** Every hardcoded value is a maintenance burden. Use styles and resources everywhere! ??
