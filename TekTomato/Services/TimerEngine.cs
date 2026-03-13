using System;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using TekTomato.Services.DataAccess;

namespace TekTomato.Services
{
    /// <summary>
    /// Represents the type of session being tracked.
    /// </summary>
    public enum SessionType
    {
        Work,
        ShortBreak,
        LongBreak
    }

    /// <summary>
    /// Represents the current state of the timer engine.
    /// </summary>
    public enum TimerState
    {
        Idle,
        Running,
        Paused,
        Overrun,
        Completed
    }

    /// <summary>
    /// Core timer engine implementing the Pomodoro timer state machine.
    /// Uses wall-clock timestamps for accurate time tracking across pauses.
    /// </summary>
    public partial class TimerEngine : ObservableObject
    {
        #region Private Fields

        private readonly DispatcherTimer _timer;
        private readonly SessionService _sessionService;
        private readonly SoundService _soundService;
        private readonly SettingsService _settingsService;

        // Timing data fields
        private DateTime _sessionStartedAtUtc;
        private int _plannedDurationSeconds;
        private DateTime _pauseStartedAtUtc;
        private long _totalPausedSeconds;
        private DateTime _completedAtUtc;
        private DateTime _overrunPauseStartedAtUtc;
        private long _totalOverrunPausedSeconds;

        // State tracking
        private TimerState _currentState = TimerState.Idle;
        private SessionType _currentSessionType;
        private int _pomodoroCountInCycle = 0;

        #endregion Private Fields

        #region Constructor

        /// <summary>
        /// Initialises the timer engine with required services.
        /// </summary>
        public TimerEngine(SessionService sessionService, SoundService soundService, SettingsService settingsService)
        {
            _sessionService = sessionService ?? throw new ArgumentNullException(nameof(sessionService));
            _soundService = soundService ?? throw new ArgumentNullException(nameof(soundService));
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));

            // Configure DispatcherTimer at 250ms intervals
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(250)
            };
            _timer.Tick += OnTimerTick;

            // Set initial state
            ResetState();
        }

        #endregion Constructor

        #region Properties (Observable)

        /// <summary>
        /// Gets the current state of the timer engine.
        /// </summary>
        public TimerState CurrentState
        {
            get => _currentState;
            private set => SetProperty(ref _currentState, value);
        }

        /// <summary>
        /// Gets or sets the formatted display text (MM:SS or -M:SS for overrun).
        /// </summary>
        public string DisplayText
        {
            get => CalculateDisplayText();
            private set => SetProperty(ref _displayText, value);
        }

        private string _displayText;

        /// <summary>
        /// Gets the type of current session (Work, ShortBreak, LongBreak).
        /// </summary>
        public SessionType SessionType
        {
            get => _currentSessionType;
            private set => SetProperty(ref _currentSessionType, value);
        }

        /// <summary>
        /// Gets a value indicating whether action buttons should be shown.
        /// </summary>
        public bool ShowActionButtons
        {
            get => _showActionButtons;
            private set => SetProperty(ref _showActionButtons, value);
        }

        private bool _showActionButtons = true;

        /// <summary>
        /// Gets the current pomodoro count in the cycle (for work sessions).
        /// </summary>
        public int PomodoroCount
        {
            get => _pomodoroCountInCycle;
            private set => SetProperty(ref _pomodoroCountInCycle, value);
        }

        #endregion Properties (Observable)

        #region Public Methods

        /// <summary>
        /// Starts a new timer session with the specified type.
        /// </summary>
        /// <param name="sessionType">The type of session to start.</param>
        public void StartSession(SessionType sessionType)
        {
            if (CurrentState != TimerState.Idle && CurrentState != TimerState.Completed)
            {
                throw new InvalidOperationException($"Cannot start session when in state: {CurrentState}");
            }

            _currentSessionType = sessionType;
            _sessionStartedAtUtc = DateTime.UtcNow;
            _plannedDurationSeconds = GetDurationForSessionType(sessionType);
            _totalPausedSeconds = 0;
            _totalOverrunPausedSeconds = 0;

            if (sessionType == SessionType.Work)
            {
                _pomodoroCountInCycle++;
            }

            CurrentState = TimerState.Running;
            ShowActionButtons = true;

            _timer.Start();
        }

        /// <summary>
        /// Pauses the currently running session.
        /// </summary>
        public void Pause()
        {
            if (CurrentState == TimerState.Running)
            {
                CurrentState = TimerState.Paused;
                _pauseStartedAtUtc = DateTime.UtcNow;
            }
            else if (CurrentState == TimerState.Overrun)
            {
                // Pause during overrun state
                _overrunPauseStartedAtUtc = DateTime.UtcNow;
                // State remains Overrun but ticking stops
                _timer.Stop();
            }
        }

        /// <summary>
        /// Resumes a paused session.
        /// </summary>
        public void Resume()
        {
            if (CurrentState == TimerState.Paused)
            {
                // Calculate paused duration and add to total
                var now = DateTime.UtcNow;
                _totalPausedSeconds += (now - _pauseStartedAtUtc).TotalSeconds;
                _timer.Start();
                CurrentState = TimerState.Running;
            }
            else if (CurrentState == TimerState.Overrun)
            {
                // Resume from paused overrun
                var now = DateTime.UtcNow;
                _totalOverrunPausedSeconds += (now - _overrunPauseStartedAtUtc).TotalSeconds;
                _timer.Start();
            }
        }

        /// <summary>
        /// Cancels the current session and returns to idle state.
        /// </summary>
        public void Cancel()
        {
            if (CurrentState == TimerState.Running || CurrentState == TimerState.Paused ||
                CurrentState == TimerState.Overrun || CurrentState == TimerState.Completed)
            {
                _timer.Stop();
                ResetState();
            }
        }

        #endregion Public Methods

        #region Private Methods

        /// <summary>
        /// Calculates the remaining time display text based on current state and timing.
        /// </summary>
        /// <returns>Formatted time string (MM:SS or -M:SS for overrun).</returns>
        private string CalculateDisplayText()
        {
            if (CurrentState == TimerState.Idle)
            {
                return "Ready";
            }

            // Calculate actual elapsed time accounting for pauses
            var now = DateTime.UtcNow;
            var elapsedSeconds = (now - _sessionStartedAtUtc).TotalSeconds - _totalPausedSeconds - _totalOverrunPausedSeconds;
            var remainingSeconds = _plannedDurationSeconds - elapsedSeconds;

            if (CurrentState == TimerState.Completed)
            {
                return "00:00";
            }

            // Format display text
            var absoluteValue = Math.Abs((int)remainingSeconds);
            var minutes = absoluteValue / 60;
            var seconds = absoluteValue % 60;

            if (remainingSeconds < 0 && CurrentState != TimerState.Completed)
            {
                return $"-{minutes}:{seconds:00}";
            }

            return $"{minutes:00}:{seconds:00}";
        }

        /// <summary>
        /// Called on each timer tick (every 250ms).
        /// Updates display and handles state transitions.
        /// </summary>
        private void OnTimerTick(object? sender, EventArgs e)
        {
            var now = DateTime.UtcNow;
            var elapsedSeconds = (now - _sessionStartedAtUtc).TotalSeconds - _totalPausedSeconds - _totalOverrunPausedSeconds;
            var remainingSeconds = _plannedDurationSeconds - elapsedSeconds;

            // Trigger display update
            OnPropertyChanged(nameof(DisplayText));

            // Check for completion or overrun state transition
            if (CurrentState == TimerState.Running && remainingSeconds <= 0)
            {
                // Fire sound notification
                _soundService.PlaySelectedSound();

                if (CurrentState != TimerState.Overrun)
                {
                    // Transition to Overrun state and continue counting into negative
                    CurrentState = TimerState.Overrun;
                }
            }
        }

        /// <summary>
        /// Marks the session as completed and records it to the database.
        /// </summary>
        public void MarkAsCompleted()
        {
            if (CurrentState == TimerState.Running || CurrentState == TimerState.Paused)
            {
                _timer.Stop();

                // Calculate final metrics
                var now = DateTime.UtcNow;
                var actualDurationSeconds = (int)(_plannedDurationSeconds - _totalPausedSeconds);
                var overrunSeconds = 0; // No overrun in normal completion

                // Record session to database asynchronously
                _sessionService.RecordSessionAsync(CreatePomodoroSession(now, actualDurationSeconds, overrunSeconds));

                CurrentState = TimerState.Completed;
            }
        }

        /// <summary>
        /// Resets the timer engine to initial idle state.
        /// </summary>
        private void ResetState()
        {
            _timer.Stop();
            CurrentState = TimerState.Idle;
            SessionType = SessionType.Work;
            _pomodoroCountInCycle = 0;
            ShowActionButtons = true;
        }

        /// <summary>
        /// Gets the planned duration in seconds for a given session type from settings.
        /// </summary>
        private int GetDurationForSessionType(SessionType sessionType)
        {
            return sessionType switch
            {
                SessionType.Work => _settingsService.WorkDurationMinutes * 60,
                SessionType.ShortBreak => _settingsService.ShortBreakDurationMinutes * 60,
                SessionType.LongBreak => _settingsService.LongBreakDurationMinutes * 60,
                _ => 25 * 60 // Fallback to 25 minutes
            };
        }

        /// <summary>
        /// Creates a PomodoroSession object with the calculated timing data.
        /// </summary>
        private Models.PomodoroSession CreatePomodoroSession(DateTime completedAt, int actualDuration, int overrun)
        {
            var now = DateTime.UtcNow;
            _completedAtUtc = now;

            return new Models.PomodoroSession
            {
                SessionType = _currentSessionType.ToString(),
                StartedAtUtc = _sessionStartedAtUtc,
                CompletedAtUtc = _completedAt,
                PlannedDurationSeconds = _plannedDurationSeconds,
                ActualDurationSeconds = actualDuration,
                OverrunSeconds = overrun,
                PausedDurationSeconds = (int)_totalPausedSeconds,
                CompletedNormally = true,
                PomodoroNumber = (_currentSessionType == SessionType.Work) ? _pomodoroCountInCycle : null
            };
        }

        #endregion Private Methods
    }
}