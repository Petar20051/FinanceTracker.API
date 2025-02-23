using Microsoft.ML.Data;
using Microsoft.ML;
using System.Linq;
using Microsoft.ML.Trainers;
using System.Collections.Generic;
using System;

namespace FinanceTracker.API.ML
{
    public class GoalSuggestionModel
    {
        public ITransformer Model { get; set; }
        public Dictionary<string, float> CategoryAverages { get; set; }
    }

    public class GoalSuggestionHelper
    {
        private readonly MLContext _mlContext;

        public GoalSuggestionHelper()
        {
            _mlContext = new MLContext();
        }

        public GoalSuggestionModel TrainGoalSuggestionModel(IEnumerable<PredictionData> expenseData)
        {
            if (expenseData == null || !expenseData.Any())
                throw new ArgumentException("Expense data is null or empty.");

            var validData = expenseData.Where(e => !string.IsNullOrWhiteSpace(e.Category)).ToList();
            if (!validData.Any())
                throw new ArgumentException("No valid data with non-empty categories.");

            
            var categoryAverages = validData
                .GroupBy(e => e.Category.ToLowerInvariant())
                .ToDictionary(g => g.Key, g => g.Average(x => x.TotalAmount));

            var dataView = _mlContext.Data.LoadFromEnumerable(validData);

            var trainerOptions = new SdcaRegressionTrainer.Options
            {
                LabelColumnName = nameof(PredictionData.TotalAmount),
                FeatureColumnName = "Features",
                MaximumNumberOfIterations = 100,
                ConvergenceTolerance = 0.001f
            };

            var pipeline = _mlContext.Transforms.Conversion.MapValueToKey(
                    inputColumnName: nameof(PredictionData.Category),
                    outputColumnName: "CategoryKey")
                .Append(_mlContext.Transforms.Categorical.OneHotEncoding(
                    inputColumnName: "CategoryKey",
                    outputColumnName: "CategoryEncoded"))
                .Append(_mlContext.Transforms.Concatenate(
                    "Features",
                    "CategoryEncoded",
                    nameof(PredictionData.IsRecurring)))
                .Append(_mlContext.Transforms.NormalizeMinMax("Features"))
                .Append(_mlContext.Regression.Trainers.Sdca(trainerOptions));

            var model = pipeline.Fit(dataView);

            return new GoalSuggestionModel
            {
                Model = model,
                CategoryAverages = categoryAverages
            };
        }

        public string PredictGoalCategory(GoalSuggestionModel goalModel, PredictionData futureData)
        {
            if (futureData == null)
                throw new ArgumentException("Future data is null.");

            if (string.IsNullOrWhiteSpace(futureData.Category))
                throw new ArgumentException("Future data contains a null or empty category.");

            var predictionEngine = _mlContext.Model.CreatePredictionEngine<PredictionData, RegressionPrediction>(goalModel.Model);

            var prediction = predictionEngine.Predict(futureData);

            string predictedCategory = MapScoreToCategory(prediction.Score, goalModel.CategoryAverages);

            return predictedCategory;
        }

        private string MapScoreToCategory(float predictedScore, Dictionary<string, float> categoryAverages)
        {
            string closestCategory = null;
            float smallestDiff = float.MaxValue;

            foreach (var kvp in categoryAverages)
            {
                float diff = Math.Abs(predictedScore - kvp.Value);
                if (diff < smallestDiff)
                {
                    smallestDiff = diff;
                    closestCategory = kvp.Key;
                }
            }

            return char.ToUpper(closestCategory[0]) + closestCategory.Substring(1);
        }
    }

    public class PredictionData
    {
        public string Category { get; set; }
        public float TotalAmount { get; set; }
        public float IsRecurring { get; set; }
    }

    public class RegressionPrediction
    {
        public float Score { get; set; }
    }
}
