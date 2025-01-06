using FinanceTracker.API.Data;
using FinanceTracker.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.Transforms.TimeSeries;

namespace FinanceTracker.API.ML
{
    public class MLModelHelper
    {
        private readonly MLContext _mlContext;
        private readonly FinanceTrackerDbContext _context;

        public MLModelHelper(FinanceTrackerDbContext financeTrackerDbContext)
        {
            _mlContext = new MLContext();
            _context = financeTrackerDbContext ?? throw new ArgumentNullException(nameof(financeTrackerDbContext));
        }

        public async Task<List<ExpenseData>> PrepareHistoricalData(string userId)
        {
            var expenses = await _context.Expenses
                .Where(e => e.UserId == userId)
                .ToListAsync();

            var userIncome = await _context.Users
                .Where(u => u.Id == userId)
                .Select(u => u.TotalIncome)
                .FirstOrDefaultAsync();

            var historicalData = new List<ExpenseData>();

            foreach (var expense in expenses)
            {
                var isHolidaySeason = IsHolidaySeason(expense.Date);
                var userSpecificWeight = CalculateUserSpecificWeight(userId, expense.Category);

                historicalData.Add(new ExpenseData
                {
                    TotalAmount = (float)expense.Amount,
                    Income = (float)(userIncome),
                    Category = expense.Category,
                    IsHolidaySeason = isHolidaySeason,
                    UserSpecificWeight = userSpecificWeight
                });
            }

            return historicalData;
        }

        public bool IsHolidaySeason(DateTime date)
        {
            var holidays = new List<DateTime>
        {
            new DateTime(date.Year, 12, 25),
            new DateTime(date.Year, 1, 1)
        };

            return holidays.Any(h => h.Month == date.Month && h.Day == date.Day);
        }

        public float CalculateUserSpecificWeight(string userId, string category)
        {
            var categorySpending = _context.Expenses
                .Where(e => e.UserId == userId && e.Category == category)
                .Sum(e => (float?)e.Amount) ?? 0;

            return categorySpending > 1000 ? 1.5f : 1.0f;
        }

        public ITransformer TrainModel(IEnumerable<ExpenseData> historicalData)
        {
            if (!historicalData.Any())
                throw new ArgumentException("Historical data cannot be empty.", nameof(historicalData));

            var dataView = _mlContext.Data.LoadFromEnumerable(historicalData);

            var pipeline = _mlContext.Transforms.Conversion.MapValueToKey(nameof(ExpenseData.Category))
                .Append(_mlContext.Transforms.Categorical.OneHotEncoding(nameof(ExpenseData.Category)))
                .Append(_mlContext.Transforms.Concatenate("Features",
                    nameof(ExpenseData.TotalAmount),
                    nameof(ExpenseData.Income),
                    nameof(ExpenseData.IsHolidaySeason),
                    nameof(ExpenseData.UserSpecificWeight)))
                .Append(_mlContext.Regression.Trainers.Sdca(
                    labelColumnName: nameof(ExpenseData.TotalAmount),
                    featureColumnName: "Features"));

            return pipeline.Fit(dataView);
        }

        public IEnumerable<float> PredictExpenses(ITransformer model, int horizon, IEnumerable<ExpenseData> futureData)
        {
            if (!futureData.Any())
                throw new ArgumentException("Future data cannot be empty.", nameof(futureData));

            var predictionEngine = _mlContext.Model.CreatePredictionEngine<ExpenseData, ExpensePrediction>(model);

            foreach (var data in futureData)
            {
                var prediction = predictionEngine.Predict(data);
                yield return prediction.PredictedAmount;
            }
        }
        public class ExpenseCategoryPrediction
        {
            [ColumnName("PredictedLabel")]
            public string PredictedCategory { get; set; }
        }

        public ITransformer TrainCategoryPredictionModel(IEnumerable<ExpenseData> expenseData)
        {
            var dataView = _mlContext.Data.LoadFromEnumerable(expenseData);

            var pipeline = _mlContext.Transforms.Text.FeaturizeText("Features", nameof(ExpenseData.Description))
                .Append(_mlContext.Transforms.Conversion.MapValueToKey(nameof(ExpenseData.Category)))
                .Append(_mlContext.MulticlassClassification.Trainers.SdcaMaximumEntropy())
                .Append(_mlContext.Transforms.Conversion.MapKeyToValue("PredictedLabel"));

            var model = pipeline.Fit(dataView);
            return model;
        }

        public string PredictCategory(ITransformer model, string description)
        {
            var predictionEngine = _mlContext.Model.CreatePredictionEngine<ExpenseData, ExpenseCategoryPrediction>(model);
            var prediction = predictionEngine.Predict(new ExpenseData { Description = description });
            return prediction.PredictedCategory;
        }


    }
}
