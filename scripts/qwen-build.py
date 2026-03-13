#!/usr/bin/env python3
"""
TekTomato — Qwen3.5:27b Modular Build Orchestrator
Feeds project modules to Ollama one at a time, parses file output, writes files.
"""

import json
import os
import sys
import time
import urllib.request
import urllib.error
import datetime
import re
import subprocess

OLLAMA_URL = "http://192.168.1.8:11434/api/generate"
MODEL = "qwen3.5:27b"
NUM_CTX = 32768
PROJECT_DIR = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
STATS_LOG = os.path.expanduser("~/.openclaw/workspace/my-data/ollama-stats.jsonl")

# ─── Helpers ────────────────────────────────────────────────────────────────

def read_file(path):
    full = os.path.join(PROJECT_DIR, path)
    with open(full, "r", encoding="utf-8") as f:
        return f.read()

def write_file(rel_path, content):
    full = os.path.join(PROJECT_DIR, rel_path)
    os.makedirs(os.path.dirname(full), exist_ok=True)
    with open(full, "w", encoding="utf-8") as f:
        f.write(content)
    print(f"  ✓ wrote {rel_path}")

def call_ollama(prompt, stage_name):
    """POST to Ollama, return (response_text, stats_dict)"""
    payload = json.dumps({
        "model": MODEL,
        "prompt": prompt,
        "stream": False,
        "options": {
            "num_ctx": NUM_CTX,
            "think": False,
        }
    }).encode("utf-8")

    req = urllib.request.Request(
        OLLAMA_URL,
        data=payload,
        headers={"Content-Type": "application/json"},
        method="POST"
    )

    print(f"\n🦾 Sending {stage_name} to Qwen ({len(prompt)} chars, ~{len(prompt)//4} tokens)...")
    t0 = time.time()

    try:
        with urllib.request.urlopen(req, timeout=1800) as resp:
            raw = resp.read().decode("utf-8")
    except urllib.error.URLError as e:
        print(f"❌ Ollama connection failed: {e}")
        sys.exit(1)

    wall = time.time() - t0
    data = json.loads(raw)

    stats = {
        "timestamp": datetime.datetime.utcnow().isoformat() + "Z",
        "project": "tektomato",
        "stage": stage_name,
        "model": MODEL,
        "prompt_tokens": data.get("prompt_eval_count", 0),
        "output_tokens": data.get("eval_count", 0),
        "output_lines": data.get("response", "").count("\n"),
        "tokens_per_sec": round(data.get("eval_count", 0) / max((data.get("eval_duration", 1) / 1e9), 0.001), 1),
        "prompt_duration_s": round(data.get("prompt_eval_duration", 0) / 1e9, 1),
        "gen_duration_s": round(data.get("eval_duration", 0) / 1e9, 1),
        "total_duration_s": round(data.get("total_duration", 0) / 1e9, 1),
        "output_bytes": len(data.get("response", "").encode("utf-8")),
        "wall_time_s": round(wall, 1),
    }

    print(f"\n📊 Stats — {stage_name}")
    print(f"   Model:         {MODEL}")
    print(f"   Input tokens:  {stats['prompt_tokens']}")
    print(f"   Output tokens: {stats['output_tokens']}")
    print(f"   Lines:         {stats['output_lines']}")
    print(f"   Tokens/sec:    {stats['tokens_per_sec']}")
    print(f"   Prompt time:   {stats['prompt_duration_s']}s")
    print(f"   Gen time:      {stats['gen_duration_s']}s")
    print(f"   Total time:    {stats['total_duration_s']}s")
    print(f"   Wall time:     {stats['wall_time_s']}s")

    # Append to persistent stats log
    os.makedirs(os.path.dirname(STATS_LOG), exist_ok=True)
    with open(STATS_LOG, "a", encoding="utf-8") as f:
        f.write(json.dumps(stats) + "\n")

    return data.get("response", ""), stats

def parse_files(response_text):
    """
    Extract files from response using XML-style markers:
      <file path="TekTomato/Models/Foo.cs">
      ...content...
      </file>
    """
    pattern = re.compile(
        r'<file\s+path=["\']([^"\']+)["\']\s*>(.*?)</file>',
        re.DOTALL
    )
    matches = pattern.findall(response_text)

    if not matches:
        # Fallback: try markdown code blocks with filename comments
        # e.g. ```csharp\n// File: TekTomato/Models/Foo.cs\n...```
        pattern2 = re.compile(
            r'```[a-zA-Z]*\n//\s*[Ff]ile:\s*([^\n]+)\n(.*?)```',
            re.DOTALL
        )
        matches = pattern2.findall(response_text)

    return [(path.strip(), content.strip()) for path, content in matches]

def git_commit(message):
    result = subprocess.run(
        ["git", "add", "-A"],
        cwd=PROJECT_DIR, capture_output=True, text=True
    )
    result = subprocess.run(
        ["git", "commit", "-m", message],
        cwd=PROJECT_DIR, capture_output=True, text=True
    )
    if result.returncode == 0:
        print(f"  ✓ git commit: {message}")
    else:
        print(f"  ⚠ git commit output: {result.stdout} {result.stderr}")

def collect_generated_code(paths):
    """Read previously written files and return as a context block."""
    parts = []
    for rel_path in paths:
        full = os.path.join(PROJECT_DIR, rel_path)
        if os.path.exists(full):
            with open(full, "r", encoding="utf-8") as f:
                content = f.read()
            parts.append(f'<file path="{rel_path}">\n{content}\n</file>')
    return "\n\n".join(parts)

# ─── Shared context ──────────────────────────────────────────────────────────

PRD = read_file("docs/bmad/01-prd.md")
ARCH = read_file("docs/bmad/02-architecture.md")

FILE_FORMAT_INSTRUCTION = """
Output EVERY file you create using this EXACT XML format — no exceptions:

<file path="TekTomato/Folder/FileName.cs">
[complete file content here]
</file>

Rules:
- Include the COMPLETE content of every file, not snippets or placeholders
- Do not truncate any file with "// ... rest of implementation"
- Every file must compile without errors
- Use British English in all comments and UI text
- Include XML doc comments (///) on all public members
"""

# ─── Module 1: Foundation ────────────────────────────────────────────────────

MODULE1_FILES = [
    "TekTomato.sln",
    "TekTomato/TekTomato.csproj",
    "TekTomato/App.xaml",
    "TekTomato/App.xaml.cs",
    "TekTomato/Models/PomodoroSession.cs",
    "TekTomato/Models/Setting.cs",
    "TekTomato/Data/TekTomatoDbContext.cs",
    "TekTomato/Data/Migrations/20260313000001_InitialCreate.cs",
    "TekTomato/Data/Migrations/TekTomatoDbContextModelSnapshot.cs",
]

MODULE1_PROMPT = f"""You are a senior C# developer building TekTomato, a Windows WPF Pomodoro timer.

## Architecture Reference
{ARCH}

## Task: Module 1 — Foundation

Generate these files to bootstrap the project:

1. `TekTomato.sln` — Visual Studio 2022 solution file referencing TekTomato/TekTomato.csproj
2. `TekTomato/TekTomato.csproj` — .NET 8 WPF project file with all required NuGet package references:
   - Microsoft.EntityFrameworkCore.Sqlite 8.*
   - Microsoft.EntityFrameworkCore.Design 8.*
   - CommunityToolkit.Mvvm 8.*
   - H.NotifyIcon.Wpf 2.*
   - LiveChartsCore.SkiaSharpView.WPF 2.*
   - SkiaSharp.Views.WPF 2.*
   - Microsoft.Extensions.DependencyInjection 8.*
   Also include the app manifest for per-monitor DPI awareness.
3. `TekTomato/App.xaml` — Application XAML. Merge Common.xaml and Dark.xaml (default theme) into Application.Resources. Do NOT define StartupUri (the overlay window is shown in code).
4. `TekTomato/App.xaml.cs` — Application startup: create DI container, register ALL services and ViewModels as singletons, resolve and show OverlayWindow, handle application exit cleanly. Services to register: TekTomatoDbContext (transient via factory), SettingsService, SoundService, SessionService, ThemeService, TimerEngine, OverlayViewModel, SettingsViewModel, StatsViewModel, OverlayWindow. Run EF migrations on startup. Create %AppData%\\TekTomato\\ directory if missing.
5. `TekTomato/Models/PomodoroSession.cs` — EF Core entity. Properties: Id (int, PK autoincrement), SessionType (string), StartedAtUtc (DateTime), CompletedAtUtc (DateTime), PlannedDurationSeconds (int), ActualDurationSeconds (int), OverrunSeconds (int, default 0), PausedDurationSeconds (int, default 0), CompletedNormally (bool, default true), PomodoroNumber (int?, nullable).
6. `TekTomato/Models/Setting.cs` — EF Core entity. Properties: Key (string, PK), Value (string).
7. `TekTomato/Data/TekTomatoDbContext.cs` — EF Core DbContext with DbSet<PomodoroSession> and DbSet<Setting>. OnModelCreating: configure indexes on Sessions (StartedAtUtc; SessionType+CompletedNormally). Database path: Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TekTomato", "tektomato.db").
8. `TekTomato/Data/Migrations/20260313000001_InitialCreate.cs` — EF Core migration creating both tables with all columns and indexes as defined in the architecture.
9. `TekTomato/Data/Migrations/TekTomatoDbContextModelSnapshot.cs` — EF Core model snapshot corresponding to the migration above.

{FILE_FORMAT_INSTRUCTION}
"""

# ─── Module 2: Services ──────────────────────────────────────────────────────

MODULE2_FILES = [
    "TekTomato/Services/TimerEngine.cs",
    "TekTomato/Services/SettingsService.cs",
    "TekTomato/Services/SoundService.cs",
    "TekTomato/Services/SessionService.cs",
    "TekTomato/Services/ThemeService.cs",
]

def module2_prompt(prev_code):
    return f"""You are a senior C# developer building TekTomato, a Windows WPF Pomodoro timer.

## Architecture Reference (key sections)

### Timer Engine (Section 5)
- DispatcherTimer at 250ms intervals using wall-clock timestamps (DateTime.UtcNow), NOT tick counting
- State machine: Idle → Running → (Paused ↔ Running) → Overrun → Completed → Idle
- When remaining seconds <= 0: fire sound, enter Overrun state, continue ticking into negative
- NOTHING auto-starts — all transitions require user action
- Internal fields: _sessionStartedAtUtc, _plannedDurationSeconds, _pauseStartedAtUtc, _totalPausedSeconds, _completedAtUtc, _overrunPauseStartedAtUtc, _totalOverrunPausedSeconds
- Expose observable properties: CurrentState (enum), DisplayText (string, formatted as "MM:SS" or "-M:SS"), SessionType (enum), ShowActionButtons (bool), PomodoroCount (int in current cycle)
- Expose commands/methods: StartSession(SessionType), Pause(), Resume(), Cancel()
- On Completed transition: call SessionService.RecordSessionAsync() with all timing data

### Settings (Section 3)
Default values: WorkDurationMinutes=25, ShortBreakDurationMinutes=5, LongBreakDurationMinutes=15, LongBreakIntervalPomodoros=4, SoundFileName="Windows Notify System Generic.wav", Theme=SystemAuto, BackgroundOpacityPercent=80, OverlayPositionX=null, OverlayPositionY=null

### Sound (Section 7)
- Enumerate C:\\Windows\\Media\\ .wav files via Directory.GetFiles
- Store filename only; reconstruct full path at playback
- New SoundPlayer per play; LoadAsync then Play on LoadCompleted
- Fallback to SystemSounds.Beep if file missing

### Theme (Section 4)
- ThemeType enum: Dark, Light, SystemAuto
- Swap ResourceDictionary in Application.Current.Resources.MergedDictionaries at runtime
- System Auto: read HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize\\AppsUseLightTheme

### Session aggregation queries needed by StatsViewModel:
- GetDailyPomodoroCounts(DateOnly? from, DateOnly? to) → Dictionary<DateOnly, int>
- GetDailyFocusMinutes(DateOnly? from, DateOnly? to) → List<(DateOnly date, double minutes)>
- GetWeeklyFocusMinutes(DateOnly? from, DateOnly? to) → List<(DateOnly weekStart, double minutes)>

## Previously generated code (Module 1)
{prev_code}

## Task: Module 2 — Services

Generate these 5 service files:

1. `TekTomato/Services/TimerEngine.cs` — Full state machine as described above. Expose SessionType enum (Work, ShortBreak, LongBreak) and TimerState enum (Idle, Running, Paused, Overrun, Completed) in this file or a shared location. Use ObservableObject from CommunityToolkit.Mvvm.
2. `TekTomato/Services/SettingsService.cs` — Reads/writes settings to DB (key/value). In-memory cache. Strongly-typed properties for all settings. Async load on construction. Default values used if key not in DB.
3. `TekTomato/Services/SoundService.cs` — GetAvailableSounds() returns IEnumerable<string> of filenames. PlaySelectedSound() as described in architecture.
4. `TekTomato/Services/SessionService.cs` — RecordSessionAsync(PomodoroSession), GetDailyPomodoroCounts(), GetDailyFocusMinutes(), GetWeeklyFocusMinutes() as above.
5. `TekTomato/Services/ThemeService.cs` — ApplyTheme(ThemeType), CurrentTheme property, system auto detection.

{FILE_FORMAT_INSTRUCTION}
"""

# ─── Module 3: ViewModels ────────────────────────────────────────────────────

MODULE3_FILES = [
    "TekTomato/ViewModels/OverlayViewModel.cs",
    "TekTomato/ViewModels/SettingsViewModel.cs",
    "TekTomato/ViewModels/StatsViewModel.cs",
]

def module3_prompt(prev_code):
    return f"""You are a senior C# developer building TekTomato, a Windows WPF Pomodoro timer.

## Architecture Reference (key sections)

### MVVM Pattern
- Use CommunityToolkit.Mvvm: [ObservableProperty], [RelayCommand] source generators
- ObservableObject base class
- No business logic in code-behind; ViewModels are the only presentation logic layer

### OverlayViewModel
- Wraps TimerEngine — subscribes to its property changes
- Properties to expose: DisplayText, SessionTypeLabel (e.g. "Focus", "Short Break"), CurrentState, ShowActionButtons, BackgroundOpacity (double 0-1, from SettingsService.BackgroundOpacityPercent/100)
- Commands: StartPomodoroCommand, StartShortBreakCommand, StartLongBreakCommand, PauseCommand, ResumeCommand, OpenSettingsCommand, OpenStatsCommand
- Start/Pause button text: "Start" when Idle, "Pause" when Running/Overrun, "Resume" when Paused
- Show action buttons (Start Pomodoro / Short Break / Long Break) only in Overrun or after break completes
- Countdown display: normal colour in Running/Paused, red (use DynamicResource AccentDanger) in Overrun

### SettingsViewModel
- Expose all configurable settings as [ObservableProperty]: WorkDurationMinutes, ShortBreakMinutes, LongBreakMinutes, LongBreakInterval, SelectedSound, SelectedTheme, BackgroundOpacityPercent
- AvailableSounds: ObservableCollection<string> from SoundService.GetAvailableSounds(), "No sound" prepended
- AvailableThemes: list of ThemeType enum values as strings
- [RelayCommand] SaveCommand — writes all settings to SettingsService, applies theme
- [RelayCommand] CancelCommand — closes without saving

### StatsViewModel
- Properties: SelectedRange (enum: Last3Months, Last6Months, Last12Months, AllTime), IsWeeklyView (bool)
- HeatmapData: Dictionary<DateOnly, int> — date to completed pomodoro count
- LineChartSeries: ISeries[] for LiveCharts2 CartesianChart
- XAxes: ICartesianAxis[] for LiveCharts2 (DateTimeAxis with appropriate label formatter)
- [RelayCommand] RefreshCommand — re-queries data and updates chart bindings
- LoadAsync() method called on construction

## Previously generated code (Modules 1-2)
{prev_code}

## Task: Module 3 — ViewModels

Generate these 3 ViewModels. They must compile against the services from Module 2.

{FILE_FORMAT_INSTRUCTION}
"""

# ─── Module 4: Views + Themes ────────────────────────────────────────────────

MODULE4_FILES = [
    "TekTomato/Resources/Themes/Common.xaml",
    "TekTomato/Resources/Themes/Dark.xaml",
    "TekTomato/Resources/Themes/Light.xaml",
    "TekTomato/Views/OverlayWindow.xaml",
    "TekTomato/Views/OverlayWindow.xaml.cs",
    "TekTomato/Views/SettingsView.xaml",
    "TekTomato/Views/SettingsView.xaml.cs",
    "TekTomato/Views/StatsView.xaml",
    "TekTomato/Views/StatsView.xaml.cs",
]

def module4_prompt(prev_code):
    return f"""You are a senior C# developer building TekTomato, a Windows WPF Pomodoro timer.

## Architecture Reference (key sections)

### Overlay Window
- WindowStyle=None, AllowsTransparency=True, Topmost=True, Background=Transparent
- Size: approximately 220x140px. Can be dragged anywhere.
- TWO stacked Grid layers: BackgroundPanel (opacity adjustable, bound to OverlayViewModel.BackgroundOpacity), ContentGrid (always Opacity=1.0)
- Layout: session type label (small, top), countdown text (large font 52px, centre), start/pause button (bottom)
- Overrun state: countdown text turns AccentDanger colour, action buttons row appears below main button
- Action buttons: "🍅 Start", "☕ Short Break", "☕ Long Break"
- Top-right: small icon buttons for Settings and Stats
- Code-behind: MouseLeftButtonDown → DragMove(), intercept window Close to minimise to tray instead

### Theme ResourceDictionaries
Common.xaml: button styles, text styles, control templates, font families
Dark.xaml: colour palette — PrimaryBackground=#1a1a1a, SecondaryBackground=#2d2d2d, PrimaryForeground=#ffffff, SecondaryForeground=#999999, AccentGreen=#4caf50, AccentDanger=#e53935, ButtonBackground=#3d3d3d, ButtonHover=#555555
Light.xaml: PrimaryBackground=#f5f5f5, SecondaryBackground=#e0e0e0, PrimaryForeground=#212121, SecondaryForeground=#666666, AccentGreen=#388e3c, AccentDanger=#d32f2f, ButtonBackground=#dddddd, ButtonHover=#cccccc

### Settings View
- Modal window (not UserControl) opened from OverlayViewModel
- Sections: Timer Durations (work/short/long/interval), Sound (dropdown), Appearance (theme dropdown, opacity slider), Save/Cancel buttons
- Opacity slider: 0-100 with label showing current value

### Stats View
- Secondary window with two sections:
  1. Calendar heatmap: SKElement for SkiaSharp rendering, date range ComboBox (Last 3/6/12 months, All time)
  2. Line chart: LiveCharts2 CartesianChart, Day/Week toggle RadioButtons
- Window size: approximately 700x450px

## Previously generated code (Modules 1-3)
{prev_code}

## Task: Module 4 — Views and Themes

Generate these 9 files. XAML must be valid WPF markup. SKElement PaintSurface handler in OverlayWindow.xaml.cs or StatsView.xaml.cs should call a HeatmapRenderer helper method with the HeatmapData dictionary from StatsViewModel.

{FILE_FORMAT_INSTRUCTION}
"""

# ─── Main orchestration ──────────────────────────────────────────────────────

def run_module(name, prompt, expected_files, stage_key):
    print(f"\n{'='*60}")
    print(f"  MODULE: {name}")
    print(f"{'='*60}")

    response, stats = call_ollama(prompt, stage_key)

    # Save raw response for debugging
    raw_path = os.path.join(PROJECT_DIR, f"docs/bmad/raw-{stage_key}.txt")
    with open(raw_path, "w", encoding="utf-8") as f:
        f.write(response)
    print(f"  ✓ raw response saved to docs/bmad/raw-{stage_key}.txt")

    files = parse_files(response)
    if not files:
        print(f"  ⚠️  WARNING: No <file> blocks found in response! Check raw-{stage_key}.txt")
        return []

    print(f"\n  📁 Writing {len(files)} files...")
    written = []
    for path, content in files:
        write_file(path, content)
        written.append(path)

    git_commit(f"feat: {name} — generated by Qwen3.5:27b")
    return written

def main():
    print("🍅 TekTomato — Qwen3.5:27b Modular Build")
    print(f"   Model:   {MODEL}")
    print(f"   Context: {NUM_CTX} tokens")
    print(f"   Project: {PROJECT_DIR}")

    # Test Ollama connectivity
    try:
        req = urllib.request.Request(
            "http://192.168.1.8:11434/api/tags",
            method="GET"
        )
        with urllib.request.urlopen(req, timeout=10) as resp:
            tags = json.loads(resp.read())
        models = [m["name"] for m in tags.get("models", [])]
        if not any("qwen3.5" in m for m in models):
            print(f"⚠️  qwen3.5:27b not found. Available: {models}")
        else:
            print(f"✅ Ollama connected. qwen3.5:27b available.")
    except Exception as e:
        print(f"❌ Cannot reach Ollama at 192.168.1.8:11434: {e}")
        sys.exit(1)

    # Track what's been generated for context
    all_generated = []

    # Module 1
    written = run_module(
        "Module 1: Foundation",
        MODULE1_PROMPT,
        MODULE1_FILES,
        "module1-foundation"
    )
    all_generated.extend(written)

    # Module 2
    prev_code = collect_generated_code(all_generated)
    written = run_module(
        "Module 2: Services",
        module2_prompt(prev_code),
        MODULE2_FILES,
        "module2-services"
    )
    all_generated.extend(written)

    # Module 3
    prev_code = collect_generated_code(all_generated)
    written = run_module(
        "Module 3: ViewModels",
        module3_prompt(prev_code),
        MODULE3_FILES,
        "module3-viewmodels"
    )
    all_generated.extend(written)

    # Module 4
    prev_code = collect_generated_code(all_generated)
    written = run_module(
        "Module 4: Views + Themes",
        module4_prompt(prev_code),
        MODULE4_FILES,
        "module4-views"
    )
    all_generated.extend(written)

    print(f"\n{'='*60}")
    print(f"  ✅ BUILD COMPLETE — {len(all_generated)} files generated")
    print(f"{'='*60}")

    # Notify OpenClaw
    os.system('openclaw system event --text "Done: TekTomato Qwen build complete - all modules generated" --mode now')

if __name__ == "__main__":
    main()
