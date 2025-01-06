using Newtonsoft.Json;

namespace FinanceTracker.API.Banking
{
    public class CurrencyExchangeService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;

        public CurrencyExchangeService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _configuration = configuration;
        }

        public async Task<decimal> ConvertCurrency(string fromCurrency, string toCurrency, decimal amount)
        {
            var apiKey = _configuration["CurrencyExchange:ApiKey"];
            var endpoint = $"https://api.exchangeratesapi.io/latest?base={fromCurrency}";

            var response = await _httpClient.GetAsync(endpoint);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var rates = JsonConvert.DeserializeObject<CurrencyExchangeResponse>(content);

            if (rates.Rates.TryGetValue(toCurrency, out var rate))
            {
                return amount * (decimal)rate;
            }

            throw new Exception($"Conversion rate for {toCurrency} not found.");
        }
    }

    public class CurrencyExchangeResponse
    {
        public Dictionary<string, double> Rates { get; set; }
    }

}
