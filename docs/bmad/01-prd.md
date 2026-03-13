# TekTomato — Product Requirements Document

## 1. Problem Statement

Existing Pomodoro timers for Windows fall into two camps: minimal overlays with no stats (e.g. YAPA 2), or feature-bloated apps that demand attention. There's no lightweight, always-on-top timer that combines a clean overlay aesthetic with meaningful productivity tracking (heatmaps, charts) and deep configurability — all running locally with zero cloud dependency.

TekTomato fills that gap: a portable Windows Pomodoro timer that stays out of your way but gives you the data to understand your focus habits.

---

## 2. Core Features

### MVP (v1.0)

| # | Feature | Description |
|---|---------|-------------|
| 1 | **Pomodoro Timer** | Countdown timer for work sessions, short breaks (default 5 min), long breaks (default 15 min). All durations configurable. |
| 2 | **Always-on-Top Overlay** | Small, borderless window displaying countdown in large font + Start/Pause button. Draggable to any screen position. |
| 3 | **Session Flow** | On work session completion, sound fires at 0:00 and the timer continues into **negative territory** (e.g. −0:32) showing how long the user has overrun. Completion prompt (Start Pomodoro / 5 min break / 15 min break) is shown but the user decides when to act. On break completion, prompt to start next Pomodoro — break does NOT auto-start. **Nothing ever auto-starts.** |
| 4 | **Pause/Resume** | Pause and resume at any point during any session type. |
| 5 | **Window Opacity** | Adjustable background transparency (0–100%). Timer text and controls remain fully opaque regardless. |
| 6 | **Themes** | Dark, Light, and System Auto (follows Windows theme). |
| 7 | **Sound Notifications** | Play a sound on session completion. Settings dropdown to choose from available Windows system notification sounds. |
| 8 | **System Tray** | Minimise to system tray. Tray icon: tomato (🍅-inspired .ico). Right-click menu: Show, Start, Pause, Exit. |
| 9 | **Settings Panel** | Configurable: work duration, short break, long break, long break interval (every N pomodoros), sound selection, theme, opacity, auto-start breaks, auto-start pomodoros. |
| 10 | **Stats: Calendar Heatmap** | GitHub-style contribution heatmap showing Pomodoros completed per day. Viewable by week, month, and year. |
| 11 | **Stats: Line Chart** | Time spent in focus sessions over a configurable period (last 1–12 months). |
| 12 | **Local Data Storage** | All session history and settings stored locally (e.g. SQLite or JSON file alongside the .exe). No cloud, no login. |
| 13 | **Portable Executable** | Single .exe, no installer required. ⚠️ *Flag: confirm user wants portable .exe vs MSI/installer option.* |
| 14 | **Branding** | "TekTomato" name and tomato icon throughout. |

### Nice-to-Haves (Post-MVP)

- Keyboard shortcuts (global hotkeys to start/pause/skip)
- Daily focus goal (e.g. "aim for 8 Pomodoros today") with progress indicator
- Export stats to CSV
- Custom sounds (load your own .wav files)
- Session tagging/categories (e.g. "Deep Work", "Admin")
- Multi-monitor awareness (remember position per display)
- Minimal "tick" animation or progress ring around the timer

---

## 3. User Stories

- **As a user**, I want to start a Pomodoro with one click so I can begin focusing immediately.
- **As a user**, I want the timer overlay to float above all windows so I can see my remaining time without switching apps.
- **As a user**, I want to adjust the window opacity so the timer blends into my desktop without obscuring content.
- **As a user**, I want to pause the timer at any point so I can handle interruptions without losing my session.
- **As a user**, I want to choose between another Pomodoro or a break when a session ends so I can control my workflow.
- **As a user**, I want to hear a notification sound when a session completes so I don't have to watch the timer.
- **As a user**, I want to see a heatmap of my completed Pomodoros so I can visualise my consistency over time.
- **As a user**, I want to see a line chart of focus time so I can track trends across weeks and months.
- **As a user**, I want to configure all durations and settings so the app fits my personal workflow.
- **As a user**, I want to minimise to the system tray so the app doesn't clutter my taskbar.
- **As a user**, I want to switch between dark and light themes so the UI matches my desktop environment.

---

## 4. Out of Scope

- Cloud sync, accounts, or login
- Mobile companion app
- Team/collaboration features
- Integration with third-party tools (Todoist, Jira, etc.)
- macOS or Linux support
- Auto-update mechanism (MVP)
- Subscription or payment model

---

## 5. Success Criteria

- Timer is accurate to ±1 second over a 25-minute session
- Overlay renders correctly on standard (100%) and high-DPI (125%, 150%, 200%) displays
- Background opacity is adjustable without affecting text legibility
- Stats persist across application restarts with no data loss
- Application launches in under 2 seconds
- .exe runs without requiring admin privileges or .NET installation beyond what ships with Windows 10/11
- All UI text uses British English

---

## 6. Constraints & Open Questions

### Constraints

- **Platform:** Windows 10/11 only
- **Tech stack:** C# WPF expected — to be confirmed at architecture stage
- **Storage:** Local only (file-based, stored alongside the .exe or in AppData)
- **Language:** British English throughout the UI
- **Offline:** Fully functional with no internet connection
- **Dependencies:** Minimise external runtime requirements (ideally self-contained)

### Open Questions

| # | Question | Notes |
|---|----------|-------|
| 1 | **Portable .exe vs installer?** | User preference is portable. Confirm: is a single self-contained .exe essential, or would a folder with .exe + data files be acceptable? |
| 2 | **Long break interval** | Default to every 4 Pomodoros? Make configurable? |
| 3 | **Auto-start behaviour** | ✅ **RESOLVED:** Nothing auto-starts. Breaks and Pomodoros only begin when the user explicitly clicks. Timer continues into negative numbers after a Pomodoro completes, so the user can carry on working until they're ready to stop. |
| 4 | **Stats data location** | Store alongside .exe (truly portable) or in `%AppData%` (survives moving the .exe)? |
| 5 | **Minimum Windows version** | Windows 10 21H2+ sufficient, or need older support? |
| 6 | **.NET dependency** | Self-contained publish (larger .exe, ~60-150MB) vs framework-dependent (smaller, requires .NET runtime)? Affects portability. |

---

*Document version: 0.1 — Draft for review*
*Date: 13 March 2026*
