using System.ComponentModel.DataAnnotations;

namespace FinanceTracker.API.Models
{
    public class Budget
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Category { get; set; }

        [Required]
        [Range(0.01, double.MaxValue)]
        public decimal Limit { get; set; }

        public decimal Spent { get; set; }

        [Required]
        public string UserId { get; set; }

        public ApplicationUser User { get; set; }
    }
}