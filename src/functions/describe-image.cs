using System;
using System.IO;
using System.Collections.Generic;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;

namespace ImageProcessing
{
    // Decided to go with EventGrid as Microsoft's guidance was that BlobTriggers weren't 100% guaranteed to fire for every blob change.
    
    // public class describe_image
    // {
    //     [FunctionName("describe_image")]
    //     public void Run([BlobTrigger("describe-images/{name}.{extension}", Connection = "mecozzidemo_STORAGE")]Stream theBlob
    //     , string name
    //     , string extension
    //     , IDictionary<string, string> metadata
    //     , ILogger logger)
    //     {
    //         logger.LogInformation($"C# Blob trigger function Processed blob\n Name:{name}.{extension}\n Size: {theBlob.Length} Bytes\n Metadata: {metadata}");
    //     }
    // }
}
