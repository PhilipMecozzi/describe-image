namespace ImageProcessing;

using Azure.Messaging.EventGrid;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision.Models;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.EventGrid;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

public class describe_uploaded_image
{
    private readonly HttpClient httpClient;

    public describe_uploaded_image(IHttpClientFactory httpClientFactory)
    {
        this.httpClient = httpClientFactory.CreateClient();
    }

    [FunctionName("describe_uploaded_image")]
    public async Task Run(
        [EventGridTrigger]EventGridEvent eventGridEvent
        , ILogger logger
    ) {
        using (logger.BeginScope(eventGridEvent.Id))
        {
            logger.LogInformation($"Processing {eventGridEvent.EventType} event for: {eventGridEvent.Subject}");

            string storageConnectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage", EnvironmentVariableTarget.Process);
            string computerVisionEndpoint = Environment.GetEnvironmentVariable("ComputerVisionEndpoint", EnvironmentVariableTarget.Process);
            string computerVisionSubscriptionKey = Environment.GetEnvironmentVariable("ComputerVisionSubscriptionKey", EnvironmentVariableTarget.Process);

            string url = GetValueOrEmptyString(eventGridEvent.Data.ToObjectFromJson<JsonElement>(), "url");
            var (metadata, thumbnail) = GenerateMetadataAndThumbnail(logger, httpClient, computerVisionEndpoint, computerVisionSubscriptionKey, url);
            await SetBlobMetadataAndSaveThumbnail(logger, storageConnectionString, url, metadata, thumbnail);
        }
    }

    private static string GetValueOrEmptyString(
        JsonElement json
        , string propertyName
    ) {
        if (json.TryGetProperty(propertyName, out JsonElement element))
        {
            return element.ToString() ?? string.Empty;
        }
        return string.Empty;
    }

    private static (Task<IDictionary<string, string>> metadata, Task<Stream> thumbnail) GenerateMetadataAndThumbnail(
        ILogger logger
        , HttpClient httpClient
        , string endpoint
        , string subscriptionKey
        , string imageUrl
    ) {
        // string caption = "This image does not prominently feature a car.";

        var credentials = new ApiKeyServiceClientCredentials(subscriptionKey);
        using var client = new ComputerVisionClient(credentials, httpClient, false);
        client.Endpoint = endpoint;
        
        return (GenerateMetadata(logger, client, imageUrl), GenerateThumbnail(logger, client, imageUrl));

        static async Task<IDictionary<string, string>> GenerateMetadata(
            ILogger logger
            , ComputerVisionClient client
            , string url
        ) {
            ImageDescription description = await client.DescribeImageAsync(url);
            var dictionary = new Dictionary<string, string>();

            if (description?.Tags is not null
                && description.Tags.Any()) {
                    dictionary.Add("tags", string.Join(',', description.Tags));
            }

            if (description?.Captions is not null
                && description.Captions.Any()) {
                    dictionary.Add("caption", Uri.EscapeDataString(description.Captions.First().Text));
            }

            using (logger.BeginScope(dictionary))
            {
                logger.LogInformation($"Metadata generated for {url}");
            }

            return dictionary;
        }

        static Task<Stream> GenerateThumbnail(
            ILogger logger
            , ComputerVisionClient client
            , string url
        ) {
            const int width = 100;
            const int height = 100;
            const bool smartCropping = true;

            var thumbnail = client.GenerateThumbnailAsync(width, height, url, smartCropping);

            using (logger.BeginScope(new Dictionary<string, object>{
                [nameof(width)] = width,
                [nameof(height)] = height,
                [nameof(smartCropping)] = smartCropping
            }))
            {
                logger.LogInformation($"Thumbnail generated for {url}");
            }

            return thumbnail;
        }
    }

    private static Task SetBlobMetadataAndSaveThumbnail(
        ILogger logger
        , string connectionString
        , string url
        , Task<IDictionary<string, string>> metadata
        , Task<Stream> thumbnail
    ) {
        var blob = new BlobClient(new Uri(url));

        return Task.WhenAll(new[] {
            UpdateBlobWithMetadata(logger, connectionString, blob.BlobContainerName, blob.Name, metadata)
            , SaveBlobThumbnail(logger, url, connectionString, blob.BlobContainerName, blob.Name, thumbnail)
        });
    }

    private static async Task UpdateBlobWithMetadata(
        ILogger logger
        , string connectionString
        , string containerName
        , string blobName
        , Task<IDictionary<string, string>> metadata
    ) {
        var client = new BlobClient(connectionString, containerName, blobName);
        Azure.Response<BlobInfo> response = await client.SetMetadataAsync(await metadata);
        logger.LogInformation($"{blobName} blob updated at {response.Value.LastModified}");
    }

    private static async Task SaveBlobThumbnail(
        ILogger logger
        , string url
        , string connectionString
        , string containerName
        , string blobName
        , Task<Stream> thumbnail
    ) {
        string thumbnailContainerName = $"{containerName}-thumbnails";
        var options = new BlobUploadOptions() {
            Metadata = new Dictionary<string, string>() {{ "thumbnailof", url }}
        };
        var client = new BlobClient(connectionString, thumbnailContainerName, blobName);
        Azure.Response<BlobContentInfo> response = await client.UploadAsync(await thumbnail, options);
        logger.LogInformation($"{blobName} uploaded to the {thumbnailContainerName} container at {response.Value.LastModified}");
    }

}
