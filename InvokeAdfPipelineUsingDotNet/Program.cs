using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Management.DataFactory;
using Microsoft.Azure.Management.DataFactory.Models;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Microsoft.Rest;
using Newtonsoft.Json.Linq;

namespace InvokeAdfPipeline
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // Load configuration from appsettings.json
            var config = LoadConfiguration("appsettings.json");
            string tenantId = config["tenant_id"].ToString();
            string clientId = config["client_id"].ToString();
            string clientSecret = config["client_secret"].ToString();
            string subscriptionId = config["subscription_id"].ToString();
            string resourceGroupName = config["resource_group_name"].ToString();
            string factoryName = config["factory_name"].ToString();
            string pipelineName = config["pipeline_name"].ToString();

            // Auth using the service principal
            TokenCredentials tokenCredentials = await GetTokenCredentials(tenantId, clientId, clientSecret);

            // Create the Data Factory management client
            var adfClient = new DataFactoryManagementClient(tokenCredentials)
            {
                SubscriptionId = subscriptionId
            };

            // Trigger the pipeline run
            CreateRunResponse runResponse = await adfClient.Pipelines.CreateRunAsync(resourceGroupName, factoryName, pipelineName);
            Console.WriteLine($"Pipeline run initiated. Run ID: {runResponse.RunId}");

            // Poll for pipeline run status until it completes
            string runId = runResponse.RunId;
            PipelineRun pipelineRun;
            do
            {
                pipelineRun = await adfClient.PipelineRuns.GetAsync(resourceGroupName, factoryName, runId);
                Console.WriteLine($"Current pipeline status: {pipelineRun.Status}");
                Thread.Sleep(2000); // Wait 10 seconds before polling again
            }
            while (pipelineRun.Status != "Succeeded" &&
                   pipelineRun.Status != "Failed" &&
                   pipelineRun.Status != "Cancelled");

            Console.WriteLine("Pipeline run completed.");
        }

        // Reads configuration from a JSON file.
        private static JObject LoadConfiguration(string filePath)
        {
            string jsonContent = File.ReadAllText(filePath);
            return JObject.Parse(jsonContent);
        }

        // Authenticates using a service principal and returns token credentials.
        private static async Task<TokenCredentials> GetTokenCredentials(string tenantId, string clientId, string clientSecret)
        {
            string authority = $"https://login.microsoftonline.com/{tenantId}";
            var authContext = new AuthenticationContext(authority);
            var credential = new ClientCredential(clientId, clientSecret);
            AuthenticationResult authResult = await authContext.AcquireTokenAsync("https://management.azure.com/", credential);
            return new TokenCredentials(authResult.AccessToken);
        }
    }
}