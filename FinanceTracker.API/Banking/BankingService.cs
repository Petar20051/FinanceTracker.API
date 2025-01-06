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
            // Mock API endpoint
            var endpoint = $"https://mockbankingapi.com/transactions?token={userToken}";

            var response = await _httpClient.GetAsync(endpoint);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<List<Transaction>>(content);
        }
    }

    public class Transaction
    {
        public string Description { get; set; }
        public string Category { get; set; }
        public decimal Amount { get; set; }
        public DateTime Date { get; set; }
    }

}
