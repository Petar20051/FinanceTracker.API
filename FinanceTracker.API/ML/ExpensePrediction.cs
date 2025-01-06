using Microsoft.ML.Data;

namespace FinanceTracker.API.ML
{
    public class ExpensePrediction
    {
        public float PredictedAmount { get; set; }
    }
    public class ExpenseData
    {

        public float TotalAmount { get; set; }
        public float Income { get; set; } 
        public string Category { get; set; }
        public bool IsHolidaySeason { get; set; } 
        public float UserSpecificWeight { get; set; }

        public string? Description { get; set; }
    }

}
