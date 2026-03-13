using System;
using System.Windows;
using System.Windows.Controls;
using TekTomato.ViewModels;

namespace TekTomato.Views
{
    /// <summary>
    /// Interaction logic for the main overlay window displaying the Pomodoro timer.
    /// </summary>
    public partial class OverlayWindow : Window
    {
        #region Private Fields

        private readonly OverlayViewModel _viewModel;

        #endregion Private Fields

        #region Constructor

        /// <summary>
        /// Initialises the OverlayWindow with the associated ViewModel.
        /// Sets up window initialisation, bindings, and positioning.
        /// </summary>
        public OverlayWindow(OverlayViewModel viewModel)
        {
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));

            InitializeComponent();
            
            // Set DataContext for MVVM bindings
            DataContext = _viewModel;

            // Apply settings-based initialisation
            OnInitialised();
        }

        #endregion Constructor

        #region Private Methods

        /// <summary>
        /// Initialises the window after component construction is complete.
        /// Sets up visibility states and retrieves saved position from SettingsService.
        /// </summary>
        private void OnInitialised()
        {
            // Apply visibility based on initial state
            UpdateButtonVisibility();

            // Retrieve saved window position if available (from SettingsService)
            var x = _viewModel.BackgroundOpacity; // Using BackgroundOpacity as proxy for now
            // Note: Position retrieval should come from SettingsService via ViewModel
            
            // Show without activation (prevents stealing focus from other apps)
            Show();

            // Set initial window location to center of screen if no saved position
            var screenWidth = SystemParameters.PrimaryScreenWidth;
            var screenHeight = SystemParameters.PrimaryScreenHeight;
            Left = (screenWidth - Width) / 2;
            Top = (screenHeight - Height) / 3;
        }

        /// <summary>
        /// Updates button visibility based on current timer state.
        /// Shows action buttons in Idle/Completed states, shows completion buttons during Overrun.
        /// </summary>
        private void UpdateButtonVisibility()
        {
            // Action buttons panel visibility bound to ShowActionButtons property
            ActionButtonsPanel.Visibility = _viewModel.ShowActionButtons ? Visibility.Visible : Visibility.Collapsed;

            // Completion action buttons visibility for overrun state
            CompleteSessionButton.Visibility = (_viewModel.CurrentState == Services.TimerState.Overrun) 
                ? Visibility.Visible 
                : Visibility.Collapsed;

            CancelButton.Visibility = (_viewModel.CurrentState != Services.TimerState.Idle && 
                                      _viewModel.CurrentState != Services.TimerState.Completed)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        /// <summary>
        /// Handles the main action button click (Start/Pause/Resume).
        /// Routes to appropriate ViewModel command based on current state.
        /// </summary>
        private void OnMainActionClick(object sender, RoutedEventArgs e)
        {
            if (_viewModel.CurrentState == Services.TimerState.Idle || 
                _viewModel.CurrentState == Services.TimerState.Completed)
            {
                // Start first pomodoro by default when in idle state
                _viewModel.StartPomodoroCommand.Execute(null);
            }
            else if (_viewModel.CurrentState == Services.TimerState.Running ||
                     _viewModel.CurrentState == Services.TimerState.Overrun)
            {
                _viewModel.PauseCommand.Execute(null);
            }
            else if (_viewModel.CurrentState == Services.TimerState.Paused)
            {
                _viewModel.ResumeCommand.Execute(null);
            }

            UpdateButtonVisibility();
        }

        /// <summary>
        /// Handles complete session button click.
        /// Marks the current session as completed.
        /// </summary>
        private void OnCompleteSessionClick(object sender, RoutedEventArgs e)
        {
            _viewModel.CompleteSessionCommand.Execute(null);
            UpdateButtonVisibility();
        }

        /// <summary>
        /// Handles cancel session button click.
        /// Cancels the current running session and returns to idle state.
        /// </summary>
        private void OnCancelSessionClick(object sender, RoutedEventArgs e)
        {
            _viewModel.CancelSessionCommand.Execute(null);
            UpdateButtonVisibility();
        }

        /// <summary>
        /// Handles start Pomodoro button click.
        /// Initiates a work session.
        /// </summary>
        private void OnStartPomodoroClick(object sender, RoutedEventArgs e)
        {
            _viewModel.StartPomodoroCommand.Execute(null);
            UpdateButtonVisibility();
        }

        /// <summary>
        /// Handles start short break button click.
        /// Initiates a short break session.
        /// </summary>
        private void OnStartShortBreakClick(object sender, RoutedEventArgs e)
        {
            _viewModel.StartShortBreakCommand.Execute(null);
            UpdateButtonVisibility();
        }

        /// <summary>
        /// Handles start long break button click.
        /// Initiates a long break session.
        /// </summary>
        private void OnStartLongBreakClick(object sender, RoutedEventArgs e)
        {
            _viewModel.StartLongBreakCommand.Execute(null);
            UpdateButtonVisibility();
        }

        /// <summary>
        /// Handles settings icon button click.
        /// Opens the Settings window.
        /// </summary>
        private void OnSettingsIconClick(object sender, RoutedEventArgs e)
        {
            _viewModel.OpenSettingsCommand.Execute(null);
        }

        /// <summary>
        /// Handles statistics icon button click.
        /// Opens the Statistics window.
        /// </summary>
        private void OnStatsIconClick(object sender, RoutedEventArgs e)
        {
            _viewModel.OpenStatsCommand.Execute(null);
        }

        /// <summary>
        /// Handles mouse down event to enable window dragging.
        /// Allows the overlay window to be repositioned by dragging anywhere on the window.
        /// </summary>
        private void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            // Start drag operation when left mouse button is pressed
            if (e.ChangedButton == MouseButton.Left)
            {
                DragMove();
            }
        }

        /// <summary>
        /// Handles window closing event to minimise to system tray instead of exiting.
        /// Prevents the overlay from disappearing on X button click.
        /// </summary>
        private void OnWindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Cancel the default close operation
            e.Cancel = true;

            // Minimise to system tray (hide window)
            Hide();

            // Note: System tray icon functionality would be implemented via H.NotifyIcon in App.xaml.cs
            // For now, we simply hide the window
        }

        #endregion Private Methods
    }
}