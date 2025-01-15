using System.Net.Http;
using System.Net.Http.Headers;
using Newtonsoft.Json;

namespace FinanceTracker.API.Banking
{
    public class BankingService
    {
        private readonly HttpClient _httpClient;

        public BankingService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<List<Transaction>> GetTransactions(string userToken)
        {
            const string endpoint = "https://www.saltedge.com/api/v5/transactions";

            // Add required headers for Salt Edge API
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", userToken);
            _httpClient.DefaultRequestHeaders.Add("Client-ID", "YOUR_CLIENT_ID");
            _httpClient.DefaultRequestHeaders.Add("App-Secret", "YOUR_APP_SECRET");

            // Make the API request
            var response = await _httpClient.GetAsync(endpoint);
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new Exception($"Salt Edge API error: {response.StatusCode} - {errorContent}");
            }

            var content = await response.Content.ReadAsStringAsync();
            var result = JsonConvert.DeserializeObject<SaltEdgeTransactionResponse>(content);

            // Map transactions to your application's model
            return result.Data.Select(transaction => new Transaction
            {
                Description = transaction.Description,
                Category = transaction.Category,
                Amount = transaction.Amount,
                Date = transaction.MadeOn
            }).ToList();
        }
    }

    // Salt Edge API response format
    public class SaltEdgeTransactionResponse
    {
        public List<SaltEdgeTransaction> Data { get; set; }
    }

    public class SaltEdgeTransaction
    {
        public string Description { get; set; }
        public string Category { get; set; }
        public decimal Amount { get; set; }
        public DateTime MadeOn { get; set; }
    }

    // Your application's transaction model
    public class Transaction
    {
        public string Description { get; set; }
        public string Category { get; set; }
        public decimal Amount { get; set; }
        public DateTime Date { get; set; }
    }
}
