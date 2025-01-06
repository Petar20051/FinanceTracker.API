using Microsoft.ML.Data;
using Microsoft.ML;

namespace FinanceTracker.API.ML
{
    public class GoalSuggestionHelper
    {
        private readonly MLContext _mlContext;

        public GoalSuggestionHelper()
        {
            _mlContext = new MLContext();
        }

        public ITransformer TrainGoalSuggestionModel(IEnumerable<PredictionData> expenseData)
        {
            var dataView = _mlContext.Data.LoadFromEnumerable(expenseData);

            var pipeline = _mlContext.Transforms.Conversion.MapValueToKey(nameof(PredictionData.Category))
                .Append(_mlContext.Transforms.Categorical.OneHotEncoding(nameof(PredictionData.Category)))
                .Append(_mlContext.Transforms.Concatenate("Features", nameof(PredictionData.TotalAmount), nameof(PredictionData.IsRecurring)))
                .Append(_mlContext.Transforms.Conversion.MapKeyToValue("PredictedCategory", nameof(PredictionData.Category)))
                .Append(_mlContext.Transforms.NormalizeMinMax("Features"))
                .Append(_mlContext.Regression.Trainers.Sdca(labelColumnName: nameof(PredictionData.TotalAmount), featureColumnName: "Features"));

            var model = pipeline.Fit(dataView);
            return model;
        }

        public IEnumerable<string> PredictGoalCategories(ITransformer model, IEnumerable<PredictionData> futureData)
        {
            var predictionEngine = _mlContext.Model.CreatePredictionEngine<PredictionData, GoalPrediction>(model);

            foreach (var data in futureData)
            {
                var prediction = predictionEngine.Predict(data);
                yield return prediction.PredictedCategory;
            }
        }
    }

    public class PredictionData
    {
        public string Category { get; set; }
        public float TotalAmount { get; set; }
        public bool IsRecurring { get; set; }
    }

    public class GoalPrediction
    {
        [ColumnName("PredictedCategory")]
        public string PredictedCategory { get; set; }
    }
}
