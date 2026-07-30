using A2A.AspNetCore;
using Azure.AI.Projects;
using Azure.Identity;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Agents.AI.Hosting.A2A;
using Microsoft.Extensions.AI;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
var config = builder.Configuration;

string azureEndpoint = config["FOUNDRY_PROJECT_ENDPOINT"] ?? throw new ApplicationException("Set FOUNDRY_PROJECT_ENDPOINT");
Console.WriteLine($"Azure endpoint: {azureEndpoint}");

string deploymentName = config["FOUNDRY_MODEL"] ?? throw new ApplicationException("Set FOUNDRY_MODEL");
Console.WriteLine($"Deployment name: {deploymentName}");


#pragma warning disable OPENAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
IChatClient chatClient = new AIProjectClient(
        new Uri(azureEndpoint),
        new DefaultAzureCredential())
    .GetProjectOpenAIClient()
    .GetProjectResponsesClient()
    .AsIChatClient(deploymentName);
#pragma warning restore OPENAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

builder.Services.AddKeyedChatClient("chat-model", chatClient);

builder.AddAIAgent(
        "pirate",
        instructions: "You are a pirate. Speak like a pirate.",
        description: "An agent that speaks like a pirate",
        chatClientServiceKey: "chat-model"
    )
    .WithInMemorySessionStore(withIsolation: false);

builder.AddA2AServer("pirate");

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapA2AHttpJson("pirate", "/pirate");

app.Map(
    "/debug",
    async (
        [FromKeyedServices("pirate")] AIAgent agent,
        CancellationToken cancellationToken) =>
    {
        AgentResponse response = await agent.RunAsync(
            "Introduce yourself",
            cancellationToken: cancellationToken);

        return Results.Text(response.Text);
    });

app.Map("/", () =>
{
    return new { Value = "Hello"};
});

app.Run();


