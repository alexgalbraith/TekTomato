using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media.Imaging;
using SkiaSharp;
using SkiaSharp.Views.WPF;
using TekTomato.ViewModels;

namespace TekTomato.Views
{
    /// <summary>
    /// Interaction logic for the Statistics window displaying Pomodoro analytics.
    /// Contains heatmap canvas and line chart visualisation of focus data.
    /// </summary>
    public partial class StatsWindow : Window
    {
        #region Private Fields

        private readonly StatsViewModel _viewModel;

        #endregion Private Fields

        #region Constructor

        /// <summary>
        /// Initialises the StatsWindow with the associated ViewModel.
        /// Sets up data binding and initialises chart rendering components.
        /// </summary>
        public StatsWindow(StatsViewModel viewModel)
        {
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));

            InitializeComponent();
            
            // Set DataContext for MVVM bindings
            DataContext = _viewModel;

            // Wait for VM to load data before initialising chart
            Loaded += OnWindowLoaded;
        }

        #endregion Constructor

        #region Event Handlers

        /// <summary>
        /// Handles window loaded event to trigger initial data refresh.
        /// </summary>
        private async void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            // Give VM time to load initial data
            await Task.Delay(100);

            // Trigger heatmap repaint after data is loaded
            HeatmapCanvas.InvalidateSurface();
        }

        /// <summary>
        /// Handles date range combo box selection changes.
        /// </summary>
        private void OnDateRangeChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            // Data loading is handled by the View Model's SelectedRange binding
            // This event handler ensures UI updates properly
            if (_viewModel.RefreshCommand.CanExecute(null))
            {
                Task.Run(async () => await _viewModel.RefreshCommand.ExecuteAsync(null));
                
                // Refresh heatmap canvas after data update
                Dispatcher.BeginInvoke(new Action(() => HeatmapCanvas.InvalidateSurface()));
            }
        }

        /// <summary>
        /// Handles SKElement paint surface event for rendering the activity heatmap.
        /// Delegates to HeatmapRenderer helper method with current ViewModel data.
        /// </summary>
        private void OnHeatmapPaintSurface(object sender, SKPaintSurfaceEventArgs e)
        {
            var info = e.Info;
            using (var canvas = e.Surface.Canvas)
            {
                // Clear the canvas with background colour
                canvas.Clear(SKColors.Transparent);

                // Render heatmap using helper method
                HeatmapRenderer.RenderHeatmap(canvas, info, _viewModel.HeatmapData);
            }
        }

        #endregion Event Handlers
    }

    /// <summary>
    /// Static helper class for rendering activity heatmap visualisation.
    /// </summary>
    public static class HeatmapRenderer
    {
        #region Private Constants

        private const int CELL_PADDING = 2;
        private const int LABEL_SIZE = 12;
        
        // Colour palette for activity levels (0-5+ pomodoros per day)
        private static readonly SKColor[] ActivityColours = new SKColor[]
        {
            SKColors.White,        // No activity
            SKColor.Parse("#388E3C"),  // 1-2 pomodoros
            SKColor.Parse("#66BB6A"),  // 3 pomodoros
            SKColor.Parse("#81C784"),  // 4 pomodoros
            SKColor.Parse("#A5D6A7"),  // 5+ pomodoros
        };

        #endregion Private Constants

        /// <summary>
        /// Renders the activity heatmap on the provided canvas.
        /// Takes a dictionary of dates mapped to pomodoro counts and displays as calendar grid.
        /// </summary>
        /// <param name="canvas">The SkiaSharp canvas to render on.</param>
        /// <param name="info">Canvas surface information including size.</param>
        /// <param name="heatmapData">Dictionary mapping dates to pomodoro counts.</param>
        public static void RenderHeatmap(SKCanvas canvas, SKImageInfo info, Dictionary<DateOnly, int> heatmapData)
        {
            if (heatmapData == null || heatmapData.Count == 0)
            {
                // Draw empty state message
                using (var paint = new SKPaint
                {
                    TextSize = 24,
                    Color = SKColors.Gray,
                    TextAlign = SKTextAlign.Center
                })
                {
                    canvas.DrawText("No activity data available", 
                        info.Width / 2f, 
                        info.Height / 2f, 
                        paint);
                }
                return;
            }

            // Get date range for the heatmap display (show last 90 days)
            var endDate = DateOnly.FromDateTime(DateTime.UtcNow);
            var startDate = endDate.AddDays(-89);

            // Calculate grid dimensions
            const int weeksToShow = 13;
            const int daysPerWeek = 7;
            
            var totalCells = weeksToShow * daysPerWeek;
            
            // Calculate cell size based on available space minus labels
            var headerHeight = 25.0f;
            var labelWidth = 35.0f;
            var availableWidth = info.Width - labelWidth - 10;
            var availableHeight = info.Height - headerHeight - 10;
            
            var cellSize = Math.Min(availableWidth / daysPerWeek, availableHeight / weeksToShow) - CELL_PADDING * 2;

            // Create brushes and paints
            var backgroundBrush = new SKPaint { Color = SKColor.Parse("#2D2D2D") };
            var textPaint = new SKPaint 
            { 
                TextSize = LABEL_SIZE, 
                Color = SKColors.White,
                TextAlign = SKTextAlign.Center
            };

            // Draw grid header (week numbers or dates)
            for (int week = 0; week < weeksToShow; week++)
            {
                var yPos = 10 + headerHeight + week * (cellSize + CELL_PADDING);
                
                using (var rectPaint = new SKPaint 
                { 
                    Color = SKColor.Parse("#2D2D2D"),
                    Style = SKPaintStyle.Stroke,
                    StrokeWidth = 1.5f
                })
                {
                    var weekStart = endDate.AddDays(-week * 7);
                    canvas.DrawRect(new SKRect(10, yPos - CELL_PADDING, availableWidth + labelWidth, 
                        yPos + cellSize + CELL_PADDING), rectPaint);
                }

                // Draw week number/label on left
                using (var labelPaint = new SKPaint
                {
                    TextSize = LABEL_SIZE,
                    Color = SKColor.Parse("#999999"),
                    TextAlign = SKTextAlign.Right
                })
                {
                    canvas.DrawText($"W{week + 1}", 0, yPos + (cellSize - LABEL_SIZE) / 2f, labelPaint);
                }
            }

            // Render activity cells
            foreach (var kvp in heatmapData)
            {
                var date = kvp.Key;
                var count = kvp.Value;

                // Only render dates within display range
                if (date < startDate || date > endDate)
                    continue;

                // Calculate position relative to start date
                var daysFromEnd = (endDate - date).Days;
                if (daysFromEnd < 0 || daysFromEnd >= 91)
                    continue;

                var weekIndex = daysFromEnd / 7;
                var dayOfWeek = (int)date.DayOfWeek; // 0=Monday, 6=Sunday

                var xPos = 15 + labelWidth + dayOfWeek * (cellSize + CELL_PADDING);
                var yPos = 10 + headerHeight + weekIndex * (cellSize + CELL_PADDING);

                // Get colour based on activity level
                var colourIndex = Math.Min(count, ActivityColours.Length - 1);
                var cellColour = ActivityColours[colourIndex];

                // Draw cell rectangle with activity colour
                using (var cellPaint = new SKPaint 
                { 
                    Color = cellColour,
                    IsAntialias = true
                })
                {
                    canvas.DrawRoundRect(new SKRect(xPos, yPos, xPos + cellSize, yPos + cellSize),
                        4, 4, // Corner radius
                        cellPaint);
                }

                // Draw pomodoro count if applicable
                if (count > 0)
                {
                    using (var countPaint = new SKPaint
                    {
                        TextSize = 10,
                        Color = SKColors.White,
                        TextAlign = SKTextAlign.Center
                    })
                    {
                        canvas.DrawText(count.ToString(), 
                            xPos + cellSize / 2f, 
                            yPos + cellSize / 2f, 
                            countPaint);
                    }
                }
            }

            // Add legend at bottom
            DrawLegend(canvas, info.Width, info.Height);
        }

        /// <summary>
        /// Renders the activity level legend at the bottom of the heatmap canvas.
        /// </summary>
        private static void DrawLegend(SKCanvas canvas, float width, float height)
        {
            using (var paint = new SKPaint 
            { 
                TextSize = LABEL_SIZE, 
                Color = SKColor.Parse("#999999") 
            })
            {
                var legendY = height - 20;
                
                // Draw "No Activity" indicator
                canvas.DrawCircle(50f, legendY, 10, new SKPaint { Color = SKColors.White });
                canvas.DrawText("No Activity", 75f, legendY, paint);

                // Draw activity level indicators
                var xPos = 200f;
                for (int i = 1; i < ActivityColours.Length; i++)
                {
                    canvas.DrawRect(new SKRect(xPos, legendY - 10, xPos + 20, legendY),
                        new SKPaint { Color = ActivityColours[i] });
                    
                    var label = i == ActivityColours.Length - 1 ? "5+" : $"{i}+";
                    canvas.DrawText(label, xPos + 10f, legendY, paint);
                    
                    xPos += 35;
                }
            }
        }
    }

    /// <summary>
    /// Date range enumeration for statistics display.
    /// </summary>
    public enum SelectedRangeEnum
    {
        Last3Months,
        Last6Months,
        Last12Months,
        AllTime
    }
}