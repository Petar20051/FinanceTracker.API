using Newtonsoft.Json;

namespace FinanceTracker.API.Banking
{
    public class CurrencyExchangeService
    {
        private readonly Dictionary<string, decimal> _exchangeRates;

        public CurrencyExchangeService()
        {
          
            _exchangeRates = new Dictionary<string, decimal>
            {
                { "USD:EUR", 0.85m },
                { "EUR:USD", 1.18m },
                { "USD:GBP", 0.75m },
                { "GBP:USD", 1.33m },
                { "EUR:GBP", 0.88m },
                { "GBP:EUR", 1.14m },
                { "USD:JPY", 110.50m },
                { "JPY:USD", 0.009m },
                { "EUR:JPY", 130.20m },
                { "JPY:EUR", 0.0077m },
                { "USD:BGN", 1.80m },
                { "BGN:USD", 0.56m },
                { "EUR:BGN", 1.95m },
                { "BGN:EUR", 0.51m },
                { "GBP:BGN", 2.29m },
                { "BGN:GBP", 0.44m },
                { "JPY:BGN", 0.016m },
                { "BGN:JPY", 62.50m }
            };
        }

        public Task<decimal> ConvertCurrency(string fromCurrency, string toCurrency, decimal amount)
        {
            var key = $"{fromCurrency.ToUpper()}:{toCurrency.ToUpper()}";

            if (_exchangeRates.TryGetValue(key, out var rate))
            {
                return Task.FromResult(amount * rate);
            }

            throw new Exception($"Conversion rate for {fromCurrency} to {toCurrency} not available.");
        }
    }
}