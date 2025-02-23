using FinanceTracker.API.Data;
using FinanceTracker.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.Trainers;

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
                .AsNoTracking()
                .Where(e => e.UserId == userId)
                .ToListAsync();

            var userIncome = await _context.Users
                .AsNoTracking()
                .Where(u => u.Id == userId)
                .Select(u => u.TotalIncome)
                .FirstOrDefaultAsync();

            return expenses.Select(expense => new ExpenseData
            {
                Income = (float)userIncome,
                Category = expense.Category,
                IsHolidaySeason = IsHolidaySeason(expense.Date),
                UserSpecificWeight = CalculateUserSpecificWeight(userId, expense.Category)
            }).ToList();
        }

        private bool IsHolidaySeason(DateTime date)
        {
            var holidays = new List<DateTime>
            {
                new DateTime(date.Year, 12, 25), 
                new DateTime(date.Year, 1, 1)   
            };

            return holidays.Any(h => h.Month == date.Month && h.Day == date.Day);
        }

        private float CalculateUserSpecificWeight(string userId, string category)
        {
            var categorySpending = _context.Expenses
                .AsNoTracking()
                .Where(e => e.UserId == userId && e.Category == category)
                .Sum(e => (float?)e.Amount) ?? 0;

            return categorySpending > 1000 ? 1.5f : 1.0f;
        }

        public ITransformer TrainCategoryPredictionModel(IEnumerable<ExpenseData> expenseData)
        {
            if (expenseData == null || !expenseData.Any())
            {
                throw new ArgumentException("Expense data cannot be null or empty.", nameof(expenseData));
            }

            Console.WriteLine($"Preparing data... Total records: {expenseData.Count()}");

            try
            {
                var dataView = _mlContext.Data.LoadFromEnumerable(expenseData);
                Console.WriteLine("DataView successfully created.");

                var trainerOptions = new SdcaMaximumEntropyMulticlassTrainer.Options
                {
                    LabelColumnName = "CategoryKey",
                    FeatureColumnName = "Features",
                    MaximumNumberOfIterations = 50,
                    NumberOfThreads = 1
                };

                var pipeline = _mlContext.Transforms.Conversion.MapValueToKey("CategoryKey", nameof(ExpenseData.Category))
                    .Append(_mlContext.Transforms.Text.FeaturizeText("DescriptionFeatures", nameof(ExpenseData.Description)))
                    .Append(_mlContext.Transforms.Concatenate("Features", "DescriptionFeatures"))
                    .Append(_mlContext.MulticlassClassification.Trainers.SdcaMaximumEntropy(trainerOptions))
                    .Append(_mlContext.Transforms.Conversion.MapKeyToValue("PredictedCategory", "PredictedLabel"));

                Console.WriteLine("Starting pipeline training...");

                var startTime = DateTime.UtcNow;
                var model = pipeline.Fit(dataView);
                var endTime = DateTime.UtcNow;

                Console.WriteLine($"Pipeline training completed successfully. Duration: {(endTime - startTime).TotalSeconds} seconds.");
                return model;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during pipeline execution: {ex.Message}");
                throw;
            }
        }

        public string PredictCategory(ITransformer model, string description)
        {
            if (string.IsNullOrWhiteSpace(description))
                throw new ArgumentException("Description cannot be null or empty.", nameof(description));

            var predictionEngine = _mlContext.Model.CreatePredictionEngine<ExpenseData, ExpenseCategoryPrediction>(model);
            var prediction = predictionEngine.Predict(new ExpenseData
            {
                Description = description
            });

            return prediction.PredictedCategory;
        }

        public async Task<decimal> PredictNextMonthExpenseForCategoryAsync(string category, string userId)
        {
            try
            {
               
                if (string.IsNullOrWhiteSpace(category))
                    throw new ArgumentException("Category cannot be null or empty.", nameof(category));

                if (string.IsNullOrWhiteSpace(userId))
                    throw new ArgumentException("UserId cannot be null or empty.", nameof(userId));

               
                var historicalData = await _context.Expenses
                    .AsNoTracking()
                    .Where(e => e.UserId == userId && e.Category == category)
                    .GroupBy(e => new { e.Date.Year, e.Date.Month })
                    .Select(g => new MonthlyExpenseData
                    {
                        Year = g.Key.Year,
                        Month = g.Key.Month,
                        TotalAmount = g.Sum(e => (float)e.Amount)
                    })
                    .ToListAsync();

                if (!historicalData.Any())
                    throw new InvalidOperationException("No historical data available for this category.");

                Console.WriteLine($"Fetched {historicalData.Count} historical data records for category: {category}.");

                
                var dataView = _mlContext.Data.LoadFromEnumerable(historicalData);
                Console.WriteLine("DataView successfully created.");

                
                var trainerOptions = new SdcaRegressionTrainer.Options
                {
                    LabelColumnName = nameof(MonthlyExpenseData.TotalAmount),
                    FeatureColumnName = "Features",
                    MaximumNumberOfIterations = 100,
                    ConvergenceTolerance = 0.001f
                };

                
                var pipeline = _mlContext.Transforms.Conversion.ConvertType(outputColumnName: "YearFloat", inputColumnName: nameof(MonthlyExpenseData.Year), outputKind: DataKind.Single)
                    .Append(_mlContext.Transforms.Conversion.ConvertType(outputColumnName: "MonthFloat", inputColumnName: nameof(MonthlyExpenseData.Month), outputKind: DataKind.Single))
                    .Append(_mlContext.Transforms.Concatenate("Features", "YearFloat", "MonthFloat"))
                    .Append(_mlContext.Regression.Trainers.Sdca(trainerOptions));

                Console.WriteLine("Starting pipeline training...");

                var startTime = DateTime.UtcNow;

              
                var model = pipeline.Fit(dataView);

                var endTime = DateTime.UtcNow;
                Console.WriteLine($"Pipeline training completed in {(endTime - startTime).TotalSeconds} seconds.");

                
                const string modelPath = "model.zip";
                _mlContext.Model.Save(model, dataView.Schema, modelPath);
                Console.WriteLine($"Model saved to {modelPath}.");

                
                var lastData = historicalData.OrderByDescending(h => h.Year).ThenByDescending(h => h.Month).First();
                var nextMonth = lastData.Month == 12 ? 1 : lastData.Month + 1;
                var nextYear = lastData.Month == 12 ? lastData.Year + 1 : lastData.Year;

                
                var loadedModel = _mlContext.Model.Load(modelPath, out _);
                var predictionEngine = _mlContext.Model.CreatePredictionEngine<MonthlyExpenseData, MonthlyExpensePrediction>(loadedModel);

                var prediction = predictionEngine.Predict(new MonthlyExpenseData
                {
                    Year = nextYear,
                    Month = nextMonth
                });

                Console.WriteLine($"Predicted Amount for {nextMonth}/{nextYear}: {prediction.PredictedAmount}");

                return (decimal)prediction.PredictedAmount;
            }
            catch (InvalidOperationException ex)
            {
                Console.WriteLine($"Validation error: {ex.Message}");
                throw new ArgumentException("Invalid input or insufficient data for prediction.", ex);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error predicting next month's expense: {ex.Message}");
                throw;
            }
        }



        public class MonthlyExpenseData
        {
            public float Year { get; set; }
            public float Month { get; set; }
            public float TotalAmount { get; set; }
        }

        public class MonthlyExpensePrediction
        {
            [ColumnName("Score")]
            public float PredictedAmount { get; set; }
        }

        public class ExpenseCategoryPrediction
        {
            [ColumnName("PredictedCategory")]
            public string PredictedCategory { get; set; }
        }
    }
}
