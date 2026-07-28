using Azure.AI.Projects;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

Console.WriteLine("Hello, World!");

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
IConfiguration config = builder.Configuration;

string azureEndpoint = config["FOUNDRY_PROJECT_ENDPOINT"] ?? throw new ApplicationException("Set FOUNDRY_PROJECT_ENDPOINT");
Console.WriteLine($"Azure endpoint: {azureEndpoint}");

string deploymentName = config["FOUNDRY_MODEL"] ?? throw new ApplicationException("Set FOUNDRY_MODEL");
Console.WriteLine($"Deployment name: {deploymentName}");

AIAgent agent = new AIProjectClient(new Uri(azureEndpoint), new DefaultAzureCredential())
    .AsAIAgent(
        model: deploymentName,
        instructions: "You are good at telling jokes.",
        name: "Joker"
    );

Console.WriteLine(await agent.RunAsync("Tell me a joke about a pirate."));

await foreach (var update in agent.RunStreamingAsync("Tell me a dad joke."))
{
    Console.Write(update);
}
