using Microsoft.ML.Data;
using Microsoft.ML;
using System.Linq;
using Microsoft.ML.Trainers;

namespace FinanceTracker.API.ML
{
    public class GoalSuggestionHelper
    {
        private readonly MLContext _mlContext;

        public GoalSuggestionHelper()
        {
            _mlContext = new MLContext();
        }

        // Updated pipeline for category prediction
        public ITransformer TrainGoalSuggestionModel(IEnumerable<PredictionData> expenseData)
        {
            if (expenseData == null || !expenseData.Any())
                throw new ArgumentException("Expense data is null or empty.");

            var validData = expenseData.Where(e => !string.IsNullOrWhiteSpace(e.Category)).ToList();
            if (!validData.Any())
                throw new ArgumentException("No valid data with non-empty categories.");

            var dataView = _mlContext.Data.LoadFromEnumerable(validData);

            // Define options for the SdcaRegressionTrainer
            var trainerOptions = new SdcaRegressionTrainer.Options
            {
                LabelColumnName = nameof(PredictionData.TotalAmount),
                FeatureColumnName = "Features",
                MaximumNumberOfIterations = 100,
                ConvergenceTolerance = 0.001f
            };

            // Define and build the ML pipeline
            var pipeline = _mlContext.Transforms.Conversion.MapValueToKey(
                    inputColumnName: nameof(PredictionData.Category),
                    outputColumnName: "CategoryKey")
                .Append(_mlContext.Transforms.Categorical.OneHotEncoding(
                    inputColumnName: "CategoryKey",
                    outputColumnName: "CategoryEncoded"))
                .Append(_mlContext.Transforms.Concatenate(
                    "Features",
                    "CategoryEncoded",
                    nameof(PredictionData.IsRecurring))) // Keep relevant features for regression
                .Append(_mlContext.Transforms.NormalizeMinMax("Features")) // Normalize for regression
                .Append(_mlContext.Regression.Trainers.Sdca(trainerOptions));

            // Train the model
            var model = pipeline.Fit(dataView);


            return model;
        }

        // Updated prediction method
        public string PredictGoalCategory(ITransformer model, PredictionData futureData)
        {
            if (futureData == null)
                throw new ArgumentException("Future data is null or empty.");

            if (string.IsNullOrWhiteSpace(futureData.Category))
                throw new ArgumentException("Future data contains a null or empty category.");

            // Create a prediction engine
            var predictionEngine = _mlContext.Model.CreatePredictionEngine<PredictionData, GoalPrediction>(model);

            // Predict and return the category
            var prediction = predictionEngine.Predict(futureData);
            return prediction.PredictedCategory; // Return the predicted category as a string
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
