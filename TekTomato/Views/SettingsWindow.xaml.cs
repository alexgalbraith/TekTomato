using System;
using System.Windows;
using TekTomato.ViewModels;

namespace TekTomato.Views
{
    /// <summary>
    /// Interaction logic for the Settings modal window.
    /// Displays application configuration options for timer durations, sounds, and appearance.
    /// </summary>
    public partial class SettingsWindow : Window
    {
        #region Private Fields

        private readonly SettingsViewModel _viewModel;

        #endregion Private Fields

        #region Constructor

        /// <summary>
        /// Initialises the SettingsWindow with the associated ViewModel.
        /// Sets up bindings and initialises UI state.
        /// </summary>
        public SettingsWindow(SettingsViewModel viewModel)
        {
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));

            InitializeComponent();
            
            // Set DataContext for MVVM bindings
            DataContext = _viewModel;

            // Apply opacity label update logic
            OpacitySlider.ValueChanged += OnOpacitySliderValueChanged;
        }

        #endregion Constructor

        #region Event Handlers

        /// <summary>
        /// Handles opacity slider value changes to update the display label.
        /// </summary>
        private void OnOpacitySliderValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (sender is Slider slider)
            {
                OpacityLabel.Text = $"{(int)slider.Value}%";
            }
        }

        /// <summary>
        /// Handles reset to defaults button click.
        /// </summary>
        private void OnResetDefaultsClick(object sender, RoutedEventArgs e)
        {
            if (_viewModel.ResetToDefaultsCommand.CanExecute(null))
            {
                _viewModel.ResetToDefaultsCommand.Execute(null);
            }
        }

        #endregion Event Handlers

        #region Public Methods

        /// <summary>
        /// Shows the settings window as a modal dialog.
        /// </summary>
        public void ShowModal()
        {
            // Show window (caller handles whether to use .Show() or .ShowDialog())
            Show();
        }

        #endregion Public Methods
    }
}