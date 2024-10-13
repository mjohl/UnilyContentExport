using Microsoft.Extensions.Configuration;
using Serilog;
using System.Text.Json;
using System.Text;
using UnilyContentExport.Models;

public static class SearchService
{
    private static ILogger? _logger;
    private static IConfiguration? _config;
    private static HttpClient? _httpClient;
    private static JsonSerializerOptions? _jsonSerializerOptions;

    public static void InitSearchService(ILogger logger, IConfiguration config, HttpClient httpClient)
    {
        _logger = logger;
        _config = config;
        _httpClient = httpClient;

        _jsonSerializerOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }

    public static async Task<Dictionary<long, string>> GetNodeTitlesByIds(Uri graphqlEndpoint, List<long> nodeIds)
    {
        if (_httpClient == null)
        {
            throw new InvalidOperationException("HttpClient is not initialized.");
        }

        string nodeIdsQuery = string.Join(" ", nodeIds.Select(id => $"id:{id}"));
        string query = $@"query GetNodeTitles {{ content {{ byQueryText(queryText: ""{nodeIdsQuery}"") {{ data {{ id nodeName }} }} }} }}";

        var response = await _httpClient.PostAsync(graphqlEndpoint, new StringContent(
            JsonSerializer.Serialize(new { query }), Encoding.UTF8, "application/json"));

        if (!response.IsSuccessStatusCode)
        {
            _logger?.Error("Failed to fetch node titles for ids {NodeIds}. Status code: {StatusCode}", string.Join(",", nodeIds), response.StatusCode);
            return new Dictionary<long, string>();
        }

        var jsonResponse = await response.Content.ReadAsStringAsync();
        var gqlResponse = JsonSerializer.Deserialize<GraphQLResponse>(jsonResponse, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        var nodeTitles = new Dictionary<long, string>();
        if (gqlResponse?.Data?.Content?.ByQueryText?.Data != null)
        {
            foreach (var item in gqlResponse.Data.Content.ByQueryText.Data)
            {
                nodeTitles[item.Id] = item.NodeName;
            }
        }

        return nodeTitles;
    }


    public static async Task<List<DataItem>?> GetContentItems(Uri graphqlEndpoint, string query)
    {
        if (_httpClient == null)
        {
            throw new InvalidOperationException("HttpClient is not initialized.");
        }

        var response = await _httpClient.PostAsync(graphqlEndpoint, new StringContent(
            JsonSerializer.Serialize(new { query }), Encoding.UTF8, "application/json"));

        if (!response.IsSuccessStatusCode)
        {
            _logger?.Error("Failed to fetch content items. Status code: {StatusCode}", response.StatusCode);
            return null;
        }

        var jsonResponse = await response.Content.ReadAsStringAsync();
        var gqlResponse = JsonSerializer.Deserialize<GraphQLResponse>(jsonResponse, _jsonSerializerOptions);

        if (gqlResponse?.Data?.Content?.ByQueryText?.Data == null)
        {
            _logger?.Error("Failed to deserialize GraphQL response");
            return null;
        }

        var contentItems = new List<DataItem>();
        foreach (var item in gqlResponse.Data.Content.ByQueryText.Data)
        {
            var propertiesDictionary = ParseDynamicProperties(item.Properties);

            var dataItem = new DataItem
            {
                Id = item.Id,
                NodeName = item.NodeName,
                Path = item.Path?.Split(',') ?? Array.Empty<string>(),
                Properties = propertiesDictionary
            };

            contentItems.Add(dataItem);
        }

        return contentItems;
    }

    private static Dictionary<string, object> ParseDynamicProperties(JsonElement properties)
    {
        var parsedProperties = new Dictionary<string, object>();

        foreach (var prop in properties.EnumerateObject())
        {
            if (prop.Value.ValueKind == JsonValueKind.Object)
            {
                parsedProperties[prop.Name] = ParseDynamicProperties(prop.Value);
            }
            else if (prop.Value.ValueKind == JsonValueKind.Array)
            {
                parsedProperties[prop.Name] = ParseArrayProperties(prop.Value);
            }
            else
            {
                parsedProperties[prop.Name] = prop.Value.ToString();
            }
        }

        return parsedProperties;
    }

    private static List<object> ParseArrayProperties(JsonElement arrayElement)
    {
        var list = new List<object>();
        foreach (var item in arrayElement.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.Object)
            {
                list.Add(ParseDynamicProperties(item));
            }
            else
            {
                list.Add(item.ToString());
            }
        }
        return list;
    }

    public static async Task DownloadFile(string fileUrl, string outputPath)
    {
        if (_httpClient == null)
        {
            throw new InvalidOperationException("HttpClient is not initialized.");
        }

        try
        {
            var response = await _httpClient.GetAsync(fileUrl);

            if (response.IsSuccessStatusCode)
            {
                byte[] fileBytes = await response.Content.ReadAsByteArrayAsync();
                await File.WriteAllBytesAsync(outputPath, fileBytes);
                _logger?.Information("Successfully downloaded file to {OutputPath}", fileUrl, outputPath);
            }
            else
            {
                _logger?.Error("Failed to download file from {FileUrl}. Status code: {StatusCode}", fileUrl, response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger?.Error(ex, "Error occurred while downloading file from {FileUrl}", fileUrl);
        }
    }
}
