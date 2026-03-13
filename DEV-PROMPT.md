# TekTomato — Developer Instructions

You are building **TekTomato**, a Windows WPF Pomodoro timer application in C# .NET 8.

## Your job

Read the full PRD at `docs/bmad/01-prd.md` and the full architecture document at `docs/bmad/02-architecture.md` before writing any code. Follow them precisely.

## What to build

A complete, working Visual Studio solution with all source files needed to compile and run TekTomato on Windows 10/11. The user will open the solution in Visual Studio 2022 and hit Build.

## File structure to create

Exactly as specified in Section 8 of the architecture doc:

```
TekTomato.sln
TekTomato/
  TekTomato.csproj
  App.xaml + App.xaml.cs
  Assets/tomato.ico  (create a placeholder if needed)
  Data/TekTomatoDbContext.cs
  Data/Migrations/ (initial migration only)
  Models/PomodoroSession.cs
  Models/Setting.cs
  Services/TimerEngine.cs
  Services/SettingsService.cs
  Services/SoundService.cs
  Services/SessionService.cs
  Services/ThemeService.cs
  ViewModels/OverlayViewModel.cs
  ViewModels/SettingsViewModel.cs
  ViewModels/StatsViewModel.cs
  Views/OverlayWindow.xaml + .xaml.cs
  Views/SettingsView.xaml + .xaml.cs
  Views/StatsView.xaml + .xaml.cs
  Resources/Themes/Common.xaml
  Resources/Themes/Dark.xaml
  Resources/Themes/Light.xaml
```

## Key implementation requirements

1. **Timer:** DispatcherTimer at 250ms intervals. Wall-clock timestamps (DateTime.UtcNow), NOT tick counting. Implement the full state machine: Idle → Running → Paused → Overrun → Completed → Idle.

2. **Negative countdown:** When the timer crosses zero, it continues into negative territory (e.g. -0:32). Sound fires at zero. Action buttons (Start Pomodoro / Short Break / Long Break) appear. Timer keeps ticking until user acts.

3. **NOTHING auto-starts.** Every transition requires a user click.

4. **Opacity:** Use TWO stacked Grid layers inside the overlay window. BackgroundPanel.Opacity is user-adjustable. Content Grid always stays at Opacity=1. Do NOT use Window.Opacity.

5. **Window:** WindowStyle=None, AllowsTransparency=True, Topmost=True, Background=Transparent. Draggable via MouseLeftButtonDown + DragMove().

6. **Themes:** Dark.xaml, Light.xaml, Common.xaml ResourceDictionaries. ThemeService swaps them at runtime. All colours via DynamicResource. System Auto mode reads Windows theme from registry.

7. **Sound:** Enumerate C:\Windows\Media\ .wav files. Dropdown in settings. Store filename only in Settings table. Play via SoundPlayer. "No sound" option at top of list.

8. **Stats:** SQLite Sessions table stores every completed session. Calendar heatmap via SkiaSharp SKElement (custom rendering). Line chart via LiveCharts2 CartesianChart. Date range selector: Last 3/6/12 months / All time.

9. **System tray:** H.NotifyIcon.Wpf TaskbarIcon. Right-click menu: Show, Start, Pause, Exit. Minimise to tray on window close.

10. **Settings persisted** in SQLite Settings table (key/value). SettingsService reads/writes with in-memory cache.

11. **Database auto-migrated** on startup via DbContext.Database.Migrate(). %AppData%\TekTomato\ directory created if missing.

12. **DI container** in App.xaml.cs using Microsoft.Extensions.DependencyInjection. All services, ViewModels, and windows registered.

## NuGet packages (TekTomato.csproj must reference these)

- Microsoft.EntityFrameworkCore.Sqlite 8.*
- Microsoft.EntityFrameworkCore.Design 8.*
- CommunityToolkit.Mvvm 8.*
- H.NotifyIcon.Wpf 2.*
- LiveChartsCore.SkiaSharpView.WPF 2.*
- SkiaSharp.Views.WPF 2.*
- Microsoft.Extensions.DependencyInjection 8.*

## UI style

Clean, minimal, inspired by YAPA 2. Overlay window is small (around 200x120px). Large countdown font (48px+). Single Start/Pause button below the countdown. Session type label above the countdown. When in Overrun state, countdown turns red/accent colour and action buttons appear.

## Quality bar

- Code must compile without errors in Visual Studio 2022
- All features from the PRD implemented
- MVVM throughout (no business logic in code-behind except DragMove, PaintSurface rendering)
- British English in all UI text
- XML doc comments on public members
- No hardcoded strings for UI text (use constants or resource keys)

## When done

After creating all files and making a git commit, run:
openclaw system event --text "Done: TekTomato dev pass complete - all source files created and committed" --mode now

