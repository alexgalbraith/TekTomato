using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TekTomato.Models
{
    /// <summary>
    /// Represents a single completed Pomodoro session in the database.
    /// </summary>
    [Table("Sessions")]
    public class PomodoroSession
    {
        /// <summary>
        /// Unique identifier for the session.
        /// </summary>
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        /// <summary>
        /// Type of session: "Work", "ShortBreak", or "LongBreak".
        /// </summary>
        [Required]
        [MaxLength(50)]
        public string SessionType { get; set; } = string.Empty;

        /// <summary>
        /// When the timer was started (UTC).
        /// </summary>
        [Required]
        public DateTime StartedAtUtc { get; set; }

        /// <summary>
        /// When the user acknowledged completion (UTC).
        /// </summary>
        [Required]
        public DateTime CompletedAtUtc { get; set; }

        /// <summary>
        /// The planned duration in seconds.
        /// </summary>
        [Required]
        public int PlannedDurationSeconds { get; set; }

        /// <summary>
        /// The actual active time spent (excluding pauses).
        /// </summary>
        [Required]
        public int ActualDurationSeconds { get; set; }

        /// <summary>
        /// Time spent past zero (Overtime) in seconds. Default 0.
        /// </summary>
        [Required]
        public int OverrunSeconds { get; set; } = 0;

        /// <summary>
        /// Total time spent paused in seconds. Default 0.
        /// </summary>
        [Required]
        public int PausedDurationSeconds { get; set; } = 0;

        /// <summary>
        /// True if the timer reached zero normally; False if abandoned early or manually stopped. Default true.
        /// </summary>
        [Required]
        public bool CompletedNormally { get; set; } = true;

        /// <summary>
        /// Position in cycle (1-N) for Work sessions, null for breaks.
        /// </summary>
        public int? PomodoroNumber { get; set; }

        // Navigation properties omitted for simplicity as this is a transactional entity without parent objects
    }
}