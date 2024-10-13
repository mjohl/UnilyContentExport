using UnilyContentExport.Services;
using UnilyContentExport.Models;
using Serilog;
using Microsoft.Extensions.Configuration;
using System.Text.Json;

SemaphoreSlim mediaSemaphore = new SemaphoreSlim(1, 1);


Uri graphqlEndpoint;
string exportBasePath, exportMediaPath;
int batchSize, maxParallelTasks, upperLimitId;
Dictionary<long, string> nodeNameCache, mediaCache;
using var httpClient = new HttpClient();

var config = new ConfigurationBuilder()
.SetBasePath(Directory.GetCurrentDirectory())
.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
.AddJsonFile("appsettings.local.json", optional: false, reloadOnChange: true)
.Build();

SetConfigurations(config, out graphqlEndpoint, out exportBasePath, out exportMediaPath, out batchSize, out maxParallelTasks, out upperLimitId, out nodeNameCache, out mediaCache);

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.File($"{exportBasePath}\\..\\log_" + DateTime.Now.ToString("yyyyMMddHHmm") + ".txt", rollingInterval: RollingInterval.Day)
    .WriteTo.Async(x => x.Console(theme: Serilog.Sinks.SystemConsole.Themes.AnsiConsoleTheme.Sixteen))
    .CreateLogger();

await SetAuthToken(httpClient, config);

SearchService.InitSearchService(Log.Logger, config, httpClient);
IOHelper.InitIOHelper(Log.Logger);

foreach (var queryPair in config.GetSection("GraphQl:Queries").GetChildren())
{
    string nodeTypeAlias = queryPair.Key;
    string? queryTemplate = queryPair.Value;
    if (queryTemplate == null)
    {
        Log.Error("Query template for {NodeTypeAlias} is null", nodeTypeAlias);
        continue;
    }
    long lastId = upperLimitId;
    bool hasMoreData = true;

    while (hasMoreData)
    {
        string query = queryTemplate
            .Replace("{lastId}", lastId.ToString())
            .Replace("{take}", batchSize.ToString());

        Log.Information("Fetching {NodeTypeAlias} items with lastId: {LastId}", nodeTypeAlias, lastId);
        var dataItems = await SearchService.GetContentItems(graphqlEndpoint, query);

        if (dataItems == null || dataItems.Count == 0)
        {
            Log.Information("No more data for {NodeTypeAlias}. Exiting loop.", nodeTypeAlias);
            hasMoreData = false;
            break;
        }

        lastId = dataItems.Select(x => x.Id).Min() - 1;
        Log.Information("Updated lastId to: {LastId}", lastId);

        using SemaphoreSlim semaphore = new SemaphoreSlim(maxParallelTasks);

        var processingTasks = dataItems.Select(async dataItem =>
        {
            await semaphore.WaitAsync();

            try
            {
                await ProcessDataItem(dataItem, nodeTypeAlias, nodeNameCache, mediaCache, graphqlEndpoint, exportBasePath, exportMediaPath);
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(processingTasks);
    }
}

async Task ProcessDataItem(DataItem dataItem, string nodeTypeAlias, Dictionary<long, string> nodeNameCache, Dictionary<long, string> mediaCache, Uri graphqlEndpoint, string exportBasePath, string exportMediaPath)
{
    Log.Information("Processing {NodeTypeAlias} item {Id} {NodeName}", nodeTypeAlias, dataItem.Id, dataItem.NodeName);

    var pathNames = await FetchPathNames(dataItem, nodeNameCache, graphqlEndpoint);
    pathNames = pathNames.Select(IOHelper.SanitizePathName).ToArray();

    string folderPath = Path.Combine(exportBasePath, nodeTypeAlias, string.Join("\\", pathNames));
    folderPath = IOHelper.SanitizePathName(folderPath);

    IOHelper.CreateDirectoryIfNotExists(folderPath);

    string mediaFolderPath = Path.Combine(exportMediaPath, nodeTypeAlias);
    IOHelper.CreateDirectoryIfNotExists(mediaFolderPath);

    await DownloadAndCacheMedia(dataItem, mediaCache, folderPath, mediaFolderPath);

    await WriteContentToFile(dataItem, folderPath);
}


async Task WriteContentToFile(DataItem dataItem, string folderPath)
{
    string sanitizedNodeName = IOHelper.SanitizeFileName(dataItem.NodeName);
    string truncatedNodeName = IOHelper.TruncateFileName(sanitizedNodeName, 50);
    string contentFilePath = Path.Combine(folderPath, $"{dataItem.Id}_{truncatedNodeName}.json");

    IOHelper.CreateDirectoryIfNotExists(folderPath);

    await File.WriteAllTextAsync(contentFilePath, JsonSerializer.Serialize(dataItem));
}

async Task<string[]> FetchPathNames(DataItem dataItem, Dictionary<long, string> nodeNameCache, Uri graphqlEndpoint)
{
    var nodeIdsToFetch = new List<long>();
    var pathWithoutLastId = dataItem.Path.Take(dataItem.Path.Length - 1).Skip(2).ToArray();

    var pathNames = pathWithoutLastId
        .Select(pathIdString =>
        {
            if (!int.TryParse(pathIdString, out int pathId))
            {
                return string.Empty;
            }

            if (!nodeNameCache.TryGetValue(pathId, out string? nodeName))
            {
                nodeIdsToFetch.Add(pathId);
                return string.Empty;
            }

            return nodeName;
        })
        .Where(nodeName => !string.IsNullOrEmpty(nodeName))
        .ToArray();

    if (nodeIdsToFetch.Any())
    {
        var fetchedNodeTitles = await SearchService.GetNodeTitlesByIds(graphqlEndpoint, nodeIdsToFetch);
        foreach (var nodeId in fetchedNodeTitles.Keys)
        {
            nodeNameCache[nodeId] = fetchedNodeTitles[nodeId];
        }

        pathNames = pathWithoutLastId
            .Select(pathIdString =>
            {
                if (!int.TryParse(pathIdString, out int pathId))
                {
                    return string.Empty;
                }

                return nodeNameCache.TryGetValue(pathId, out string? nodeName) ? nodeName : string.Empty;
            })
            .Where(nodeName => !string.IsNullOrEmpty(nodeName))
            .ToArray();
    }

    return pathNames;
}


async Task DownloadAndCacheMedia(DataItem dataItem, Dictionary<long, string> mediaCache, string folderPath, string mediaFolderPath)
{
    var mediaUrls = ExtractMediaUrls(dataItem.Properties);

    foreach (var mediaUrl in mediaUrls)
    {
        string mediaFilePath;

        await mediaSemaphore.WaitAsync();

        try
        {
            if (!mediaCache.TryGetValue(dataItem.Id, out mediaFilePath))
            {
                mediaFilePath = Path.Combine(mediaFolderPath, Path.GetFileName(mediaUrl));
                mediaCache[dataItem.Id] = mediaFilePath;
                await SearchService.DownloadFile(mediaUrl, mediaFilePath);
            }

            UpdateMediaReference(dataItem.Properties, mediaUrl, mediaFilePath);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error downloading media for {Id} {NodeName}", dataItem.Id, dataItem.NodeName);
        }
        finally
        {
            mediaSemaphore.Release();
        }
    }
}

List<string> ExtractMediaUrls(Dictionary<string, object> properties)
{
    var mediaUrls = new List<string>();

    foreach (var property in properties)
    {
        if (property.Value is Dictionary<string, object> subProperties)
        {
            mediaUrls.AddRange(ExtractMediaUrls(subProperties));
        }
        else if (property.Key == "mediaUrl" && property.Value is string mediaUrl)
        {
            mediaUrls.Add(mediaUrl);
        }
    }

    return mediaUrls;
}

void UpdateMediaReference(Dictionary<string, object> properties, string mediaUrl, string mediaFilePath)
{
    var keysToUpdate = new List<string>();

    foreach (var property in properties)
    {
        if (property.Value is Dictionary<string, object> subProperties)
        {
            UpdateMediaReference(subProperties, mediaUrl, mediaFilePath);
        }
        else if (property.Key == "mediaUrl" && property.Value is string url && url == mediaUrl)
        {
            keysToUpdate.Add(property.Key);
        }
    }

    foreach (var key in keysToUpdate)
    {
        properties["localMediaPath"] = mediaFilePath;
    }
}

static void SetConfigurations(IConfigurationRoot config, out Uri graphqlEndpoint, out string exportBasePath, out string exportMediaPath, out int batchSize, out int maxParallelTasks, out int upperLimitId, out Dictionary<long, string> nodeNameCache, out Dictionary<long, string> mediaCache)
{
    var baseUri = new Uri(config["Unily:ApiSiteUrl"] ?? throw new ArgumentNullException("API Site URL cannot be null"));
    graphqlEndpoint = new Uri(baseUri, config["GraphQl:Endpoint"] ?? throw new ArgumentNullException("GraphQL endpoint cannot be null"));
    exportBasePath = config["GraphQl:ExportPath"] ?? throw new ArgumentNullException("Export path cannot be null");
    exportMediaPath = config["GraphQl:ExportMediaPath"] ?? throw new ArgumentNullException("Export media path cannot be null");
    batchSize = int.Parse(config["GraphQl:BatchSize"] ?? "1000");
    maxParallelTasks = int.Parse(config["GraphQl:ParallelTasks"] ?? "3");
    ;
    upperLimitId = int.Parse(config["GraphQl:UpperLimitId"] ?? "99999999");
    nodeNameCache = new Dictionary<long, string> { { -1, "Root" } };
    mediaCache = new Dictionary<long, string>();
}

static async Task SetAuthToken(HttpClient httpClient, IConfigurationRoot config)
{
    var authService = new AuthService(httpClient, config);
    string accessToken = await authService.GetAccessTokenAsync();
    httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");
}