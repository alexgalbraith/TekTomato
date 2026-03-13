# TekTomato — Technical Architecture Document

**Version:** 1.0
**Date:** 13 March 2026
**Status:** Approved for Development

---

## 1. Tech Stack

### Runtime & Language

| Component | Choice | Justification |
|-----------|--------|---------------|
| Language | C# 12 | WPF's native language. Strong typing, mature ecosystem, excellent tooling. |
| Framework | .NET 8 (LTS) | Long-term support. Ships with Windows 10/11 via framework-dependent deployment. Proven stable. .NET 9 is not LTS — unnecessary risk for a desktop app. |
| UI Framework | WPF | PRD requirement. Native Windows, mature, excellent support for borderless windows, transparency, system tray, and custom styling via ResourceDictionary. No Electron bloat. |

### NuGet Dependencies

| Package | Version | Purpose | Justification |
|---------|---------|---------|---------------|
| `Microsoft.EntityFrameworkCore.Sqlite` | 8.x | SQLite database access | Typed access, migrations, query building. No hand-written SQL. EF Core is the standard .NET ORM. |
| `Microsoft.EntityFrameworkCore.Design` | 8.x | EF Core migration tooling (dev only) | Required for `dotnet ef migrations add` during development. |
| `CommunityToolkit.Mvvm` | 8.x | MVVM infrastructure | Eliminates INotifyPropertyChanged/ICommand boilerplate. Microsoft-maintained, source-generator-based, no runtime reflection. |
| `H.NotifyIcon.Wpf` | 2.x | System tray icon and context menu | Pure WPF, XAML-defined context menus, no WinForms reference required. |
| `LiveChartsCore.SkiaSharpView.WPF` | 2.x | Line chart rendering | Actively maintained, SkiaSharp-rendered, straightforward WPF data-binding API. |
| `SkiaSharp.Views.WPF` | 2.x | Custom heatmap rendering | Provides SKElement for custom 2D drawing in WPF. Used for the calendar heatmap. |
| `Microsoft.Extensions.DependencyInjection` | 8.x | IoC container | Constructor injection throughout. Lightweight, standard, avoids service-locator anti-pattern. |

**Packages explicitly rejected:** Dapper (EF Core sufficient), OxyPlot (inconsistent maintenance), Prism/Caliburn.Micro (heavyweight), Hardcodet.NotifyIcon.Wpf (predecessor, less maintained).

---

## 2. Component Breakdown

### High-Level Architecture

```
┌─────────────────────────────────────────────────────┐
│                    App.xaml.cs                       │
│              (Startup, DI Container)                │
└──────────┬──────────────────────────┬───────────────┘
           │                          │
    ┌──────▼──────┐           ┌───────▼────────┐
    │  TrayManager │           │  ThemeService  │
    │(H.NotifyIcon)│           │ (ResourceDict) │
    └──────┬──────┘           └───────┬────────┘
           │                          │
┌──────────▼──────────────────────────▼───────────────┐
│                    Views / Windows                   │
│  ┌──────────────┐ ┌──────────────┐ ┌──────────────┐ │
│  │OverlayWindow │ │SettingsView  │ │  StatsView   │ │
│  └──────┬───────┘ └──────┬───────┘ └──────┬───────┘ │
│  ┌──────▼───────┐ ┌──────▼───────┐ ┌──────▼───────┐ │
│  │OverlayVM     │ │SettingsVM    │ │ StatsVM      │ │
│  └──────┬───────┘ └──────┬───────┘ └──────┬───────┘ │
└─────────┼────────────────┼────────────────┼─────────┘
          │                │                │
┌─────────▼────────────────▼────────────────▼─────────┐
│                    Services Layer                    │
│  ┌─────────────┐ ┌─────────────┐ ┌────────────────┐ │
│  │ TimerEngine  │ │SoundService │ │ SessionService │ │
│  └─────────────┘ └─────────────┘ └────────┬───────┘ │
│                                           │         │
│                                  ┌────────▼───────┐ │
│                                  │   DataStore    │ │
│                                  │(EF Core/SQLite)│ │
│                                  └────────────────┘ │
└─────────────────────────────────────────────────────┘
```

### Components

**`App.xaml.cs`** — Configures DI container, runs EF Core migrations on startup, creates `OverlayWindow`, initialises `TrayManager`, applies initial theme.

**`TimerEngine`** — Core countdown logic. Manages state machine (Idle, Running, Paused, Overrun, Completed). No UI dependency. Exposes observable state. Detailed in Section 5.

**`OverlayWindow` / `OverlayViewModel`** — Primary user-facing window. Borderless, always-on-top, draggable, adjustable background opacity. Displays countdown, Start/Pause button, session type, completion action buttons. Binds to `TimerEngine` via ViewModel.

**`SettingsView` / `SettingsViewModel`** — Configuration panel (UserControl hosted in overlay or secondary window). Exposes all configurable settings. Saves via `SettingsService` on apply/close.

**`StatsView` / `StatsViewModel`** — Statistics panel. Calendar heatmap (SkiaSharp) and line chart (LiveCharts2). Queries `SessionService` for aggregated data.

**`TrayManager`** — Wraps H.NotifyIcon `TaskbarIcon`. Right-click menu: Show, Start, Pause, Exit. Intercepts window close to minimise-to-tray. Updates tooltip with timer state.

**`SoundService`** — Enumerates `C:\Windows\Media\` .wav files. Plays selected sound. Detailed in Section 7.

**`SessionService`** — Records completed sessions. Provides aggregation queries for stats (daily counts, focus minutes by period).

**`DataStore` / `TekTomatoDbContext`** — EF Core DbContext with SQLite. Database at `%AppData%\TekTomato\tektomato.db`. Schema in Section 3.

**`ThemeService`** — Manages Dark / Light / System Auto themes by swapping ResourceDictionary entries at runtime. Listens for Windows theme changes.

**`SettingsService`** — Reads/writes settings to the Settings table. Strongly typed. In-memory cache; writes to DB on change.

---

## 3. Data Model

### Storage: SQLite

Chosen over JSON because: append-efficient for growing session history, supports indexed aggregation queries, ACID-compliant (no corruption on crash), scales from tens to thousands of records.

**Database file:** `%AppData%\TekTomato\tektomato.db`

### Table: `Sessions`

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| `Id` | INTEGER | PRIMARY KEY, AUTOINCREMENT | Unique session identifier |
| `SessionType` | TEXT | NOT NULL | `"Work"`, `"ShortBreak"`, or `"LongBreak"` |
| `StartedAtUtc` | TEXT (ISO 8601) | NOT NULL | When the timer was started |
| `CompletedAtUtc` | TEXT (ISO 8601) | NOT NULL | When the user acknowledged completion |
| `PlannedDurationSeconds` | INTEGER | NOT NULL | Configured duration for this session type |
| `ActualDurationSeconds` | INTEGER | NOT NULL | Elapsed active time (excludes paused time) |
| `OverrunSeconds` | INTEGER | NOT NULL, DEFAULT 0 | Active seconds past 0:00 before user acted |
| `PausedDurationSeconds` | INTEGER | NOT NULL, DEFAULT 0 | Total time spent paused |
| `CompletedNormally` | INTEGER (bool) | NOT NULL, DEFAULT 1 | 1 = reached 0:00; 0 = abandoned |
| `PomodoroNumber` | INTEGER | NULLABLE | Position in current cycle (1–N). NULL for breaks. |

**Indices:** `IX_Sessions_StartedAtUtc` (date-range queries), `IX_Sessions_SessionType_CompletedNormally` (counting completed Pomodoros).

### Table: `Settings`

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| `Key` | TEXT | PRIMARY KEY | Setting name |
| `Value` | TEXT | NOT NULL | Value as string (parsed by SettingsService) |

**Settings keys and defaults:**

| Key | Default |
|-----|---------|
| `WorkDurationMinutes` | `25` |
| `ShortBreakDurationMinutes` | `5` |
| `LongBreakDurationMinutes` | `15` |
| `LongBreakIntervalPomodoros` | `4` |
| `SoundFileName` | `Windows Notify System Generic.wav` |
| `Theme` | `SystemAuto` |
| `BackgroundOpacityPercent` | `80` |
| `OverlayPositionX` | `null` |
| `OverlayPositionY` | `null` |

---

## 4. UI Architecture

### Pattern: MVVM

MVVM is used throughout. Views contain XAML only — code-behind is limited to window-level concerns (drag, close, minimise) and pure rendering (SkiaSharp PaintSurface). ViewModels use CommunityToolkit.Mvvm source generators (`[ObservableProperty]`, `[RelayCommand]`). Services contain all business logic.

### Overlay Window

The `OverlayWindow` is configured as follows in XAML:

- `WindowStyle="None"` — removes title bar and border
- `AllowsTransparency="True"` — enables per-pixel transparency
- `Topmost="True"` — always-on-top
- `Background="Transparent"` — window itself is fully transparent
- `ResizeMode="NoResize"` — fixed size

**Dragging:** Implemented in code-behind by handling `MouseLeftButtonDown` on the background Grid and calling `DragMove()`.

### Opacity Implementation (Critical)

**Do NOT use `Window.Opacity`** — this makes the entire window including text and buttons transparent.

Instead, the window contains two Grid layers stacked via a root Grid:

1. **Background Grid** (`Name="BackgroundPanel"`) — contains only the background brush. Its `Opacity` property is bound to the user's configured opacity value (0.0–1.0). This panel is fully transparent at 0% opacity.
2. **Content Grid** — sits on top of the background panel (same Grid cell, ZIndex higher). Contains all UI elements (countdown text, buttons). This panel is never transparent — `Opacity` always = 1.0.

When the user adjusts the opacity slider, only `BackgroundPanel.Opacity` changes. Text and buttons remain fully visible at all times.

### Theme Implementation

Three ResourceDictionary files:
- `Resources/Themes/Common.xaml` — shared styles (button shapes, font sizes, control templates) that do not change between themes
- `Resources/Themes/Dark.xaml` — dark colour palette (background, foreground, accent colours)
- `Resources/Themes/Light.xaml` — light colour palette

`ThemeService.ApplyTheme(ThemeType theme)`:
1. Removes the current theme dictionary from `Application.Current.Resources.MergedDictionaries`
2. Inserts the new theme dictionary
3. `Common.xaml` remains loaded permanently

For **System Auto**: `ThemeService` reads `HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize\AppsUseLightTheme` on startup and applies the matching theme. It also subscribes to `SystemParameters.StaticPropertyChanged` to detect live theme changes and re-apply.

All colours in the UI are referenced via dynamic resource keys (e.g. `{DynamicResource PrimaryBackground}`) defined in the theme dictionaries, so theme changes take immediate visual effect.

### SettingsView and StatsView

These are `UserControl` types displayed in secondary windows (opened from the overlay via button or tray menu). Each is wrapped in a `Window` created on demand by the ViewModel, not pre-created at startup.

---

## 5. Timer Engine Design

### DispatcherTimer Configuration

`TimerEngine` uses a single `System.Windows.Threading.DispatcherTimer` with a tick interval of **250 milliseconds**. This is frequent enough for smooth visual updates (display never lags more than 250ms) while avoiding unnecessary CPU overhead. Running on the UI dispatcher thread means property updates from tick events can directly update bound ViewModel properties without cross-thread marshalling.

**Critical:** The engine does NOT count ticks. Tick counting drifts because `DispatcherTimer` is not a precision timer. Instead, the engine records wall-clock timestamps (`DateTime.UtcNow`) at key moments and calculates elapsed time by subtraction on every tick.

### Internal Fields

- `_sessionStartedAtUtc` — set when transitioning from Idle to Running
- `_plannedDurationSeconds` — target duration for this session
- `_pauseStartedAtUtc` — set on entering Paused state; cleared on resume
- `_totalPausedSeconds` — cumulative paused time. Each resume adds `(DateTime.UtcNow - _pauseStartedAtUtc).TotalSeconds`
- `_completedAtUtc` — set once when remaining time first reaches zero
- `_overrunPauseStartedAtUtc` — set when pausing during Overrun state
- `_totalOverrunPausedSeconds` — cumulative paused time that occurred after the timer crossed zero (tracked separately so OverrunSeconds reflects only active overrun time)

### State Machine

**Idle** — Initial state. No timer ticking. Display shows default duration for next session type.
- → **Running**: User clicks Start. Engine records `_sessionStartedAtUtc`, resets all accumulators, starts `DispatcherTimer`.

**Running** — Timer ticking. Each tick calculates:
- `elapsedActive = (DateTime.UtcNow - _sessionStartedAtUtc).TotalSeconds - _totalPausedSeconds`
- `remainingSeconds = _plannedDurationSeconds - elapsedActive`
- Display shows `remainingSeconds` as `MM:SS` (truncated, not rounded)
- → **Paused**: User clicks Pause. Engine records `_pauseStartedAtUtc`, stops `DispatcherTimer`.
- → **Overrun**: Automatic on any tick where `remainingSeconds <= 0`. Engine records `_completedAtUtc`, calls `SoundService.PlaySelectedSound()`, does NOT stop the timer. Action buttons appear.

**Paused** — Timer not ticking. Display frozen.
- → **Running**: User clicks Resume. Engine adds pause duration to `_totalPausedSeconds`, clears `_pauseStartedAtUtc`, restarts `DispatcherTimer`.
- → **Idle**: User clicks Cancel/Stop. Session discarded, not saved.

**Overrun** — Timer still ticking. `remainingSeconds` is now negative. Display shows `-M:SS` (e.g. `-0:32`). Format: take absolute value, format as `M:SS`, prepend minus sign.
- → **Paused**: User clicks Pause. Engine records `_overrunPauseStartedAtUtc`, stops timer. On resume, duration is added to both `_totalPausedSeconds` AND `_totalOverrunPausedSeconds`.
- → **Completed**: User clicks an action button (Start Pomodoro / Short Break / Long Break). Engine saves session and transitions to Idle.

**Completed** — Transient. Engine constructs session record:
- `ActualDurationSeconds` = total active time from start to now (excluding all paused time)
- `OverrunSeconds` = `(DateTime.UtcNow - _completedAtUtc).TotalSeconds - _totalOverrunPausedSeconds`
- `PausedDurationSeconds` = `_totalPausedSeconds`
- `CompletedNormally` = `true`

After saving via `SessionService`, immediately transitions to Idle and configures display for the next session type.

### Negative Countdown — Summary

There is no separate "overtime" mode. The same `elapsed - duration` arithmetic continues past zero and naturally produces negative values. The `DispatcherTimer` never stops at zero. At the zero crossing: (a) state → Overrun, (b) sound fires, (c) action buttons appear, (d) `_completedAtUtc` recorded. Everything else continues unchanged.

---

## 6. Stats Implementation

### Heatmap Data Aggregation

`StatsViewModel` queries via `SessionService`:
1. Filter: `SessionType == "Work"` AND `CompletedNormally == true`
2. Filter: `CompletedAtUtc >= startDate` (based on selected range)
3. Materialise with `.ToListAsync()`, then group in memory
4. Convert `CompletedAtUtc` to local date: `record.CompletedAtUtc.ToLocalTime().Date`
5. Group by local date, count per group
6. Return `Dictionary<DateOnly, int>`

(Grouping by `.Date` after timezone conversion cannot be translated to SQL, hence in-memory grouping.)

### Line Chart Data Aggregation

1. Same Work + CompletedNormally filter + date range
2. Group by local date (day mode) or ISO week (`ISOWeek.GetWeekOfYear()` — week mode)
3. Sum `ActualDurationSeconds` per group
4. Convert to minutes for display
5. Return `List<(DateTime date, double minutes)>`

ViewModel exposes a Day / Week toggle that re-aggregates and rebinds.

### Calendar Heatmap (SkiaSharp)

Rendered on an `SKElement` (from SkiaSharp.Views.WPF) via the `PaintSurface` event.

**Layout:** Grid of rounded rectangles, one per day. Columns = weeks (oldest left, newest right). Rows = days of week (Monday top, Sunday bottom). Cell size: 14px, gap: 3px. Month labels above grid, M/W/F day labels to the left.

**Colour scale (5 levels):**

| Count | Dark Theme | Light Theme |
|-------|-----------|-------------|
| 0 | `#161b22` | `#ebedf0` |
| 1 | `#0e4429` | `#9be9a8` |
| 2 | `#006d32` | `#40c463` |
| 3 | `#26a641` | `#30a14e` |
| 4+ | `#39d353` | `#216e39` |

**PaintSurface procedure:** Clear canvas → calculate dimensions → iterate days → look up count → select colour → draw filled rounded rectangle → draw labels.

**Invalidation:** Call `skElement.InvalidateVisual()` on data change, date range change, or theme change.

**Tooltip:** Handle `MouseMove` on `SKElement`. Hit-test which cell the mouse is over (coordinate-based). Show WPF `ToolTip` or `Popup` with "X sessions on DD MMM YYYY". Hide when not over a cell.

### Date Range Selector

ComboBox options: Last 3 months / Last 6 months / Last 12 months (default) / All time. Selecting re-queries and re-renders both charts.

### Line Chart (LiveCharts2)

Use `CartesianChart` with:
- X axis: `DateTimeAxis` with label formatter showing month/week label
- Y axis: `Axis` labelled in minutes
- Series: `LineSeries<DateTimePoint>` with smooth curve and point markers
- Bind `Series` and `XAxes` to ViewModel properties

---

## 7. Sound Implementation

`SoundService` is a singleton registered via DI. Depends on `SettingsService`.

**Enumeration.** `GetAvailableSounds()` calls `Directory.GetFiles(@"C:\Windows\Media", "*.wav")` and returns `IEnumerable<string>` of filenames only (via `Path.GetFileName()`). If the directory does not exist or contains zero `.wav` files, returns an empty collection.

**Settings dropdown.** `SettingsViewModel` calls `GetAvailableSounds()` on initialisation. Builds an `ObservableCollection<string>` sorted alphabetically. Inserts `"No sound"` at position zero regardless. If stored value no longer exists in the list, selection falls back to `"No sound"`.

**Persistence.** Stores filename only (e.g. `"Windows Notify System Generic.wav"`), never the full path. Full path reconstructed at playback via `Path.Combine(@"C:\Windows\Media", filename)`.

**Playback.** `SoundService.PlaySelectedSound()` is called by `TimerEngine` at the zero crossing:
1. Read current sound filename from `SettingsService` (not cached — live changes take effect immediately)
2. If `"No sound"` or null/empty: return without playing
3. Reconstruct full path
4. If file does not exist: fall back to `SystemSounds.Beep.Play()` and return
5. Create new `SoundPlayer` instance with full path
6. Call `SoundPlayer.LoadAsync()` — loads on background thread
7. On `LoadCompleted` success: call `SoundPlayer.Play()` (fire-and-forget). On failure: fall back to `SystemSounds.Beep.Play()`

A new `SoundPlayer` instance is created each time (lightweight; avoids threading issues with reuse).

---

## 8. File and Folder Structure

```
TekTomato.sln
TekTomato/
├── TekTomato.csproj
├── App.xaml
├── App.xaml.cs
├── Assets/
│   └── tomato.ico
├── Data/
│   ├── TekTomatoDbContext.cs
│   └── Migrations/
│       └── (EF Core migration files — auto-generated)
├── Models/
│   ├── PomodoroSession.cs
│   └── Setting.cs
├── Services/
│   ├── TimerEngine.cs
│   ├── SettingsService.cs
│   ├── SoundService.cs
│   ├── SessionService.cs
│   └── ThemeService.cs
├── ViewModels/
│   ├── OverlayViewModel.cs
│   ├── SettingsViewModel.cs
│   └── StatsViewModel.cs
├── Views/
│   ├── OverlayWindow.xaml
│   ├── OverlayWindow.xaml.cs
│   ├── SettingsView.xaml
│   ├── SettingsView.xaml.cs
│   ├── StatsView.xaml
│   └── StatsView.xaml.cs
└── Resources/
    └── Themes/
        ├── Common.xaml
        ├── Dark.xaml
        └── Light.xaml
```

**Notes:**
- No `MainWindow.xaml` — primary window is `Views/OverlayWindow.xaml`
- `SettingsView` and `StatsView` are `UserControl` types hosted in secondary windows created on demand
- No `appsettings.json` — all settings in SQLite via `SettingsService`
- `tomato.ico` used for both system tray icon and application window icon
- `Migrations/` is auto-generated by EF Core tooling and checked into source control

---

## 9. Key Technical Decisions

- **Layered Grid for background opacity, not Window.Opacity.** `Window.Opacity` makes the entire window — including text and buttons — transparent. The overlay uses a dedicated background Grid layer whose `Opacity` is adjustable, while the foreground content layer always remains at full opacity.

- **Theme switching by swapping ResourceDictionary entries at runtime.** `ThemeService` removes the current theme dictionary from `Application.Current.Resources.MergedDictionaries` and inserts the new one. `Common.xaml` remains permanently loaded. Instant theme changes without restarting the visual tree.

- **SQLite over flat JSON for persistence.** Append-efficient for growing session history, supports indexed aggregation queries, ACID-compliant against crash corruption, scales from tens to thousands of records.

- **LiveCharts2 over OxyPlot.** More actively maintained, SkiaSharp-rendered (consistent with heatmap stack), strong WPF data-binding support, polished output with less configuration.

- **H.NotifyIcon.Wpf over WinForms NotifyIcon.** Pure WPF, XAML-defined context menus, no `System.Windows.Forms` reference or interop quirks.

- **DispatcherTimer with wall-clock timestamps over tick counting.** Elapsed time is computed as `DateTime.UtcNow - startTimestamp` on every tick — never accumulated from ticks. Ensures displayed countdown is always accurate regardless of UI thread load.

- **Negative countdown via continued elapsed calculation past zero.** No separate overtime mode. The same arithmetic naturally produces negative values past zero. No additional state, no transition bugs. State changes to Overrun but the calculation is unchanged.

- **Data in `%AppData%\TekTomato` over alongside the .exe.** Avoids UAC permission issues under Program Files, survives the user moving the executable, follows Windows conventions.

- **Framework-dependent publish over self-contained.** Windows 10/11 ship with .NET runtime. Keeps the published output small (a few MB vs 60+ MB self-contained), benefits from shared runtime security patches.

- **EF Core migrations applied automatically on startup.** `DbContext.Database.Migrate()` runs at launch — creates the database on fresh install, applies new migrations on update. No manual steps required.

---

## 10. Risk Assessment

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| **WPF `AllowsTransparency` performance** — software rendering fallback on older/integrated GPUs can cause sluggish redraw and high CPU for transparent windows | Medium | Medium | Keep overlay small and visually simple. Offer a "disable transparency" setting that switches to `WindowStyle=SingleBorderWindow` with a solid background if performance issues arise. |
| **SkiaSharp heatmap complexity** — custom-rendering a year-view heatmap requires manual hit-testing, layout calculation, and tooltip handling without a pre-built component | High | Medium | Implement as a self-contained UserControl with clearly defined inputs. Start with a minimal version (coloured rectangles, no interactivity) and iterate. Fall back to a WPF UniformGrid of coloured cells if SkiaSharp proves too complex for v1. |
| **H.NotifyIcon.Wpf version compatibility** — library has had breaking API changes between major versions | Low | Medium | Pin to a specific 2.x minor version. Wrap all tray interaction behind `ITrayService` so the implementation can be swapped if needed. |
| **.NET 8 not installed on target machine** — framework-dependent deployment fails if runtime is absent | Medium | High | .NET produces a clear dialog directing users to install it. Include direct download link in README. Offer a self-contained build as an alternative download if distribution expands. |
| **DispatcherTimer display lag on busy UI thread** — ticks may arrive late, causing visual update to lag 1–2 seconds | Medium | Low | Tick interval of 250ms means display is never more than 250ms behind. The zero-crossing check runs on every tick so completion sound and state change are never missed — only the visual may lag briefly. |
| **EF Core migration failure on first run** — `%AppData%\TekTomato` may not exist or may be read-only on locked-down corporate machines | Low | High | `App.xaml.cs` calls `Directory.CreateDirectory()` before `Database.Migrate()`. Migration failures are caught and show a clear error dialog with the exception message and database path, then exit cleanly. |
| **`C:\Windows\Media\` .wav files vary by Windows edition and language** — different editions ship different sounds | Medium | Low | Sound list is populated dynamically at runtime — users always see exactly what is available. "No sound" option ensures full functionality even if the directory is empty or missing. |
| **High-DPI display rendering** — WPF may not scale correctly on multi-monitor setups with different DPI settings | Medium | Medium | Set per-monitor DPI awareness in the application manifest (`<dpiAwareness>PerMonitorV2</dpiAwareness>`) to ensure crisp rendering when moving between monitors. |
