using System;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TekTomato.Services;
using TekTomato.Views;

namespace TekTomato.ViewModels
{
    /// <summary>
    /// View model for the main overlay window displaying the Pomodoro timer.
    /// Wraps TimerEngine and exposes timer state, display text, and control commands.
    /// </summary>
    public partial class OverlayViewModel : ObservableObject
    {
        #region Private Fields

        private readonly TimerEngine _timerEngine;
        private readonly SettingsService _settingsService;
        private readonly ThemeService _themeService;
        private readonly SoundService _soundService;
        private readonly SessionService _sessionService;

        // Navigation commands target views
        private readonly SettingsViewModel _settingsViewModel;
        private readonly StatsViewModel _statsViewModel;

        #endregion Private Fields

        #region Observable Properties

        /// <summary>
        /// Gets the formatted timer display text (MM:SS or -M:SS for overrun).
        /// </summary>
        [ObservableProperty]
        private string _displayText = "Ready";

        /// <summary>
        /// Gets the session type label for display ("Focus", "Short Break", "Long Break").
        /// </summary>
        [ObservableProperty]
        private string _sessionTypeLabel = "Focus";

        /// <summary>
        /// Gets the current timer state (Idle, Running, Paused, Overrun, Completed).
        /// </summary>
        [ObservableProperty]
        private TimerState _currentState = TimerState.Idle;

        /// <summary>
        /// Gets whether action buttons (Start Pomodoro/Short Break/Long Break) should be shown.
        /// Shown in Idle or after break completes.
        /// </summary>
        [ObservableProperty]
        private bool _showActionButtons = true;

        /// <summary>
        /// Gets the background opacity as a value between 0 and 1 for semi-transparent overlay.
        /// Derived from BackgroundOpacityPercent setting divided by 100.
        /// </summary>
        [ObservableProperty]
        private double _backgroundOpacity = 0.8;

        /// <summary>
        /// Gets the text to display on the main start/pause/resume button.
        /// "Start" when Idle, "Pause" when Running/Overrun, "Resume" when Paused.
        /// </summary>
        [ObservableProperty]
        private string _buttonText = "Start";

        #endregion Observable Properties

        #region Commands

        /// <summary>
        /// Command to start a Pomodoro work session.
        /// Available when in Idle or Completed state.
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanStartSession))]
        private void StartPomodoro()
        {
            _timerEngine.StartSession(SessionType.Work);
            UpdateUIState();
        }

        /// <summary>
        /// Command to start a short break session.
        /// Available when in Idle or Completed state.
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanStartSession))]
        private void StartShortBreak()
        {
            _timerEngine.StartSession(SessionType.ShortBreak);
            UpdateUIState();
        }

        /// <summary>
        /// Command to start a long break session.
        /// Available when in Idle or Completed state.
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanStartSession))]
        private void StartLongBreak()
        {
            _timerEngine.StartSession(SessionType.LongBreak);
            UpdateUIState();
        }

        /// <summary>
        /// Command to pause the currently running timer.
        /// Only enabled when timer is Running or Overrun.
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanPause))]
        private void Pause()
        {
            _timerEngine.Pause();
            UpdateUIState();
        }

        /// <summary>
        /// Command to resume a paused timer.
        /// Only enabled when timer is Paused (from Running or Overrun).
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanResume))]
        private void Resume()
        {
            _timerEngine.Resume();
            UpdateUIState();
        }

        /// <summary>
        /// Command to mark the current session as completed and record it.
        /// Available when timer is Running, Paused, or Overrun (before completing normally).
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanCompleteSession))]
        private void CompleteSession()
        {
            // Handle overrun state completion differently
            if (_timerEngine.CurrentState == TimerState.Overrun)
            {
                // Overrun completed - record the session with overrun data
                RecordOverrunSession();
            }
            else
            {
                _timerEngine.MarkAsCompleted();
            }
            
            UpdateUIState();
        }

        /// <summary>
        /// Command to cancel/abandon the current session.
        /// </summary>
        [RelayCommand]
        private void CancelSession()
        {
            _timerEngine.Cancel();
            UpdateUIState();
        }

        /// <summary>
        /// Command to open the Settings window.
        /// </summary>
        [RelayCommand]
        private void OpenSettings()
        {
            // Show settings dialog - the actual window showing is handled by App.xaml.cs or navigation logic
            var settingsWindow = new SettingsWindow();
            settingsWindow.Show();
        }

        /// <summary>
        /// Command to open the Statistics window.
        /// </summary>
        [RelayCommand]
        private void OpenStats()
        {
            // Show stats dialog - the actual window showing is handled by App.xaml.cs or navigation logic
            var statsWindow = new StatsWindow();
            statsWindow.Show();
        }

        #endregion Commands

        #region Constructor

        /// <summary>
        /// Initialises the OverlayViewModel with required services.
        /// Subscribes to TimerEngine property changes to update UI bindings.
        /// </summary>
        public OverlayViewModel(
            TimerEngine timerEngine,
            SettingsService settingsService,
            ThemeService themeService,
            SoundService soundService,
            SessionService sessionService,
            SettingsViewModel settingsViewModel,
            StatsViewModel statsViewModel)
        {
            _timerEngine = timerEngine ?? throw new ArgumentNullException(nameof(timerEngine));
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));
            _soundService = soundService ?? throw new ArgumentNullException(nameof(soundService));
            _sessionService = sessionService ?? throw new ArgumentNullException(nameof(sessionService));
            _settingsViewModel = settingsViewModel;
            _statsViewModel = statsViewModel;

            // Subscribe to TimerEngine property changes
            _timerEngine.PropertyChanged += OnTimerEnginePropertyChanged;

            // Initial state update
            UpdateUIState();
        }

        #endregion Constructor

        #region Private Methods

        /// <summary>
        /// Handles property changes from TimerEngine and updates the UI accordingly.
        /// </summary>
        private void OnTimerEnginePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            // Handle display text updates - this is called on every timer tick (250ms)
            if (e.PropertyName == nameof(TimerEngine.DisplayText))
            {
                UpdateDisplayText();
            }
            else if (e.PropertyName == nameof(TimerEngine.CurrentState))
            {
                UpdateUIState();
            }
            else if (e.PropertyName == nameof(TimerEngine.SessionType))
            {
                UpdateSessionTypeLabel();
            }
        }

        /// <summary>
        /// Updates the display text property from TimerEngine.
        /// </summary>
        private void UpdateDisplayText()
        {
            DisplayText = _timerEngine.DisplayText;
        }

        /// <summary>
        /// Updates all UI state properties based on current timer engine state.
        /// </summary>
        private void UpdateUIState()
        {
            // Update display text
            UpdateDisplayText();

            // Update button text based on state
            var newState = _timerEngine.CurrentState;
            
            if (newState == TimerState.Idle || newState == TimerState.Completed)
            {
                ButtonText = "Start";
                ShowActionButtons = true;
            }
            else if (newState == TimerState.Running || newState == TimerState.Overrun)
            {
                ButtonText = "Pause";
                // Show action buttons in Overrun to allow session reset
                ShowActionButtons = false;
            }
            else if (newState == TimerState.Paused)
            {
                ButtonText = "Resume";
                ShowActionButtons = false;
            }

            // Update session type label
            UpdateSessionTypeLabel();

            // Update background opacity from settings
            BackgroundOpacity = _settingsService.BackgroundOpacityPercent / 100.0;

            // Raise command canExecute checks to update button states
            StartPomodoroCommand.NotifyCanExecuteChanged();
            StartShortBreakCommand.NotifyCanExecuteChanged();
            StartLongBreakCommand.NotifyCanExecuteChanged();
            PauseCommand.NotifyCanExecuteChanged();
            ResumeCommand.NotifyCanExecuteChanged();
            CompleteSessionCommand.NotifyCanExecuteChanged();
        }

        /// <summary>
        /// Updates the session type label property for display.
        /// </summary>
        private void UpdateSessionTypeLabel()
        {
            SessionTypeLabel = _timerEngine.SessionType switch
            {
                SessionType.Work => "Focus",
                SessionType.ShortBreak => "Short Break",
                SessionType.LongBreak => "Long Break",
                _ => string.Empty
            };
        }

        /// <summary>
        /// Records an overrun session when timer exceeds planned duration.
        /// </summary>
        private void RecordOverrunSession()
        {
            // Calculate final metrics for overrun sessions
            var now = DateTime.UtcNow;
            var elapsedSeconds = (now - _timerEngine.GetSessionStartedAtUtc()).TotalSeconds 
                - _timerEngine.GetTotalPausedSeconds();
            var overrunSeconds = Math.Max(0, (int)(elapsedSeconds - _timerEngine.GetPlannedDurationSeconds()));
            
            // SessionEngine needs to expose methods to retrieve private timing data
            // For now, we'll use MarkAsCompleted and let TimerEngine handle overrun logic internally
            _timerEngine.MarkAsCompleted();
        }

        #endregion Private Methods

        #region Command CanExecute Conditions

        /// <summary>
        /// Returns true when a new session can be started (Idle or Completed state).
        /// </summary>
        private bool CanStartSession()
        {
            return _timerEngine.CurrentState == TimerState.Idle || 
                   _timerEngine.CurrentState == TimerState.Completed;
        }

        /// <summary>
        /// Returns true when the timer can be paused (Running or Overrun state).
        /// </summary>
        private bool CanPause()
        {
            return _timerEngine.CurrentState == TimerState.Running || 
                   _timerEngine.CurrentState == TimerState.Overrun;
        }

        /// <summary>
        /// Returns true when the timer can be resumed (Paused state).
        /// </summary>
        private bool CanResume()
        {
            return _timerEngine.CurrentState == TimerState.Paused;
        }

        /// <summary>
        /// Returns true when a session can be manually completed.
        /// </summary>
        private bool CanCompleteSession()
        {
            return _timerEngine.CurrentState == TimerState.Running || 
                   _timerEngine.CurrentState == TimerState.Paused ||
                   _timerEngine.CurrentState == TimerState.Overrun;
        }

        #endregion Command CanExecute Conditions
    }
}