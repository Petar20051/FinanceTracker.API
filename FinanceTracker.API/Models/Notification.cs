using System.ComponentModel.DataAnnotations;

namespace FinanceTracker.API.Models
{
    public class Notification
    {
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; }

        [Required]
        public string Message { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public bool IsRead { get; set; } = false;

        public ApplicationUser User { get; set; }
    }
}
