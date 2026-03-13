using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using TekTomato.Data;
using TekTomato.Models;

namespace TekTomato.Services
{
    /// <summary>
    /// Handles session recording and statistical queries for pomodoro data.
    /// </summary>
    public class SessionService
    {
        #region Private Fields

        private readonly TekTomatoDbContext _dbContext;

        #endregion Private Fields

        #region Constructor

        /// <summary>
        /// Initialises the session service with database context.
        /// </summary>
        public SessionService(TekTomatoDbContext dbContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        #endregion Constructor

        #region Public Methods

        /// <summary>
        /// Records a completed pomodoro session asynchronously.
        /// </summary>
        /// <param name="session">The session to record.</param>
        public async Task RecordSessionAsync(PomodoroSession session)
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            _dbContext.Sessions.Add(session);
            await _dbContext.SaveChangesAsync();
        }

        /// <summary>
        /// Gets the count of completed pomodoro sessions per day within a date range.
        /// Only counts Work sessions that completed normally.
        /// </summary>
        /// <param name="from">Start date (inclusive). Null means no lower bound.</param>
        /// <param name="to">End date (inclusive). Null means no upper bound.</param>
        /// <returns>Dictionary mapping DateOnly to pomodoro count for each day with activity.</returns>
        public async Task<Dictionary<DateOnly, int>> GetDailyPomodoroCounts(DateOnly? from = null, DateOnly? to = null)
        {
            var query = _dbContext.Sessions
                .Where(s => s.SessionType == "Work" && s.CompletedNormally);

            // Apply date filters
            if (from.HasValue)
            {
                query = query.Where(s => s.StartedAtUtc.Date >= from.Value.ToDateTime(TimeOnly.MinValue));
            }

            if (to.HasValue)
            {
                query = query.Where(s => s.StartedAtUtc.Date <= to.Value.ToDateTime(TimeOnly.MaxValue));
            }

            var sessions = await query.ToListAsync();

            // Group by date and count
            return sessions
                .GroupBy(s => DateOnly.FromDateTime(s.StartedAtUtc))
                .ToDictionary(
                    g => g.Key,
                    g => g.Count()
                );
        }

        /// <summary>
        /// Gets the total focus minutes per day within a date range.
        /// Only counts Work sessions that completed normally.
        /// </summary>
        /// <param name="from">Start date (inclusive). Null means no lower bound.</param>
        /// <param name="to">End date (inclusive). Null means no upper bound.</param>
        /// <returns>List of (Date, minutes) tuples for each day with focus activity.</returns>
        public async Task<List<(DateOnly date, double minutes)>> GetDailyFocusMinutes(DateOnly? from = null, DateOnly? to = null)
        {
            var query = _dbContext.Sessions
                .Where(s => s.SessionType == "Work" && s.CompletedNormally);

            // Apply date filters
            if (from.HasValue)
            {
                query = query.Where(s => s.StartedAtUtc.Date >= from.Value.ToDateTime(TimeOnly.MinValue));
            }

            if (to.HasValue)
            {
                query = query.Where(s => s.StartedAtUtc.Date <= to.Value.ToDateTime(TimeOnly.MaxValue));
            }

            var sessions = await query.ToListAsync();

            // Group by date and sum actual duration
            return sessions
                .GroupBy(s => DateOnly.FromDateTime(s.StartedAtUtc))
                .Select(g => (
                    date: g.Key,
                    minutes: g.Sum(s => s.ActualDurationSeconds) / 60.0
                ))
                .OrderBy(x => x.date)
                .ToList();
        }

        /// <summary>
        /// Gets the total focus minutes per week within a date range.
        /// Groups by ISO week starting Monday. Only counts Work sessions that completed normally.
        /// </summary>
        /// <param name="from">Start date (inclusive). Null means no lower bound.</param>
        /// <param name="to">End date (inclusive). Null means no upper bound.</param>
        /// <returns>List of (WeekStart, minutes) tuples for each week with focus activity.</returns>
        public async Task<List<(DateOnly weekStart, double minutes)>> GetWeeklyFocusMinutes(DateOnly? from = null, DateOnly? to = null)
        {
            var query = _dbContext.Sessions
                .Where(s => s.SessionType == "Work" && s.CompletedNormally);

            // Apply date filters
            if (from.HasValue)
            {
                query = query.Where(s => s.StartedAtUtc.Date >= from.Value.ToDateTime(TimeOnly.MinValue));
            }

            if (to.HasValue)
            {
                query = query.Where(s => s.StartedAtUtc.Date <= to.Value.ToDateTime(TimeOnly.MaxValue));
            }

            var sessions = await query.ToListAsync();

            // Group by week start (Monday) and sum actual duration
            return sessions
                .GroupBy(s => GetWeekStart(DateOnly.FromDateTime(s.StartedAtUtc)))
                .Select(g => (
                    weekStart: g.Key,
                    minutes: g.Sum(s => s.ActualDurationSeconds) / 60.0
                ))
                .OrderBy(x => x.weekStart)
                .ToList();
        }

        #endregion Public Methods

        #region Private Methods

        /// <summary>
        /// Gets the Monday of the week containing the given date.
        /// </summary>
        private static DateOnly GetWeekStart(DateOnly date)
        {
            // Get days since Monday (0=Monday, 1=Tuesday, etc.)
            var dayOfWeek = date.DayOfWeek;
            var daysSinceMonday = (int)dayOfWeek == DayOfWeek.Monday 
                ? 0 
                : (7 + (int)dayOfWeek - 1) % 7;

            return date.AddDays(-daysSinceMonday);
        }

        #endregion Private Methods
    }
}