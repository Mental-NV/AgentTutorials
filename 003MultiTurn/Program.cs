using System.Diagnostics;
using Azure.AI.Projects;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
IConfiguration config = builder.Configuration;

string azureEndpoint = config["FOUNDRY_PROJECT_ENDPOINT"] ?? throw new ApplicationException("Set FOUNDRY_PROJECT_ENDPOINT");
Console.WriteLine($"Azure endpoint: {azureEndpoint}");

string deploymentName = config["FOUNDRY_MODEL"] ?? throw new ApplicationException("Set FOUNDRY_MODEL");
Console.WriteLine($"Deployment name: {deploymentName}");

AIAgent agent = new AIProjectClient(new Uri(azureEndpoint), new DefaultAzureCredential())
    .AsAIAgent(
        model: deploymentName,
        instructions: "You are a friendly assistant. Keep your answers brief.",
        name: "ConversatonAgent"
    );

AgentSession session = await agent.CreateSessionAsync();

Console.WriteLine(await agent.RunAsync("My name is Alice and I like hiking.", session));

Console.WriteLine(await agent.RunAsync("What do you remember about me?", session));