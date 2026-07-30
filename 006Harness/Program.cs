using System.ClientModel.Primitives;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Azure.AI.Extensions.OpenAI;
using Azure.AI.Projects;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using OpenAI;
using OpenAI.Chat;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
IConfiguration config = builder.Configuration;

string azureEndpoint = config["FOUNDRY_PROJECT_ENDPOINT"] ?? throw new ApplicationException("Set FOUNDRY_PROJECT_ENDPOINT");
Console.WriteLine($"Azure endpoint: {azureEndpoint}");

string deploymentName = config["FOUNDRY_MODEL"] ?? throw new ApplicationException("Set FOUNDRY_MODEL");
Console.WriteLine($"Deployment name: {deploymentName}");

#pragma warning disable OPENAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
IChatClient chatClient = new AIProjectClient(new Uri(azureEndpoint), new DefaultAzureCredential())
    .GetProjectOpenAIClient()
    .GetResponsesClient()
    .AsIChatClient(deploymentName);
#pragma warning restore OPENAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

AIAgent agent = new HarnessAgent(chatClient, new HarnessAgentOptions() 
{
    DisableWebSearch = true
});

var session = await agent.CreateSessionAsync();

Console.WriteLine("Harness agent ready. Type 'exit' to quit.");
while (true)
{
    Console.Write("> ");
    string? input = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(input) || input.Equals("exit", StringComparison.OrdinalIgnoreCase))
    {
        break;
    }

    await foreach (var update in agent.RunStreamingAsync(input, session))
    {
        Console.Write(update);
    }
    Console.WriteLine();
}