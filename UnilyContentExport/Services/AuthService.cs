using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace UnilyContentExport.Services
{
    public class AuthService
    {
        private readonly HttpClient _httpClient;
        private OAuthToken? _token;
        private readonly string _identityServiceUri;
        private readonly string _clientId;
        private readonly string _clientSecret;
        private readonly string _scopes;

        public AuthService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _identityServiceUri = configuration["Unily:IdentityServiceUri"] ?? throw new InvalidOperationException("Unily:IdentityServiceUri is not configured.");
            _clientId = configuration["Unily:API:ClientId"] ?? throw new InvalidOperationException("Unily:API:ClientId is not configured.");
            _clientSecret = configuration["Unily:API:ClientSecret"] ?? throw new InvalidOperationException("Unily:API:ClientSecret is not configured.");
            _scopes = configuration["Unily:API:Scopes"] ?? throw new InvalidOperationException("Unily:API:Scopes is not configured.");
        }

        public async Task<string> GetAccessTokenAsync()
        {
            if (_token != null && _token.ExpiresAt > DateTime.UtcNow)
            {
                return _token.AccessToken;
            }

            return await RequestNewTokenAsync();
        }

        private async Task<string> RequestNewTokenAsync()
        {
            var requestBody = new Dictionary<string, string>
                {
                    { "client_id", _clientId },
                    { "client_secret", _clientSecret },
                    { "grant_type", "client_credentials" },
                    { "scope", _scopes }
                };

            var request = new HttpRequestMessage(HttpMethod.Post, $"{_identityServiceUri}/connect/token")
            {
                Content = new FormUrlEncodedContent(requestBody)
            };

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var responseString = await response.Content.ReadAsStringAsync();
            _token = JsonSerializer.Deserialize<OAuthToken>(responseString) ?? throw new InvalidOperationException("Failed to deserialize OAuthToken.");

            _token.ExpiresAt = DateTime.UtcNow.AddSeconds(_token.ExpiresIn - 60);

            return _token.AccessToken;
        }

        private class OAuthToken
        {
            [JsonPropertyName("access_token")]
            public required string AccessToken { get; set; }

            [JsonPropertyName("expires_in")]
            public int ExpiresIn { get; set; }

            public DateTime ExpiresAt { get; set; }
        }
    }
}
