﻿using Newtonsoft.Json;
using System.Text;

namespace FinanceTracker.API.Banking
{
    public class SaltEdgeService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;

        public SaltEdgeService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _configuration = configuration;
        }

        public async Task<string> GenerateConnectUrlAsync()
        {
            const string endpoint = "https://www.saltedge.com/api/v6/connections/connect";

           
            var requestBody = new
            {
                data = new
                {
                    customer_id = "1454943464931203074", 
                    attempt = new
                    {
                        return_to = "http://localhost:3000/dashboard" 
                    },
                    consent = new
                    {
                        scopes = new[] { "holder_info", "accounts", "transactions" } 
                    }
                }
            };

            var content = new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");

           
            _httpClient.DefaultRequestHeaders.Clear();

            var appId = _configuration["SaltEdge:ClientID"];
            var appSecret = _configuration["SaltEdge:AppSecret"];

            if (string.IsNullOrEmpty(appId) || string.IsNullOrEmpty(appSecret))
            {
                throw new Exception("Salt Edge App ID or Secret is not configured.");
            }

            _httpClient.DefaultRequestHeaders.Add("App-Id", appId);
            _httpClient.DefaultRequestHeaders.Add("Secret", appSecret);

            try
            {
                var response = await _httpClient.PostAsync(endpoint, content);

                
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