using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TekTomato.Services;

namespace TekTomato.ViewModels
{
    /// <summary>
    /// View model for the Settings window.
    /// Manages application configuration including timer durations, sounds, and themes.
    /// </summary>
    public partial class SettingsViewModel : ObservableObject
    {
        #region Private Fields

        private readonly SettingsService _settingsService;
        private readonly SoundService _soundService;
        private readonly ThemeService _themeService;

        // Original values for potential rollback
        private int _originalWorkDurationMinutes;
        private int _originalShortBreakMinutes;
        private int _originalLongBreakMinutes;
        private int _originalLongBreakInterval;
        private string _originalSelectedSound;
        private ThemeType _originalSelectedTheme;
        private int _originalBackgroundOpacityPercent;

        #endregion Private Fields

        #region Observable Properties

        /// <summary>
        /// Gets or sets the work session duration in minutes.
        /// </summary>
        [ObservableProperty]
        private int _workDurationMinutes = 25;

        /// <summary>
        /// Gets or sets the short break duration in minutes.
        /// </summary>
        [ObservableProperty]
        private int _shortBreakMinutes = 5;

        /// <summary>
        /// Gets or sets the long break duration in minutes.
        /// </summary>
        [ObservableProperty]
        private int _longBreakMinutes = 15;

        /// <summary>
        /// Gets or sets the number of work sessions before a long break.
        /// </summary>
        [ObservableProperty]
        private int _longBreakInterval = 4;

        /// <summary>
        /// Gets or sets the selected sound file name for timer notifications.
        /// </summary>
        [ObservableProperty]
        private string _selectedSound = "Windows Notify System Generic.wav";

        /// <summary>
        /// Gets or sets the selected theme type as a display string.
        /// </summary>
        [ObservableProperty]
        private string _selectedTheme = "System Auto";

        /// <summary>
        /// Gets or sets the background opacity percentage for the overlay window.
        /// Range: 0-100 where 0 is fully transparent and 100 is fully opaque.
        /// </summary>
        [ObservableProperty]
        private int _backgroundOpacityPercent = 80;

        /// <summary>
        /// Gets the collection of available sound files from the Windows Media directory.
        /// Includes "No sound" as the first option.
        /// </summary>
        public ObservableCollection<string> AvailableSounds { get; } = new();

        /// <summary>
        /// Gets the collection of available theme options.
        /// </summary>
        public static readonly string[] AvailableThemes = new string[]
        {
            "System Auto",
            "Dark",
            "Light"
        };

        /// <summary>
        /// Gets a value indicating whether there are unsaved changes.
        /// </summary>
        [ObservableProperty]
        private bool _hasUnsavedChanges = false;

        #endregion Observable Properties

        #region Commands

        /// <summary>
        /// Command to save all settings to persistent storage.
        /// Updates SettingsService properties and triggers theme application.
        /// </summary>
        [RelayCommand]
        private async void Save()
        {
            try
            {
                // Update settings service with new values
                _settingsService.WorkDurationMinutes = WorkDurationMinutes;
                _settingsService.ShortBreakDurationMinutes = ShortBreakMinutes;
                _settingsService.LongBreakDurationMinutes = LongBreakMinutes;
                _settingsService.LongBreakIntervalPomodoros = LongBreakInterval;
                _settingsService.SoundFileName = SelectedSound == "No sound" ? string.Empty : SelectedSound;
                _settingsService.BackgroundOpacityPercent = BackgroundOpacityPercent;

                // Handle theme conversion from string to enum
                if (Enum.TryParse<ThemeType>(SelectedTheme, true, out var themeType))
                {
                    _themeService.ApplyTheme(themeType);
                }

                // Persist to database
                await _settingsService.SaveAsync();

                HasUnsavedChanges = false;
            }
            catch (Exception ex)
            {
                // Log error or show dialog - for now swallow with debug output
                System.Diagnostics.Debug.WriteLine($"Error saving settings: {ex.Message}");
            }
        }

        /// <summary>
        /// Command to cancel changes and close the settings window without saving.
        /// Restores original values from before changes were made.
        /// </summary>
        [RelayCommand]
        private void Cancel()
        {
            // Restore original values
            WorkDurationMinutes = _originalWorkDurationMinutes;
            ShortBreakMinutes = _originalShortBreakMinutes;
            LongBreakMinutes = _originalLongBreakMinutes;
            LongBreakInterval = _originalLongBreakInterval;
            SelectedSound = _originalSelectedSound;
            SelectedTheme = _originalSelectedTheme.ToString();
            BackgroundOpacityPercent = _originalBackgroundOpacityPercent;

            HasUnsavedChanges = false;
        }

        /// <summary>
        /// Command to reset all settings to default values.
        /// </summary>
        [RelayCommand]
        private async void ResetToDefaults()
        {
            WorkDurationMinutes = 25;
            ShortBreakMinutes = 5;
            LongBreakMinutes = 15;
            LongBreakInterval = 4;
            SelectedSound = "Windows Notify System Generic.wav";
            SelectedTheme = "System Auto";
            BackgroundOpacityPercent = 80;

            HasUnsavedChanges = true;

            // Note: This just updates UI values, actual save happens on Save command
        }

        /// <summary>
        /// Command to test the currently selected sound file.
        /// </summary>
        [RelayCommand]
        private async void TestSound()
        {
            if (SelectedSound != "No sound")
            {
                await _soundService.PlaySelectedSound();
            }
        }

        #endregion Commands

        #region Constructor

        /// <summary>
        /// Initialises the SettingsViewModel with required services.
        /// Loads current settings from SettingsService and populates AvailableSounds collection.
        /// </summary>
        public SettingsViewModel(
            SettingsService settingsService,
            SoundService soundService,
            ThemeService themeService)
        {
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _soundService = soundService ?? throw new ArgumentNullException(nameof(soundService));
            _themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));

            // Store original values for potential rollback
            _originalWorkDurationMinutes = settingsService.WorkDurationMinutes;
            _originalShortBreakMinutes = settingsService.ShortBreakDurationMinutes;
            _originalLongBreakMinutes = settingsService.LongBreakDurationMinutes;
            _originalLongBreakInterval = settingsService.LongBreakIntervalPomodoros;
            _originalSelectedSound = settingsService.SoundFileName;
            _originalSelectedTheme = settingsService.Theme;
            _originalBackgroundOpacityPercent = settingsService.BackgroundOpacityPercent;

            // Load current settings into properties
            LoadCurrentSettings();

            // Populate available sounds list
            LoadAvailableSounds();
        }

        #endregion Constructor

        #region Private Methods

        /// <summary>
        /// Loads current settings from SettingsService into view model properties.
        /// </summary>
        private void LoadCurrentSettings()
        {
            WorkDurationMinutes = _settingsService.WorkDurationMinutes;
            ShortBreakMinutes = _settingsService.ShortBreakDurationMinutes;
            LongBreakMinutes = _settingsService.LongBreakDurationMinutes;
            LongBreakInterval = _settingsService.LongBreakIntervalPomodoros;
            BackgroundOpacityPercent = _settingsService.BackgroundOpacityPercent;

            // Convert sound filename to display value
            var soundFile = _settingsService.SoundFileName;
            SelectedSound = string.IsNullOrEmpty(soundFile) ? "No sound" : soundFile;

            // Convert theme enum to string
            SelectedTheme = _settingsService.Theme.ToString();
        }

        /// <summary>
        /// Populates the AvailableSounds collection from SoundService.
        /// Adds "No sound" as the first option, followed by available WAV files.
        /// </summary>
        private void LoadAvailableSounds()
        {
            AvailableSounds.Clear();
            AvailableSounds.Add("No sound");

            foreach (var sound in _soundService.GetAvailableSounds())
            {
                AvailableSounds.Add(sound);
            }
        }

        #endregion Private Methods
    }
}