namespace FinanceTracker.API.Models
{
    public class MonthlySummary
    {
        public string UserId { get; set; }
        public int Month { get; set; }
        public int Year { get; set; }
        public decimal TotalExpenses { get; set; }
    }

}
