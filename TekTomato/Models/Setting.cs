using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TekTomato.Models
{
    /// <summary>
    /// Represents an application setting stored in the database.
    /// </summary>
    [Table("Settings")]
    public class Setting
    {
        /// <summary>
        /// The unique key for the setting (e.g., "WorkDurationMinutes").
        /// </summary>
        [Key]
        [MaxLength(100)]
        public string Key { get; set; } = string.Empty;

        /// <summary>
        /// The value of the setting stored as a string.
        /// </summary>
        [Required]
        [MaxLength(500)]
        public string Value { get; set; } = string.Empty;
    }
}