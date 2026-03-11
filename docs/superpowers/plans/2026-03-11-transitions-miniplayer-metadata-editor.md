# Animated Transitions, Mini-Player & Metadata Editor — Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add Fluent slide+fade view transitions, an ultra-compact mini-player mode, and an inline metadata tag editor to Audiomatic.

**Architecture:** All three features extend the existing single-window architecture. Transitions use Storyboard animations on the content containers. Mini-player adds a third collapse state (60px). Metadata editor extends the Raycast-style context menu Flyout with editable fields and writes tags via TagLibSharp.

**Tech Stack:** WinUI 3 (Windows App SDK 1.8), C# / .NET 8, TagLibSharp 2.3.0, Microsoft.Data.Sqlite

---

## File Structure

| File | Action | Responsibility |
|------|--------|----------------|
| `Audiomatic/MainWindow.xaml` | Modify | Add mini-player inline layout in Row 1 |
| `Audiomatic/MainWindow.xaml.cs` | Modify | View transition animation, mini-player state cycling, metadata editor UI |
| `Audiomatic/Services/MetadataWriter.cs` | Create | TagLibSharp write-back for title, artist, album, artwork |
| `Audiomatic/Services/LibraryManager.cs` | Modify | Add `UpdateTrackMetadata()` method |

---

## Chunk 1: Animated View Transitions

### Task 1: Add `AnimateViewTransition` helper method

**Files:**
- Modify: `Audiomatic/MainWindow.xaml.cs:37-39` (add field)
- Modify: `Audiomatic/MainWindow.xaml.cs` (add new method after `UpdateNavigation`)

- [ ] **Step 1: Add animation tracking field**

In `MainWindow.xaml.cs`, after the visualizer fields (line ~39), add:

```csharp
// View transition animation
private bool _isViewTransitioning;
```

- [ ] **Step 2: Add the `AnimateViewTransition` method**

Add this method after `UpdateNavigation()` (after line ~893):

```csharp
private void AnimateViewTransition(Action buildNewContent, bool slideFromRight = true)
{
    if (_isViewTransitioning) return;
    _isViewTransitioning = true;

    // Target the visible content container
    FrameworkElement target = _viewMode == ViewMode.Visualizer
        ? WaveformContainer
        : TrackListView;

    // Ensure it has a TranslateTransform
    if (target.RenderTransform is not TranslateTransform)
        target.RenderTransform = new TranslateTransform();

    var transform = (TranslateTransform)target.RenderTransform;
    double direction = slideFromRight ? -1 : 1;

    // Phase 1: Exit — slide out + fade
    var exitX = new DoubleAnimation
    {
        From = 0,
        To = 30 * direction,
        Duration = new Duration(TimeSpan.FromMilliseconds(120)),
        EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
    };
    Storyboard.SetTarget(exitX, transform);
    Storyboard.SetTargetProperty(exitX, "X");

    var exitOpacity = new DoubleAnimation
    {
        From = 1,
        To = 0,
        Duration = new Duration(TimeSpan.FromMilliseconds(120)),
        EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
    };
    Storyboard.SetTarget(exitOpacity, target);
    Storyboard.SetTargetProperty(exitOpacity, "Opacity");

    var exitStoryboard = new Storyboard();
    exitStoryboard.Children.Add(exitX);
    exitStoryboard.Children.Add(exitOpacity);

    exitStoryboard.Completed += (_, _) =>
    {
        // Build new content between phases
        buildNewContent();

        // Re-target if container changed (e.g. Library→Visualizer)
        FrameworkElement newTarget = _viewMode == ViewMode.Visualizer
            ? WaveformContainer
            : TrackListView;

        if (newTarget.RenderTransform is not TranslateTransform)
            newTarget.RenderTransform = new TranslateTransform();

        var newTransform = (TranslateTransform)newTarget.RenderTransform;

        // Phase 2: Enter — slide in + fade
        var enterX = new DoubleAnimation
        {
            From = -30 * direction,
            To = 0,
            Duration = new Duration(TimeSpan.FromMilliseconds(150)),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        Storyboard.SetTarget(enterX, newTransform);
        Storyboard.SetTargetProperty(enterX, "X");

        var enterOpacity = new DoubleAnimation
        {
            From = 0,
            To = 1,
            Duration = new Duration(TimeSpan.FromMilliseconds(150)),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        Storyboard.SetTarget(enterOpacity, newTarget);
        Storyboard.SetTargetProperty(enterOpacity, "Opacity");

        var enterStoryboard = new Storyboard();
        enterStoryboard.Children.Add(enterX);
        enterStoryboard.Children.Add(enterOpacity);

        enterStoryboard.Completed += (_, _) =>
        {
            _isViewTransitioning = false;
        };

        enterStoryboard.Begin();
    };

    exitStoryboard.Begin();
}
```

- [ ] **Step 3: Add required using for Storyboard animations**

Verify `Microsoft.UI.Xaml.Media.Animation` is imported. Add at the top of `MainWindow.xaml.cs` if missing:

```csharp
using Microsoft.UI.Xaml.Media.Animation;
```

- [ ] **Step 4: Build to verify compilation**

Run: `dotnet build Audiomatic.sln -c Debug`
Expected: Build succeeded

- [ ] **Step 5: Commit**

```bash
git add Audiomatic/MainWindow.xaml.cs
git commit -m "feat: Add AnimateViewTransition helper with slide+fade Storyboard"
```

---

### Task 2: Wire transitions into navigation handlers

**Files:**
- Modify: `Audiomatic/MainWindow.xaml.cs:809-816` (NavLibrary_Click)
- Modify: `Audiomatic/MainWindow.xaml.cs:818-825` (NavPlaylists_Click)
- Modify: `Audiomatic/MainWindow.xaml.cs:1141-1148` (NavQueue_Click)
- Modify: `Audiomatic/MainWindow.xaml.cs:1158-1164` (NavVisualizer_Click)

- [ ] **Step 1: Update `NavLibrary_Click`**

Replace with:

```csharp
private void NavLibrary_Click(object sender, RoutedEventArgs e)
{
    if (_viewMode == ViewMode.Library) return;
    var previousMode = _viewMode;
    _viewMode = ViewMode.Library;
    _currentPlaylist = null;
    UpdateNavigation();
    UpdateSpectrumTimer();
    AnimateViewTransition(() => ApplyFilterAndSort());
}
```

- [ ] **Step 2: Update `NavPlaylists_Click`**

Replace with:

```csharp
private void NavPlaylists_Click(object sender, RoutedEventArgs e)
{
    if (_viewMode == ViewMode.PlaylistList) return;
    _viewMode = ViewMode.PlaylistList;
    _currentPlaylist = null;
    UpdateNavigation();
    UpdateSpectrumTimer();
    AnimateViewTransition(() => LoadPlaylistList());
}
```

- [ ] **Step 3: Update `NavQueue_Click`**

Replace with:

```csharp
private void NavQueue_Click(object sender, RoutedEventArgs e)
{
    if (_viewMode == ViewMode.Queue) return;
    _viewMode = ViewMode.Queue;
    _currentPlaylist = null;
    UpdateNavigation();
    UpdateSpectrumTimer();
    AnimateViewTransition(() => BuildQueueView());
}
```

- [ ] **Step 4: Update `NavVisualizer_Click`**

Replace with:

```csharp
private void NavVisualizer_Click(object sender, RoutedEventArgs e)
{
    if (_viewMode == ViewMode.Visualizer) return;
    _viewMode = ViewMode.Visualizer;
    _currentPlaylist = null;
    UpdateNavigation();
    UpdateSpectrumTimer();
    AnimateViewTransition(() => { /* visualizer draws via timer */ });
}
```

- [ ] **Step 5: Build and verify**

Run: `dotnet build Audiomatic.sln -c Debug`
Expected: Build succeeded

- [ ] **Step 6: Commit**

```bash
git add Audiomatic/MainWindow.xaml.cs
git commit -m "feat: Wire slide+fade transitions into all navigation handlers"
```

---

## Chunk 2: Mini-Player Ultra-Compact Mode

### Task 3: Add mini-player XAML layout

**Files:**
- Modify: `Audiomatic/MainWindow.xaml:60-95` (NowPlayingCard area, Row 1)

- [ ] **Step 1: Add mini-player overlay Grid in Row 1**

In `MainWindow.xaml`, after the existing `NowPlayingCard` Grid (after line 95, before the `<!-- Row 2: Timeline -->` comment), add:

```xml
<!-- Row 1b: Mini-player layout (visible only in mini mode) -->
<Grid x:Name="MiniPlayerBar" Grid.Row="1" Padding="10,6,10,6"
      Background="Transparent" Visibility="Collapsed"
      PointerPressed="DragArea_PointerPressed"
      PointerMoved="DragArea_PointerMoved"
      PointerReleased="DragArea_PointerReleased">
    <Grid.ColumnDefinitions>
        <ColumnDefinition Width="Auto"/>
        <ColumnDefinition Width="*"/>
        <ColumnDefinition Width="Auto"/>
        <ColumnDefinition Width="Auto"/>
    </Grid.ColumnDefinitions>

    <!-- Mini album art -->
    <Grid Grid.Column="0" Width="40" Height="40" CornerRadius="4"
          Background="{ThemeResource CardBackgroundFillColorSecondaryBrush}">
        <Image x:Name="MiniAlbumArt" Stretch="UniformToFill"
               Width="40" Height="40"/>
        <FontIcon x:Name="MiniAlbumArtPlaceholder" Glyph="&#xE8D6;" FontSize="18"
                  HorizontalAlignment="Center" VerticalAlignment="Center"
                  Foreground="{ThemeResource TextFillColorTertiaryBrush}"/>
    </Grid>

    <!-- Title - Artist on one line -->
    <TextBlock x:Name="MiniTrackText" Grid.Column="1"
               Text="No track" FontSize="12"
               VerticalAlignment="Center" Margin="10,0,0,0"
               TextTrimming="CharacterEllipsis" MaxLines="1"/>

    <!-- Play/Pause button -->
    <Button x:Name="MiniPlayPauseButton" Grid.Column="2"
            Width="32" Height="32" CornerRadius="16"
            Padding="0" Margin="6,0"
            Background="{ThemeResource AccentFillColorDefaultBrush}"
            Foreground="White"
            HorizontalContentAlignment="Center"
            VerticalContentAlignment="Center"
            Click="PlayPause_Click">
        <FontIcon x:Name="MiniPlayPauseIcon" Glyph="&#xE768;" FontSize="14"
                  Foreground="White"/>
    </Button>

    <!-- Close button -->
    <Button Grid.Column="3" Background="Transparent" BorderThickness="0"
            Padding="4,4" Click="Close_Click">
        <FontIcon Glyph="&#xE711;" FontSize="10"/>
    </Button>
</Grid>
```

- [ ] **Step 2: Build to verify XAML compiles**

Run: `dotnet build Audiomatic.sln -c Debug`
Expected: Build succeeded

- [ ] **Step 3: Commit**

```bash
git add Audiomatic/MainWindow.xaml
git commit -m "feat: Add mini-player XAML layout in Row 1"
```

---

### Task 4: Implement 3-state collapse cycling

**Files:**
- Modify: `Audiomatic/MainWindow.xaml.cs:43-44` (height constants)
- Modify: `Audiomatic/MainWindow.xaml.cs:1741-1774` (ToggleCollapse)
- Modify: `Audiomatic/MainWindow.xaml.cs:1776-1812` (AnimTick)
- Modify: `Audiomatic/MainWindow.xaml.cs:515-529` (UpdateNowPlaying — sync mini-player)

- [ ] **Step 1: Add mini height constant and collapse state enum**

Replace the height fields (line ~43-44):

```csharp
// Old:
private readonly int _expandedHeight = 710;
private readonly int _collapsedHeight = 220;
```

With:

```csharp
private readonly int _expandedHeight = 710;
private readonly int _collapsedHeight = 220;
private readonly int _miniHeight = 60;
private enum CollapseState { Expanded, Compact, Mini }
private CollapseState _collapseState = CollapseState.Expanded;
```

Remove the old `_isCollapsed` field (line ~42):

```csharp
// Remove: private bool _isCollapsed;
```

- [ ] **Step 2: Add mini-player sync in `UpdateNowPlaying`**

At the end of `UpdateNowPlaying` (before the closing brace, after line ~528), add:

```csharp
// Sync mini-player display
UpdateMiniPlayer(track);
```

Add the helper method nearby:

```csharp
private void UpdateMiniPlayer(TrackInfo? track)
{
    if (track == null)
    {
        MiniTrackText.Text = "No track";
        MiniAlbumArt.Source = null;
        MiniAlbumArt.Visibility = Visibility.Collapsed;
        MiniAlbumArtPlaceholder.Visibility = Visibility.Visible;
        return;
    }

    var display = string.IsNullOrEmpty(track.Artist)
        ? track.Title
        : $"{track.Title} — {track.Artist}";
    MiniTrackText.Text = display;

    // Share album art from main display
    MiniAlbumArt.Source = AlbumArtImage.Source;
    var hasArt = AlbumArtImage.Source != null;
    MiniAlbumArt.Visibility = hasArt ? Visibility.Visible : Visibility.Collapsed;
    MiniAlbumArtPlaceholder.Visibility = hasArt ? Visibility.Collapsed : Visibility.Visible;
}
```

- [ ] **Step 3: Rewrite `ToggleCollapse` for 3-state cycling**

Replace `ToggleCollapse` (lines 1741-1774) with:

```csharp
private void ToggleCollapse()
{
    // Cycle: Expanded → Compact → Mini → Expanded
    _collapseState = _collapseState switch
    {
        CollapseState.Expanded => CollapseState.Compact,
        CollapseState.Compact => CollapseState.Mini,
        CollapseState.Mini => CollapseState.Expanded,
        _ => CollapseState.Expanded
    };

    _targetHeight = _collapseState switch
    {
        CollapseState.Mini => _miniHeight,
        CollapseState.Compact => _collapsedHeight,
        _ => _expandedHeight
    };

    _currentAnimHeight = AppWindow.Size.Height;

    // Keep bottom edge fixed
    _animStartY = AppWindow.Position.Y;
    var bottomEdge = _animStartY + AppWindow.Size.Height;
    _targetY = bottomEdge - _targetHeight;

    // Update icon and tooltip
    CollapseIcon.Glyph = _collapseState switch
    {
        CollapseState.Expanded => "\uE73F",  // chevron down → compact
        CollapseState.Compact => "\uE73F",   // chevron down → mini
        CollapseState.Mini => "\uE740",      // chevron up → expand
        _ => "\uE73F"
    };
    ToolTipService.SetToolTip(CollapseButton, _collapseState switch
    {
        CollapseState.Expanded => "Compact (Ctrl+L)",
        CollapseState.Compact => "Mini (Ctrl+L)",
        CollapseState.Mini => "Expand (Ctrl+L)",
        _ => "Compact (Ctrl+L)"
    });

    // Show elements before expanding animation
    if (_collapseState == CollapseState.Expanded)
    {
        NowPlayingCard.Visibility = Visibility.Visible;
        MiniPlayerBar.Visibility = Visibility.Collapsed;
        VolumeRow.Visibility = Visibility.Visible;
        NavRow.Visibility = Visibility.Visible;
        SearchSortRow.Visibility = (_viewMode == ViewMode.Library || _viewMode == ViewMode.PlaylistDetail)
            ? Visibility.Visible : Visibility.Collapsed;
        TrackListView.Visibility = _viewMode != ViewMode.Visualizer
            ? Visibility.Visible : Visibility.Collapsed;
        WaveformContainer.Visibility = _viewMode == ViewMode.Visualizer
            ? Visibility.Visible : Visibility.Collapsed;
        BottomBar.Visibility = Visibility.Visible;
        CustomTitleBar.Visibility = Visibility.Visible;
    }
    else if (_collapseState == CollapseState.Compact)
    {
        NowPlayingCard.Visibility = Visibility.Visible;
        MiniPlayerBar.Visibility = Visibility.Collapsed;
        CustomTitleBar.Visibility = Visibility.Visible;
    }
    else if (_collapseState == CollapseState.Mini)
    {
        // Sync mini-player before showing
        UpdateMiniPlayer(_queue.CurrentTrack);
        MiniPlayPauseIcon.Glyph = _player.IsPlaying ? "\uE769" : "\uE768";
    }

    _animTimer?.Stop();
    _animTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(8) };
    _animTimer.Tick += AnimTick;
    _animTimer.Start();
}
```

- [ ] **Step 4: Update `AnimTick` for 3-state visibility**

Replace `AnimTick` (lines 1776-1812) with:

```csharp
private void AnimTick(object? sender, object e)
{
    var diff = _targetHeight - _currentAnimHeight;

    if (Math.Abs(diff) <= 4)
    {
        _currentAnimHeight = _targetHeight;
        _animTimer?.Stop();
        _animTimer = null;

        // Set final visibility based on collapse state
        if (_collapseState == CollapseState.Compact)
        {
            VolumeRow.Visibility = Visibility.Collapsed;
            NavRow.Visibility = Visibility.Collapsed;
            SearchSortRow.Visibility = Visibility.Collapsed;
            TrackListView.Visibility = Visibility.Collapsed;
            WaveformContainer.Visibility = Visibility.Collapsed;
            BottomBar.Visibility = Visibility.Collapsed;
            MiniPlayerBar.Visibility = Visibility.Collapsed;
            NowPlayingCard.Visibility = Visibility.Visible;
        }
        else if (_collapseState == CollapseState.Mini)
        {
            CustomTitleBar.Visibility = Visibility.Collapsed;
            NowPlayingCard.Visibility = Visibility.Collapsed;
            VolumeRow.Visibility = Visibility.Collapsed;
            NavRow.Visibility = Visibility.Collapsed;
            SearchSortRow.Visibility = Visibility.Collapsed;
            TrackListView.Visibility = Visibility.Collapsed;
            WaveformContainer.Visibility = Visibility.Collapsed;
            BottomBar.Visibility = Visibility.Collapsed;
            MiniPlayerBar.Visibility = Visibility.Visible;
        }

        AppWindow.MoveAndResize(new Windows.Graphics.RectInt32(
            AppWindow.Position.X, _targetY,
            AppWindow.Size.Width, _currentAnimHeight));
    }
    else
    {
        _currentAnimHeight += (int)(diff * 0.18);
        var newY = _targetY + (_targetHeight - _currentAnimHeight);
        AppWindow.MoveAndResize(new Windows.Graphics.RectInt32(
            AppWindow.Position.X, newY,
            AppWindow.Size.Width, _currentAnimHeight));
    }
}
```

- [ ] **Step 5: Update all `_isCollapsed` references**

Replace all remaining `_isCollapsed` references throughout the file:

- In the constructor `Closed` handler and anywhere else: replace `_isCollapsed` with `_collapseState != CollapseState.Expanded`
- In `ShowSettingsFlyout` (line ~1657): update the compact mode button label:

```csharp
panel.Children.Add(ActionPanel.CreateButton("\uE73F",
    _collapseState == CollapseState.Expanded ? "Compact Mode" :
    _collapseState == CollapseState.Compact ? "Mini Player" : "Expand",
    ["Ctrl", "L"], () =>
{
    flyout.Hide();
    ToggleCollapse();
}));
```

- In `WndProc` hotkey handler (line ~1932): no change needed, `ToggleCollapse()` still works.

- [ ] **Step 6: Sync play/pause icon in mini-player**

In `PlayPause_Click` (line ~623), after `PlayPauseIcon.Glyph = ...`:

```csharp
MiniPlayPauseIcon.Glyph = PlayPauseIcon.Glyph;
```

Also in `UpdateNowPlaying` and `OnMediaEnded` where `PlayPauseIcon.Glyph` is set, add the same sync line.

- [ ] **Step 7: Build and verify**

Run: `dotnet build Audiomatic.sln -c Debug`
Expected: Build succeeded

- [ ] **Step 8: Commit**

```bash
git add Audiomatic/MainWindow.xaml Audiomatic/MainWindow.xaml.cs
git commit -m "feat: Add mini-player ultra-compact mode with 3-state collapse cycling"
```

---

## Chunk 3: Inline Metadata Editor

### Task 5: Create `MetadataWriter` service

**Files:**
- Create: `Audiomatic/Services/MetadataWriter.cs`

- [ ] **Step 1: Create the MetadataWriter service**

Create `Audiomatic/Services/MetadataWriter.cs`:

```csharp
namespace Audiomatic.Services;

public static class MetadataWriter
{
    public record WriteResult(bool Success, string? Error = null);

    public static WriteResult WriteTags(string filePath, string title, string artist, string album)
    {
        try
        {
            using var tagFile = TagLib.File.Create(filePath);
            tagFile.Tag.Title = title;
            tagFile.Tag.Performers = string.IsNullOrWhiteSpace(artist)
                ? [] : [artist];
            tagFile.Tag.Album = album;
            tagFile.Save();
            return new WriteResult(true);
        }
        catch (IOException)
        {
            return new WriteResult(false, "File is in use. Stop playback and try again.");
        }
        catch (Exception ex)
        {
            return new WriteResult(false, ex.Message);
        }
    }

    public static WriteResult WriteArtwork(string filePath, byte[]? imageData, string mimeType = "image/jpeg")
    {
        try
        {
            using var tagFile = TagLib.File.Create(filePath);
            if (imageData == null)
            {
                tagFile.Tag.Pictures = [];
            }
            else
            {
                var picture = new TagLib.Picture(new TagLib.ByteVector(imageData))
                {
                    Type = TagLib.PictureType.FrontCover,
                    MimeType = mimeType
                };
                tagFile.Tag.Pictures = [picture];
            }
            tagFile.Save();
            return new WriteResult(true);
        }
        catch (IOException)
        {
            return new WriteResult(false, "File is in use. Stop playback and try again.");
        }
        catch (Exception ex)
        {
            return new WriteResult(false, ex.Message);
        }
    }
}
```

- [ ] **Step 2: Build to verify**

Run: `dotnet build Audiomatic.sln -c Debug`
Expected: Build succeeded

- [ ] **Step 3: Commit**

```bash
git add Audiomatic/Services/MetadataWriter.cs
git commit -m "feat: Add MetadataWriter service for TagLibSharp write-back"
```

---

### Task 6: Add `UpdateTrackMetadata` to LibraryManager

**Files:**
- Modify: `Audiomatic/Services/LibraryManager.cs` (after `GetFavorites`, around line ~391)

- [ ] **Step 1: Add `UpdateTrackMetadata` method**

Add after the Favorites section in `LibraryManager.cs`:

```csharp
// ── Metadata editing ─────────────────────────────────────

public static void UpdateTrackMetadata(long trackId, string title, string artist, string album)
{
    using var conn = new SqliteConnection(ConnectionString);
    conn.Open();
    EnablePragmas(conn);
    using var cmd = conn.CreateCommand();
    cmd.CommandText = "UPDATE tracks SET title = @title, artist = @artist, album = @album WHERE id = @id;";
    cmd.Parameters.AddWithValue("@title", title);
    cmd.Parameters.AddWithValue("@artist", artist);
    cmd.Parameters.AddWithValue("@album", album);
    cmd.Parameters.AddWithValue("@id", trackId);
    cmd.ExecuteNonQuery();
}
```

- [ ] **Step 2: Build to verify**

Run: `dotnet build Audiomatic.sln -c Debug`
Expected: Build succeeded

- [ ] **Step 3: Commit**

```bash
git add Audiomatic/Services/LibraryManager.cs
git commit -m "feat: Add UpdateTrackMetadata to LibraryManager"
```

---

### Task 7: Build inline metadata editor UI in context menu

**Files:**
- Modify: `Audiomatic/MainWindow.xaml.cs:968-1035` (BuildTrackContextContent — add Edit Tags button)
- Modify: `Audiomatic/MainWindow.xaml.cs` (add BuildMetadataEditorContent method)

- [ ] **Step 1: Add "Edit Tags" button in `BuildTrackContextContent`**

In `BuildTrackContextContent`, just before the "Remove from Playlist" section (line ~1021), add:

```csharp
// Edit tags
panel.Children.Add(ActionPanel.CreateSeparator());
panel.Children.Add(ActionPanel.CreateButton("\uE70F", "Edit Tags", [], () =>
{
    flyout.Content = BuildMetadataEditorContent(flyout, capturedTrack);
}));
```

- [ ] **Step 2: Add `BuildMetadataEditorContent` method**

Add after `BuildQueueItemContextContent` (after line ~1137):

```csharp
private StackPanel BuildMetadataEditorContent(Flyout flyout, TrackInfo track)
{
    var panel = new StackPanel { Spacing = 6, Padding = new Thickness(4) };

    // Back button + header
    var header = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
    var backBtn = new Button
    {
        Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
        BorderThickness = new Thickness(0),
        Padding = new Thickness(4, 2, 4, 2),
        MinHeight = 0, MinWidth = 0,
        Content = new FontIcon { Glyph = "\uE72B", FontSize = 12 }
    };
    backBtn.Click += (_, _) =>
    {
        flyout.Content = BuildTrackContextContent(flyout, track);
    };
    header.Children.Add(backBtn);
    header.Children.Add(new TextBlock
    {
        Text = "Edit Tags",
        FontSize = 13,
        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        VerticalAlignment = VerticalAlignment.Center
    });
    panel.Children.Add(header);
    panel.Children.Add(ActionPanel.CreateSeparator());

    // Artwork preview + buttons
    var artworkGrid = new Grid { Margin = new Thickness(0, 2, 0, 2) };
    artworkGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
    artworkGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

    var artPreview = new Image
    {
        Width = 64, Height = 64,
        Stretch = Microsoft.UI.Xaml.Media.Stretch.UniformToFill
    };
    var artPlaceholder = new FontIcon
    {
        Glyph = "\uE8D6", FontSize = 24, Width = 64, Height = 64,
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
        Foreground = ThemeHelper.Brush("TextFillColorTertiaryBrush")
    };

    // Load current artwork
    byte[]? pendingArtwork = null;
    bool removeArtwork = false;

    try
    {
        using var tagFile = TagLib.File.Create(track.Path);
        if (tagFile.Tag.Pictures.Length > 0)
        {
            var pic = tagFile.Tag.Pictures[0];
            var stream = new Windows.Storage.Streams.InMemoryRandomAccessStream();
            var writer = new Windows.Storage.Streams.DataWriter(stream.GetOutputStreamAt(0));
            writer.WriteBytes(pic.Data.Data);
            _ = writer.StoreAsync().AsTask().Result;
            stream.Seek(0);
            var bitmap = new BitmapImage();
            bitmap.SetSource(stream);
            artPreview.Source = bitmap;
            artPlaceholder.Visibility = Visibility.Collapsed;
        }
        else
        {
            artPreview.Visibility = Visibility.Collapsed;
        }
    }
    catch
    {
        artPreview.Visibility = Visibility.Collapsed;
    }

    var artContainer = new Grid
    {
        Width = 64, Height = 64, CornerRadius = new CornerRadius(4),
        Background = ThemeHelper.Brush("CardBackgroundFillColorSecondaryBrush")
    };
    artContainer.Children.Add(artPreview);
    artContainer.Children.Add(artPlaceholder);
    Grid.SetColumn(artContainer, 0);

    var artButtons = new StackPanel
    {
        VerticalAlignment = VerticalAlignment.Center,
        Margin = new Thickness(10, 0, 0, 0),
        Spacing = 4
    };

    var changeArtBtn = new Button
    {
        Content = "Change",
        FontSize = 11,
        Padding = new Thickness(8, 3, 8, 3),
        MinHeight = 0
    };
    changeArtBtn.Click += async (_, _) =>
    {
        var picker = new Windows.Storage.Pickers.FileOpenPicker();
        picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.PicturesLibrary;
        picker.FileTypeFilter.Add(".jpg");
        picker.FileTypeFilter.Add(".jpeg");
        picker.FileTypeFilter.Add(".png");

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

        var file = await picker.PickSingleFileAsync();
        if (file == null) return;

        pendingArtwork = await System.IO.File.ReadAllBytesAsync(file.Path);
        removeArtwork = false;

        // Update preview
        var stream = new Windows.Storage.Streams.InMemoryRandomAccessStream();
        var writer = new Windows.Storage.Streams.DataWriter(stream.GetOutputStreamAt(0));
        writer.WriteBytes(pendingArtwork);
        await writer.StoreAsync();
        stream.Seek(0);
        var bitmap = new BitmapImage();
        bitmap.SetSource(stream);
        artPreview.Source = bitmap;
        artPreview.Visibility = Visibility.Visible;
        artPlaceholder.Visibility = Visibility.Collapsed;
    };
    artButtons.Children.Add(changeArtBtn);

    var removeArtBtn = new Button
    {
        Content = "Remove",
        FontSize = 11,
        Padding = new Thickness(8, 3, 8, 3),
        MinHeight = 0
    };
    removeArtBtn.Click += (_, _) =>
    {
        removeArtwork = true;
        pendingArtwork = null;
        artPreview.Source = null;
        artPreview.Visibility = Visibility.Collapsed;
        artPlaceholder.Visibility = Visibility.Visible;
    };
    artButtons.Children.Add(removeArtBtn);

    Grid.SetColumn(artButtons, 1);
    artworkGrid.Children.Add(artContainer);
    artworkGrid.Children.Add(artButtons);
    panel.Children.Add(artworkGrid);

    // Text fields
    var titleBox = new TextBox
    {
        Header = "Title",
        Text = track.Title,
        FontSize = 12,
        Padding = new Thickness(8, 5, 8, 5)
    };
    panel.Children.Add(titleBox);

    var artistBox = new TextBox
    {
        Header = "Artist",
        Text = track.Artist,
        FontSize = 12,
        Padding = new Thickness(8, 5, 8, 5)
    };
    panel.Children.Add(artistBox);

    var albumBox = new TextBox
    {
        Header = "Album",
        Text = track.Album,
        FontSize = 12,
        Padding = new Thickness(8, 5, 8, 5)
    };
    panel.Children.Add(albumBox);

    // Error message area
    var errorText = new TextBlock
    {
        FontSize = 11,
        Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 99, 99)),
        TextWrapping = TextWrapping.Wrap,
        Visibility = Visibility.Collapsed
    };
    panel.Children.Add(errorText);

    panel.Children.Add(ActionPanel.CreateSeparator());

    // Save / Cancel buttons
    var buttonRow = new Grid();
    buttonRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
    buttonRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

    var saveBtn = new Button
    {
        Content = "Save",
        FontSize = 12,
        Padding = new Thickness(16, 5, 16, 5),
        Style = (Style)Application.Current.Resources["AccentButtonStyle"]
    };
    saveBtn.Click += (_, _) =>
    {
        var newTitle = titleBox.Text.Trim();
        var newArtist = artistBox.Text.Trim();
        var newAlbum = albumBox.Text.Trim();

        if (string.IsNullOrEmpty(newTitle))
        {
            newTitle = System.IO.Path.GetFileNameWithoutExtension(track.Path);
        }

        // Write tags to file
        var result = MetadataWriter.WriteTags(track.Path, newTitle, newArtist, newAlbum);
        if (!result.Success)
        {
            errorText.Text = result.Error ?? "Unknown error";
            errorText.Visibility = Visibility.Visible;
            return;
        }

        // Write artwork if changed
        if (pendingArtwork != null || removeArtwork)
        {
            var artResult = MetadataWriter.WriteArtwork(track.Path,
                removeArtwork ? null : pendingArtwork);
            if (!artResult.Success)
            {
                errorText.Text = artResult.Error ?? "Unknown error";
                errorText.Visibility = Visibility.Visible;
                return;
            }
        }

        // Update database
        LibraryManager.UpdateTrackMetadata(track.Id, newTitle, newArtist, newAlbum);

        // Update in-memory track
        track.Title = newTitle;
        track.Artist = newArtist;
        track.Album = newAlbum;

        // Refresh UI
        LoadTracks();

        // If this is the currently playing track, update now-playing display
        if (_queue.CurrentTrack?.Id == track.Id)
        {
            _queue.CurrentTrack.Title = newTitle;
            _queue.CurrentTrack.Artist = newArtist;
            _queue.CurrentTrack.Album = newAlbum;
            TrackTitle.Text = newTitle;
            TrackArtist.Text = newArtist;
            TrackAlbum.Text = newAlbum;
            UpdateMiniPlayer(_queue.CurrentTrack);

            if (pendingArtwork != null || removeArtwork)
                LoadAlbumArt(track.Path);
        }

        flyout.Hide();
    };
    Grid.SetColumn(saveBtn, 1);

    var cancelBtn = new Button
    {
        Content = "Cancel",
        FontSize = 12,
        Padding = new Thickness(12, 5, 12, 5)
    };
    cancelBtn.Click += (_, _) => flyout.Hide();
    Grid.SetColumn(cancelBtn, 0);

    buttonRow.Children.Add(cancelBtn);
    buttonRow.Children.Add(saveBtn);
    panel.Children.Add(buttonRow);

    return panel;
}
```

- [ ] **Step 3: Add MetadataWriter using**

At the top of `MainWindow.xaml.cs`, the `using Audiomatic.Services;` import already exists, so `MetadataWriter` is accessible.

- [ ] **Step 4: Build and verify**

Run: `dotnet build Audiomatic.sln -c Debug`
Expected: Build succeeded

- [ ] **Step 5: Commit**

```bash
git add Audiomatic/MainWindow.xaml.cs
git commit -m "feat: Add inline metadata editor in track context menu"
```

---

## Chunk 4: Final Integration & Cleanup

### Task 8: Final verification and commit

**Files:**
- All modified files

- [ ] **Step 1: Full rebuild**

Run: `dotnet build Audiomatic.sln -c Release`
Expected: Build succeeded with 0 errors

- [ ] **Step 2: Verify no regressions in key areas**

Manually verify:
- Navigation between Library → Playlists → Queue → Visualizer shows slide+fade animation
- `Ctrl+L` cycles: expanded → compact → mini → expanded
- Mini-player shows cover, title — artist, play/pause button
- Right-click a track → "Edit Tags" opens inline editor
- Editing title/artist/album and clicking Save updates the track
- Change/Remove artwork works via file picker

- [ ] **Step 3: Final commit**

```bash
git add -A
git commit -m "feat: Add view transitions, mini-player mode, and metadata editor"
```
