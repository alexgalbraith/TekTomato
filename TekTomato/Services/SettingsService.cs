using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using CommunityToolkit.Mvvm.ComponentModel;
using TekTomato.Data;
using TekTomato.Models;

namespace TekTomato.Services
{
    /// <summary>
    /// Manages application settings with database persistence and in-memory caching.
    /// Provides strongly-typed access to configuration values.
    /// </summary>
    public partial class SettingsService : ObservableObject
    {
        #region Private Fields

        private readonly TekTomatoDbContext _dbContext;
        private Dictionary<string, string> _settingsCache = new();

        // Default values as per architecture reference (Section 3)
        private const int DEFAULT_WORK_DURATION_MINUTES = 25;
        private const int DEFAULT_SHORT_BREAK_DURATION_MINUTES = 5;
        private const int DEFAULT_LONG_BREAK_DURATION_MINUTES = 15;
        private const int DEFAULT_LONG_BREAK_INTERVAL_POMODOROS = 4;
        private const string DEFAULT_SOUND_FILE_NAME = "Windows Notify System Generic.wav";
        private const ThemeType DEFAULT_THEME = ThemeType.SystemAuto;
        private const int DEFAULT_BACKGROUND_OPACITY_PERCENT = 80;
        private int? DEFAULT_OVERLAY_POSITION_X = null;
        private int? DEFAULT_OVERLAY_POSITION_Y = null;

        #endregion Private Fields

        #region Constructor

        /// <summary>
        /// Initialises the settings service with database context.
        /// </summary>
        public SettingsService(TekTomatoDbContext dbContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        /// <summary>
        /// Loads settings from database asynchronously on construction.
        /// </summary>
        public async Task InitializeAsync()
        {
            await LoadSettingsAsync();
        }

        #endregion Constructor

        #region Settings Properties (Observable)

        /// <summary>
        /// Gets or sets the work duration in minutes.
        /// </summary>
        public int WorkDurationMinutes
        {
            get => GetIntSetting("WorkDurationMinutes", DEFAULT_WORK_DURATION_MINUTES);
            set => SetSetting("WorkDurationMinutes", value.ToString());
        }

        /// <summary>
        /// Gets or sets the short break duration in minutes.
        /// </summary>
        public int ShortBreakDurationMinutes
        {
            get => GetIntSetting("ShortBreakDurationMinutes", DEFAULT_SHORT_BREAK_DURATION_MINUTES);
            set => SetSetting("ShortBreakDurationMinutes", value.ToString());
        }

        /// <summary>
        /// Gets or sets the long break duration in minutes.
        /// </summary>
        public int LongBreakDurationMinutes
        {
            get => GetIntSetting("LongBreakDurationMinutes", DEFAULT_LONG_BREAK_DURATION_MINUTES);
            set => SetSetting("LongBreakDurationMinutes", value.ToString());
        }

        /// <summary>
        /// Gets or sets the number of pomodoros before a long break.
        /// </summary>
        public int LongBreakIntervalPomodoros
        {
            get => GetIntSetting("LongBreakIntervalPomodoros", DEFAULT_LONG_BREAK_INTERVAL_POMODOROS);
            set => SetSetting("LongBreakIntervalPomodoros", value.ToString());
        }

        /// <summary>
        /// Gets or sets the sound file name for timer completion.
        /// </summary>
        public string SoundFileName
        {
            get => GetStringSetting("SoundFileName", DEFAULT_SOUND_FILE_NAME);
            set => SetSetting("SoundFileName", value);
        }

        /// <summary>
        /// Gets or sets the application theme type.
        /// </summary>
        public ThemeType Theme
        {
            get => GetEnumSetting<ThemeType>("Theme", DEFAULT_THEME);
            set => SetSetting("Theme", value.ToString());
        }

        /// <summary>
        /// Gets or sets the background opacity percentage.
        /// </summary>
        public int BackgroundOpacityPercent
        {
            get => GetIntSetting("BackgroundOpacityPercent", DEFAULT_BACKGROUND_OPACITY_PERCENT);
            set => SetSetting("BackgroundOpacityPercent", value.ToString());
        }

        /// <summary>
        /// Gets or sets the X position for overlay window (null means auto-positioned).
        /// </summary>
        public int? OverlayPositionX
        {
            get => GetNullableIntSetting("OverlayPositionX", DEFAULT_OVERLAY_POSITION_X);
            set => SetSetting("OverlayPositionX", value?.ToString() ?? string.Empty);
        }

        /// <summary>
        /// Gets or sets the Y position for overlay window (null means auto-positioned).
        /// </summary>
        public int? OverlayPositionY
        {
            get => GetNullableIntSetting("OverlayPositionY", DEFAULT_OVERLAY_POSITION_Y);
            set => SetSetting("OverlayPositionY", value?.ToString() ?? string.Empty);
        }

        #endregion Settings Properties (Observable)

        #region Public Methods

        /// <summary>
        /// Saves all current settings to the database.
        /// </summary>
        public async Task SaveAsync()
        {
            foreach (var setting in GetPersistableSettings())
            {
                await UpsertSettingAsync(setting.Key, setting.Value);
            }

            // Refresh cache from DB to ensure consistency
            await LoadSettingsAsync();
        }

        /// <summary>
        /// Resets all settings to default values.
        /// </summary>
        public async Task ResetToDefaultsAsync()
        {
            // Clear existing settings in cache
            _settingsCache.Clear();

            // Set each property to its default (triggers Save via setter)
            WorkDurationMinutes = DEFAULT_WORK_DURATION_MINUTES;
            ShortBreakDurationMinutes = DEFAULT_SHORT_BREAK_DURATION_MINUTES;
            LongBreakDurationMinutes = DEFAULT_LONG_BREAK_DURATION_MINUTES;
            LongBreakIntervalPomodoros = DEFAULT_LONG_BREAK_INTERVAL_POMODOROS;
            SoundFileName = DEFAULT_SOUND_FILE_NAME;
            Theme = DEFAULT_THEME;
            BackgroundOpacityPercent = DEFAULT_BACKGROUND_OPACITY_PERCENT;
            OverlayPositionX = DEFAULT_OVERLAY_POSITION_X;
            OverlayPositionY = DEFAULT_OVERLAY_POSITION_Y;

            await SaveAsync();
        }

        #endregion Public Methods

        #region Private Methods

        /// <summary>
        /// Loads all settings from the database into memory cache.
        /// </summary>
        private async Task LoadSettingsAsync()
        {
            try
            {
                var settings = await _dbContext.Settings.ToListAsync();
                
                // Clear and rebuild cache
                _settingsCache.Clear();
                
                foreach (var setting in settings)
                {
                    if (!string.IsNullOrEmpty(setting.Key))
                    {
                        _settingsCache[setting.Key] = setting.Value;
                    }
                }
            }
            catch (Exception ex)
            {
                // Log error but don't crash - use defaults instead
                System.Diagnostics.Debug.WriteLine($"Error loading settings: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets a string setting from cache with fallback to default.
        /// </summary>
        private string GetStringSetting(string key, string defaultValue)
        {
            return _settingsCache.TryGetValue(key, out var value) && !string.IsNullOrEmpty(value) 
                ? value 
                : defaultValue;
        }

        /// <summary>
        /// Gets an integer setting from cache with fallback to default.
        /// </summary>
        private int GetIntSetting(string key, int defaultValue)
        {
            if (_settingsCache.TryGetValue(key, out var value) && !string.IsNullOrEmpty(value))
            {
                return int.TryParse(value, out var result) ? result : defaultValue;
            }

            return defaultValue;
        }

        /// <summary>
        /// Gets a nullable integer setting from cache with fallback to default.
        /// </summary>
        private int? GetNullableIntSetting(string key, int? defaultValue)
        {
            if (_settingsCache.TryGetValue(key, out var value) && !string.IsNullOrEmpty(value))
            {
                return int.TryParse(value, out var result) ? (int?)result : defaultValue;
            }

            return defaultValue;
        }

        /// <summary>
        /// Gets an enum setting from cache with fallback to default.
        /// </summary>
        private T GetEnumSetting<T>(string key, T defaultValue) where T : Enum
        {
            if (_settingsCache.TryGetValue(key, out var value) && !string.IsNullOrEmpty(value))
            {
                return Enum.TryParse<T>(value, out var result) ? result : defaultValue;
            }

            return defaultValue;
        }

        /// <summary>
        /// Sets a setting value in cache and triggers property change notification.
        /// </summary>
        private void SetSetting(string key, string value)
        {
            var oldValue = _settingsCache.TryGetValue(key, out var existingValue) ? existingValue : null;
            _settingsCache[key] = value;

            // Notify UI that settings may have changed (all properties depend on cache)
            OnPropertyChanged(nameof(WorkDurationMinutes));
            OnPropertyChanged(nameof(ShortBreakDurationMinutes));
            OnPropertyChanged(nameof(LongBreakDurationMinutes));
            OnPropertyChanged(nameof(LongBreakIntervalPomodoros));
            OnPropertyChanged(nameof(SoundFileName));
            OnPropertyChanged(nameof(Theme));
            OnPropertyChanged(nameof(BackgroundOpacityPercent));
            OnPropertyChanged(nameof(OverlayPositionX));
            OnPropertyChanged(nameof(OverlayPositionY));
        }

        /// <summary>
        /// Upserts a setting record in the database.
        /// </summary>
        private async Task UpsertSettingAsync(string key, string value)
        {
            var existing = await _dbContext.Settings.FindAsync(key);

            if (existing != null)
            {
                existing.Value = value;
                _dbContext.Settings.Update(existing);
            }
            else
            {
                var newSetting = new Setting { Key = key, Value = value };
                _dbContext.Settings.Add(newSetting);
            }

            await _dbContext.SaveChangesAsync();
        }

        /// <summary>
        /// Gets all settings that should be persisted to database.
        /// </summary>
        private List<(string Key, string Value)> GetPersistableSettings()
        {
            var persistable = new List<(string Key, string Value)>
            {
                ("WorkDurationMinutes", WorkDurationMinutes.ToString()),
                ("ShortBreakDurationMinutes", ShortBreakDurationMinutes.ToString()),
                ("LongBreakDurationMinutes", LongBreakDurationMinutes.ToString()),
                ("LongBreakIntervalPomodoros", LongBreakIntervalPomodoros.ToString()),
                ("SoundFileName", SoundFileName),
                ("Theme", Theme.ToString()),
                ("BackgroundOpacityPercent", BackgroundOpacityPercent.ToString()),
                ("OverlayPositionX", OverlayPositionX?.ToString() ?? string.Empty),
                ("OverlayPositionY", OverlayPositionY?.ToString() ?? string.Empty)
            };

            return persistable;
        }

        #endregion Private Methods
    }
}