using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json;

namespace FinanceTracker.API.Banking
{
    public class BankingService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;

        public BankingService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _configuration = configuration;
        }

        public async Task<List<Transaction>> GetTransactions(string connectionId)
        {
            const string endpoint = "https://www.saltedge.com/api/v6/transactions";
            var appId = _configuration["SaltEdge:ClientID"];
            var appSecret = _configuration["SaltEdge:AppSecret"];

           
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("App-Id", appId);
            _httpClient.DefaultRequestHeaders.Add("Secret", appSecret);

            try
            {
               
                var requestUrl = $"{endpoint}?connection_id={connectionId}";

              
                var response = await _httpClient.GetAsync(requestUrl);

                
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"Salt Edge API error: {response.StatusCode} - {errorContent}");
                    throw new Exception($"Salt Edge API error: {response.StatusCode} - {errorContent}");
                }

               
                var responseContent = await response.Content.ReadAsStringAsync();
                var result = JsonConvert.DeserializeObject<SaltEdgeTransactionResponse>(responseContent);

                
                return result.Data.Select(transaction => new Transaction
                {
                    Description = transaction.Description,
                    Category = transaction.Category,
                    Amount = transaction.Amount,
                    Date = transaction.MadeOn
                }).ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching transactions: {ex.Message}");
                throw;
            }
        }

        public class SaltEdgeTransactionResponse
        {
            public List<SaltEdgeTransaction> Data { get; set; }
        }

       
        public class SaltEdgeTransaction
        {
            public string Description { get; set; }
            public string Category { get; set; }
            public decimal Amount { get; set; }
            [JsonProperty("made_on")]
            public DateTime MadeOn { get; set; }
        }

        
        public class Transaction
        {
            public string Description { get; set; }
            public string Category { get; set; }
            public decimal Amount { get; set; }
            public DateTime Date { get; set; }
        }
    }
}
