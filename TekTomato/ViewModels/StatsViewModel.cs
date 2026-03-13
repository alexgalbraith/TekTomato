using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.Kernel.Sketches;
using LiveChartsCore.SkiaSharpView;
using TekTomato.Services;

namespace TekTomato.ViewModels
{
    /// <summary>
    /// View model for the Statistics window.
    /// Manages chart data loading and display options for Pomodoro session statistics.
    /// </summary>
    public partial class StatsViewModel : ObservableObject, INotifyPropertyChanged
    {
        #region Private Fields

        private readonly SessionService _sessionService;
        
        // Date range tracking
        private DateTime? _dateRangeStart;
        private DateTime? _dateRangeEnd;

        #endregion Private Fields

        #region Observable Properties

        /// <summary>
        /// Gets or sets the selected time range for statistics display.
        /// </summary>
        [ObservableProperty]
        private SelectedRangeEnum _selectedRange = SelectedRangeEnum.Last3Months;

        /// <summary>
        /// Gets or sets whether to display weekly aggregated data instead of daily.
        /// </summary>
        [ObservableProperty]
        private bool _isWeeklyView = false;

        /// <summary>
        /// Gets the heatmap data mapping dates to completed pomodoro counts.
        /// Used for calendar/heatmap visualization showing activity over time.
        /// </summary>
        [ObservableProperty]
        private Dictionary<DateOnly, int> _heatmapData = new();

        /// <summary>
        /// Gets the line chart series data for focus minutes visualization.
        /// Returns a single LineSeries with date-labels mapping to focus minutes.
        /// </summary>
        [ObservableProperty]
        private ISeries[] _lineChartSeries = Array.Empty<ISeries>();

        /// <summary>
        /// Gets the X-axis configuration for the chart.
        /// Configures DateTime axis with appropriate label formatter based on view mode.
        /// </summary>
        [ObservableProperty]
        private ICartesianAxis[] _xAxes = Array.Empty<ICartesianAxis>();

        #endregion Observable Properties

        #region Commands

        /// <summary>
        /// Command to refresh the chart data and re-render visualizations.
        /// Re-queries SessionService based on current SelectedRange and IsWeeklyView settings.
        /// </summary>
        [RelayCommand]
        private async Task Refresh()
        {
            await LoadDataAsync();
        }

        #endregion Commands

        #region Constructor

        /// <summary>
        /// Initialises the StatsViewModel with SessionService dependency.
        /// Calls LoadAsync on construction to populate initial chart data.
        /// </summary>
        public StatsViewModel(SessionService sessionService)
        {
            _sessionService = sessionService ?? throw new ArgumentNullException(nameof(sessionService));

            // Load initial data asynchronously
            Task.Run(async () => await LoadDataAsync());
        }

        #endregion Constructor

        #region Public Methods

        /// <summary>
        /// Loads and processes chart data based on current view settings.
        /// Called during construction and when Refresh command is executed.
        /// </summary>
        public async Task LoadAsync()
        {
            await LoadDataAsync();
        }

        #endregion Public Methods

        #region Private Methods

        /// <summary>
        /// Loads chart data from SessionService based on SelectedRange and IsWeeklyView settings.
        /// Updates HeatmapData, LineChartSeries, and XAxes properties.
        /// </summary>
        private async Task LoadDataAsync()
        {
            // Calculate date range based on selected time period
            (var startDate, var endDate) = GetDateRange();

            _dateRangeStart = startDate;
            _dateRangeEnd = endDate;

            // Load heatmap data for calendar view
            if (IsWeeklyView)
            {
                await LoadWeeklyDataAsync(startDate, endDate);
            }
            else
            {
                await LoadDailyDataAsync(startDate, endDate);
            }
        }

        /// <summary>
        /// Loads daily focus data and updates heatmap and line chart properties.
        /// </summary>
        private async Task LoadDailyDataAsync(DateTime startDate, DateTime endDate)
        {
            // Get daily counts for heatmap (work sessions only)
            var from = DateOnly.FromDateTime(startDate);
            var to = DateOnly.FromDateTime(endDate);
            
            var dailyCounts = await _sessionService.GetDailyPomodoroCounts(from, to);
            HeatmapData = dailyCounts;

            // Get daily focus minutes for line chart
            var focusMinutes = await _sessionService.GetDailyFocusMinutes(from, to);
            
            // Convert to chart series format
            LineChartSeries = new ISeries[]
            {
                new LineSeries<double>
                {
                    Values = focusMinutes.Select(fm => (double)fm.minutes).ToList(),
                    Name = "Focus Minutes",
                    XValues = focusMinutes.Select(fm => 
                    {
                        // Format dates as strings for X-axis labels
                        return fm.date.DayOfWeek switch
                        {
                            DayOfWeek.Monday => "Mon",
                            DayOfWeek.Tuesday => "Tue",
                            DayOfWeek.Wednesday => "Wed",
                            DayOfWeek.Thursday => "Thu",
                            DayOfWeek.Friday => "Fri",
                            DayOfWeek.Saturday => "Sat",
                            DayOfWeek.Sunday => "Sun",
                            _ => string.Empty
                        };
                    }).ToList()
                }
            };

            // Configure X-axis with day-of-week labels for daily view
            XAxes = new ICartesianAxis[]
            {
                new DateTimeAxis
                {
                    MinStep = TimeSpan.FromDays(1),
                    UnitMapping = UnitMappings.Days,
                    LabelsResolver = (date) => 
                    {
                        var dateOnly = DateOnly.FromDateTime(date.ToDateTime(TimeOnly.MinValue));
                        return dateOnly.DayOfWeek switch
                        {
                            DayOfWeek.Monday => "Mon",
                            DayOfWeek.Tuesday => "Tue",
                            DayOfWeek.Wednesday => "Wed",
                            DayOfWeek.Thursday => "Thu",
                            DayOfWeek.Friday => "Fri",
                            DayOfWeek.Saturday => "Sat",
                            DayOfWeek.Sunday => "Sun",
                            _ => string.Empty
                        };
                    }
                }
            };
        }

        /// <summary>
        /// Loads weekly focus data and updates line chart properties for weekly view.
        /// </summary>
        private async Task LoadWeeklyDataAsync(DateTime startDate, DateTime endDate)
        {
            // Get daily pomodoro counts (same as daily view, used for reference)
            var from = DateOnly.FromDateTime(startDate);
            var to = DateOnly.FromDateTime(endDate);
            
            var dailyCounts = await _sessionService.GetDailyPomodoroCounts(from, to);
            HeatmapData = dailyCounts;

            // Get weekly focus minutes aggregated by week
            var weeklyMinutes = await _sessionService.GetWeeklyFocusMinutes(from, to);
            
            // Convert to chart series format
            LineChartSeries = new ISeries[]
            {
                new LineSeries<double>
                {
                    Values = weeklyMinutes.Select(wm => (double)wm.minutes).ToList(),
                    Name = "Focus Minutes per Week",
                    XValues = weeklyMinutes.Select(wm => 
                    {
                        // Format week start date for display
                        return wm.weekStart.ToString("MMM dd");
                    }).ToList()
                }
            };

            // Configure X-axis with week labels for weekly view
            XAxes = new ICartesianAxis[]
            {
                new CartesianAxis
                {
                    UnitMapping = UnitMappings.Categorical,
                    LabelsResolver = (index) => 
                    {
                        if (weeklyMinutes.Any())
                        {
                            var weekIndex = ((int)index).GetValueOrDefault(0);
                            return weekIndex < weeklyMinutes.Count 
                                ? weeklyMinutes[weekIndex].weekStart.ToString("MMM")
                                : string.Empty;
                        }
                        return string.Empty;
                    }
                }
            };
        }

        /// <summary>
        /// Calculates the date range based on SelectedRange enum value.
        /// Returns (start, end) tuple with inclusive date boundaries.
        /// </summary>
        private (DateTime start, DateTime end) GetDateRange()
        {
            var now = DateTime.UtcNow;
            
            return SelectedRange switch
            {
                SelectedRangeEnum.Last3Months => 
                    (now.AddMonths(-3), now),
                SelectedRangeEnum.Last6Months => 
                    (now.AddMonths(-6), now),
                SelectedRangeEnum.Last12Months => 
                    (now.AddMonths(-12), now),
                SelectedRangeEnum.AllTime => 
                    (new DateTime(2000, 1, 1), now), // Practical minimum start date
                _ => 
                    (now.AddMonths(-3), now) // Default fallback
            };
        }

        #endregion Private Methods
    }

    /// <summary>
    /// Enum representing available time range selections for statistics display.
    /// </summary>
    public enum SelectedRangeEnum
    {
        Last3Months,
        Last6Months,
        Last12Months,
        AllTime
    }
}