using System;
using Microsoft.Win32;
using System.Windows;
using TekTomato.ViewModels;

namespace TekTomato.Services
{
    /// <summary>
    /// Represents the application theme type.
    /// </summary>
    public enum ThemeType
    {
        Dark,
        Light,
        SystemAuto
    }

    /// <summary>
    /// Manages theme switching and system auto-detection for light/dark mode.
    /// </summary>
    public class ThemeService
    {
        #region Private Fields

        private readonly SettingsService _settingsService;
        private ThemeType _currentTheme = ThemeType.SystemAuto;

        #endregion Private Fields

        #region Properties

        /// <summary>
        /// Gets or sets the current theme type.
        /// </summary>
        public ThemeType CurrentTheme
        {
            get => _currentTheme;
            private set => _currentTheme = value;
        }

        #endregion Properties

        #region Constructor

        /// <summary>
        /// Initialises the theme service with settings dependency.
        /// </summary>
        public ThemeService(SettingsService settingsService)
        {
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            
            // Apply initial theme from settings
            CurrentTheme = _settingsService.Theme;
        }

        #endregion Constructor

        #region Public Methods

        /// <summary>
        /// Applies the specified theme to the application.
        /// Handles SystemAuto detection and swaps resource dictionaries.
        /// </summary>
        /// <param name="themeType">The theme type to apply.</param>
        public void ApplyTheme(ThemeType themeType)
        {
            CurrentTheme = themeType;

            // Determine actual theme based on system settings if SystemAuto
            var actualThemePath = GetThemeResourcePath(themeType);
            
            // Swap resource dictionaries in Application.Resources.MergedDictionaries
            SwapResourceDictionaries(actualThemePath);

            // Persist to settings
            _settingsService.Theme = themeType;
        }

        /// <summary>
        /// Applies the current theme from settings (convenience method).
        /// </summary>
        public void ApplyCurrentTheme()
        {
            var themeType = _settingsService.Theme;
            CurrentTheme = themeType;
            
            // Determine actual theme based on system settings if SystemAuto
            var actualThemePath = GetThemeResourcePath(themeType);
            
            // Swap resource dictionaries in Application.Resources.MergedDictionaries
            SwapResourceDictionaries(actualThemePath);
        }

        #endregion Public Methods

        #region Private Methods

        /// <summary>
        /// Gets the resource dictionary path for a given theme type.
        /// Handles SystemAuto detection by reading Windows registry.
        /// </summary>
        private string GetThemeResourcePath(ThemeType themeType)
        {
            if (themeType == ThemeType.SystemAuto)
            {
                // Detect system theme preference via registry
                var isLight = IsSystemUsingLightTheme();
                
                return isLight 
                    ? "Resources/Themes/Light.xaml" 
                    : "Resources/Themes/Dark.xaml";
            }

            return themeType == ThemeType.Light 
                ? "Resources/Themes/Light.xaml" 
                : "Resources/Themes/Dark.xaml";
        }

        /// <summary>
        /// Detects if the system is currently using light theme by reading Windows registry.
        /// Reads HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize\AppsUseLightTheme
        /// </summary>
        private bool IsSystemUsingLightTheme()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", false);

                if (key != null)
                {
                    var value = key.GetValue("AppsUseLightTheme");
                    
                    // AppsUseLightTheme: 1 = Light theme, 0 = Dark theme
                    return value is int intValue && intValue == 1;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error detecting system theme: {ex.Message}");
            }

            // Default to dark if detection fails
            return false;
        }

        /// <summary>
        /// Swaps the theme resource dictionaries in the application resources.
        /// Removes existing theme dictionary and adds the new one.
        /// </summary>
        private void SwapResourceDictionaries(string themePath)
        {
            var app = Application.Current;
            if (app == null || app.Resources.MergedDictionaries == null)
            {
                System.Diagnostics.Debug.WriteLine("Application or MergedDictionaries is null - cannot swap theme");
                return;
            }

            // Remove existing theme dictionaries (Dark.xaml and Light.xaml, but keep Common.xaml)
            var mergedDictionaries = app.Resources.MergedDictionaries;
            
            for (int i = mergedDictionaries.Count - 1; i >= 0; i--)
            {
                var source = mergedDictionaries[i].Source;
                
                if (!string.IsNullOrEmpty(source.OriginalString))
                {
                    var originalPath = source.OriginalString;
                    
                    // Keep Common.xaml, remove theme-specific dictionaries
                    if (originalPath.Contains("Themes/Dark.xaml") || 
                        originalPath.Contains("Themes/Light.xaml"))
                    {
                        mergedDictionaries.RemoveAt(i);
                    }
                }
            }

            // Add the new theme dictionary
            var newThemeDictionary = new ResourceDictionary
            {
                Source = new Uri(themePath, UriKind.Relative)
            };

            mergedDictionaries.Add(newThemeDictionary);
        }

        #endregion Private Methods
    }
}