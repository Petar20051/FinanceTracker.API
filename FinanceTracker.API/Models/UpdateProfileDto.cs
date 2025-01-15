using System.ComponentModel.DataAnnotations;

namespace FinanceTracker.API.Models
{
    public class UpdateProfileDto
    {
        [Required(ErrorMessage = "Full Name is required")]
        public string FullName { get; set; }

        [Required(ErrorMessage = "Date of Birth is required")]
        public DateTime DateOfBirth { get; set; }

        [Required(ErrorMessage = "Total Income is required")]
        [Range(0, double.MaxValue, ErrorMessage = "Income must be a positive number")]
        public decimal TotalIncome { get; set; }
    }
}
