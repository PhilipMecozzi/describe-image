// Default URL for triggering event grid function in the local environment.
// http://localhost:7071/runtime/webhooks/EventGrid?functionName={functionname}
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Azure.Messaging.EventGrid;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
//using Microsoft.Azure.EventGrid;
using Microsoft.Azure.Storage.Blob;
using Microsoft.Azure.WebJobs.Extensions.EventGrid;
using Microsoft.Extensions.Logging;

namespace ImageProcessing
{
    public static class describe_uploaded_image
    {
        [FunctionName("describe_uploaded_image")]
        public static async void Run(
            [EventGridTrigger]EventGridEvent eventGridEvent
            , ILogger logger
        ) {
            logger.LogInformation($"Processing {eventGridEvent.EventType} event: {eventGridEvent.Subject}");
            string url = GetValueOrEmptyString(eventGridEvent.Data.ToObjectFromJson<JsonElement>(), "url");
            logger.LogInformation(url);
            string connectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage", EnvironmentVariableTarget.Process);
            await UpdateBlobWithDescription("test_description", url);
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

        private static async Task<DateTimeOffset> UpdateBlobWithDescription(
            string description
            , string url
            // , string containerName
            // , string blobName
            // , string connectionString
        ) {
            //BlobServiceClient serviceClient = new BlobServiceClient(connectionString);
            //BlobContainerClient containerClient = serviceClient.GetBlobContainerClient(containerName);
            //BlobClient client = containerClient.GetBlobClient(blobName);
            BlobClient client = new BlobClient(new Uri(url));

            Dictionary<string, string> metadata = new Dictionary<string, string>() {
                ["description"] = description
            };
            
            Azure.Response<BlobInfo> response = await client.SetMetadataAsync(metadata);
            return response.Value.LastModified;
        }
    }
}
