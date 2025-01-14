using System.ComponentModel.DataAnnotations;

namespace FinanceTracker.API.Models
{
    public class Expense
    {
        public int Id { get; set; }

        [Required]
        [Range(0.01, double.MaxValue)]
        public decimal Amount { get; set; }

        [Required]
        [StringLength(100)]
        public string Category { get; set; }

        [MaxLength(500)]
        public string Description { get; set; }

        [Required]
        public DateTime Date { get; set; }

        
        public string? UserId { get; set; }

        public ApplicationUser? User { get; set; }
    }
}