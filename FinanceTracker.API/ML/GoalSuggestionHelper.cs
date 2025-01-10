using Microsoft.ML.Data;
using Microsoft.ML;
using System.Linq;

namespace FinanceTracker.API.ML
{
    public class GoalSuggestionHelper
    {
        private readonly MLContext _mlContext;

        public GoalSuggestionHelper()
        {
            _mlContext = new MLContext();
        }

        // Train the Goal Suggestion Model
        public ITransformer TrainGoalSuggestionModel(IEnumerable<PredictionData> expenseData)
        {
            if (expenseData == null || !expenseData.Any())
                throw new ArgumentException("Expense data is null or empty.");

            // Validate the input data
            var validData = expenseData.Where(e => !string.IsNullOrWhiteSpace(e.Category)).ToList();
            if (!validData.Any())
                throw new ArgumentException("No valid data with non-empty categories.");

            // Load data into IDataView
            var dataView = _mlContext.Data.LoadFromEnumerable(validData);

            // Define the ML.NET pipeline
            var pipeline = _mlContext.Transforms.Conversion.MapValueToKey(
                    inputColumnName: nameof(PredictionData.Category),
                    outputColumnName: "CategoryKey")
                .Append(_mlContext.Transforms.Categorical.OneHotEncoding(
                    inputColumnName: "CategoryKey",
                    outputColumnName: "CategoryEncoded"))
                .Append(_mlContext.Transforms.Concatenate(
                    "Features",
                    "CategoryEncoded",
                    nameof(PredictionData.TotalAmount),
                    nameof(PredictionData.IsRecurring)))
                .Append(_mlContext.Transforms.NormalizeMinMax("Features"))
                .Append(_mlContext.Regression.Trainers.Sdca(
                    labelColumnName: nameof(PredictionData.TotalAmount),
                    featureColumnName: "Features"));

            // Train the model
            var model = pipeline.Fit(dataView);

            return model;
        }

        // Predict Goal Categories
        public IEnumerable<string> PredictGoalCategories(ITransformer model, IEnumerable<PredictionData> futureData)
        {
            if (futureData == null || !futureData.Any())
                throw new ArgumentException("Future data is null or empty.");

            // Create a prediction engine
            var predictionEngine = _mlContext.Model.CreatePredictionEngine<PredictionData, GoalPrediction>(model);

            // Predict categories for each data point
            foreach (var data in futureData)
            {
                if (string.IsNullOrWhiteSpace(data.Category))
                    throw new ArgumentException("Future data contains a null or empty category.");

                var prediction = predictionEngine.Predict(data);
                yield return prediction.PredictedCategory; // Return the predicted category
            }
        }
    }

    // Data structure for training and prediction
    public class PredictionData
    {
        public string Category { get; set; }
        public float TotalAmount { get; set; }
        public float IsRecurring { get; set; } // Changed from bool to float for compatibility
    }

    // Prediction output structure
    public class GoalPrediction
    {
        public float Score { get; set; } // Regression score (predicted value)
        public string PredictedCategory { get; set; } // Predicted category name
    }
}
