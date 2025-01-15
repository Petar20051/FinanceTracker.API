using Newtonsoft.Json;
using System.Text;

namespace FinanceTracker.API.Banking
{
    public class SaltEdgeService
    {
        private readonly HttpClient _httpClient;

        public SaltEdgeService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<string> GenerateConnectUrlAsync()
        {
            const string endpoint = "https://www.saltedge.com/api/v5/connect_sessions/create";

            // Prepare the request body
            var requestBody = new
            {
                data = new
                {
                    customer_id = "1454943464931203074", // Replace with a valid customer ID
                    return_to = "http://localhost:3000/dashboard", // Redirect after linking
                    consent = new
                    {
                        scopes = new[] { "account_details", "transactions", "balance" } // Valid scopes
                    }
                }
            };



            var content = new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");

            // Clear any existing headers to avoid conflicts
            _httpClient.DefaultRequestHeaders.Clear();

            // Add the required headers
            _httpClient.DefaultRequestHeaders.Add("App-Id", "Y9SCdCcgvs0zE82XP9Mk3NWTELQ_SOd58NiwkZWSTh0");
            _httpClient.DefaultRequestHeaders.Add("Secret", "iJjo6GqFjcx60EO7Bv64g_pnC5k4MaXo9grfopvhTX4");

            try
            {
                // Log headers for debugging
                foreach (var header in _httpClient.DefaultRequestHeaders)
                {
                    Console.WriteLine($"Header: {header.Key} = {string.Join(", ", header.Value)}");
                }

                var response = await _httpClient.PostAsync(endpoint, content);

                // Check if the response is successful
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"Salt Edge API Error: {errorContent}");
                    throw new Exception($"Salt Edge API error: {errorContent}");
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                var result = JsonConvert.DeserializeObject<dynamic>(responseContent);

                return result.data.connect_url;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error generating Salt Edge Connect URL: {ex.Message}");
                throw;
            }
        }
    }
}
