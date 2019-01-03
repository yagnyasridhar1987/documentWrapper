using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

namespace FunctionWrapper
{
    public static class Wrapper
    {
        public static CloudStorageAccount storageAccount = new CloudStorageAccount(
            new Microsoft.WindowsAzure.Storage.Auth.StorageCredentials("<storage-account-name>", "<access-key>"), true);
        public static CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
        public static CloudBlobContainer container = blobClient.GetContainerReference("mycontainer");
        public static CloudBlob blob = container.GetBlobReference("");

        [FunctionName("HttpStart")]
        public static async Task<HttpResponseMessage> Run([HttpTrigger(AuthorizationLevel.Anonymous, "get")]
                            HttpRequestMessage req, [OrchestrationClient] DurableOrchestrationClientBase starter, string functionName, TraceWriter log)
        {
            dynamic eventData = await req.Content.ReadAsAsync<object>();
            string instanceId = await starter.StartNewAsync("Orchestrator", eventData);
            log.Info($"Started orchestration with ID = '{instanceId}'.");
            return starter.CreateCheckStatusResponse(req, instanceId);
        }

        [FunctionName("Orchestration")]
        public static async Task<string> Run([OrchestrationTrigger] DurableOrchestrationContext context)
        {
            var tasks = new Task<string>[2];
            string extractData = string.Empty;

            tasks[0] = context.CallActivityAsync<string>("ValidateDocument", string.Empty);
            tasks[1] = context.CallActivityAsync<string>("ExtractDocument", string.Empty);
            await Task.WhenAll(tasks);

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

            using (StreamReader reader = new StreamReader(blob.OpenRead()))
            {
                content = reader.ReadToEnd();
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
            using (StreamReader reader = new StreamReader(blob.OpenRead()))
            {
                content = reader.ReadToEnd();
            }

            return content;
        }
    }
}
