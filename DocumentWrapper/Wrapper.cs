using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

namespace FunctionWrapper
{
    public static class Wrapper
    {
        public static CloudStorageAccount storageAccount = new CloudStorageAccount(new Microsoft.WindowsAzure.Storage.Auth.StorageCredentials(
            "name", "access key"), true);
        public static CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
        public static CloudBlobContainer container = blobClient.GetContainerReference("mycontainer");
        public static CloudBlob blob = container.GetBlobReference("1.txt");

        [FunctionName("HttpStart")]
        public static async Task<HttpResponseMessage> HttpStart(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")]HttpRequestMessage req,
            [OrchestrationClient]DurableOrchestrationClient starter,
            ILogger log)
        {
            dynamic eventData = await req.Content.ReadAsAsync<object>();
            string instanceId = await starter.StartNewAsync("Orchestrator", eventData);
            log.LogDebug($"Started orchestration with ID = '{instanceId}'.");
            return starter.CreateCheckStatusResponse(req, instanceId);
        }

        [FunctionName("Orchestrator")]
        public static async Task<string> Run([OrchestrationTrigger] DurableOrchestrationContext context, ILogger log)
        {
            string path = context.GetInput<string>();
            var tasks = new Task<string>[2];
            string extractData = string.Empty;

            tasks[0] = context.CallActivityAsync<string>("ValidateDocument", string.Empty);
            tasks[1] = context.CallActivityAsync<string>("ExtractDocument", string.Empty);
            await Task.WhenAll(tasks);

            log.LogDebug(tasks[0].Result.ToString());
            if (tasks[0].Result.ToString() == "Success")
            {
                extractData = tasks[1].Result.ToString();
                if (!string.IsNullOrEmpty(extractData))
                {
                    return tasks[1].Result.ToString();
                }
            }

            return "Failure Occured";
        }

        [FunctionName("ValidateDocument")]
        public static string ValidateDocument([ActivityTrigger] DurableActivityContext context)
        {
            string content = string.Empty;

            using (var memoryStream = new MemoryStream())
            {
                blob.DownloadToStream(memoryStream);
                content = System.Text.Encoding.UTF8.GetString(memoryStream.ToArray());
            }

            if (content.Contains("Hexaware"))
            {
                return "Success";
            }

            return "Failed";
        }

        [FunctionName("ExtractDocument")]
        public static string ExtractDocument([ActivityTrigger] DurableActivityContext context)
        {
            string content = string.Empty;
            using (var memoryStream = new MemoryStream())
            {
                blob.DownloadToStream(memoryStream);
                content = System.Text.Encoding.UTF8.GetString(memoryStream.ToArray());
            }

            return content;
        }
    }
}
