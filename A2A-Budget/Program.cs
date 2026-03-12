using A2A;
using A2A.AspNetCore;
using Azure.AI.OpenAI;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Extensions.AI;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddLogging(config =>
{
    config.AddConsole();
    config.AddDebug();
});

var logger = builder.Services.BuildServiceProvider().GetRequiredService<ILogger<Program>>();
logger.LogInformation("A2A Budget Agent starting...");

builder.Services.AddOpenApi();
builder.Services.AddSwaggerGen();

string endpoint = builder.Configuration["AZURE_OPENAI_ENDPOINT"]
    ?? throw new InvalidOperationException("AZURE_OPENAI_ENDPOINT is not set.");

logger.LogInformation("Azure OpenAI endpoint configured: {Endpoint}", endpoint);

string deploymentName = builder.Configuration["AZURE_FOUNDRY_PROJECT_DEPLOYMENT_NAME"] ?? "gpt-4.1-mini";

logger.LogInformation("Azure deployment name configured: {DeploymentName}", deploymentName);

string apiKey = builder.Configuration["AZURE_API_KEY"]
    ?? throw new InvalidOperationException("AZURE_API_KEY is not set.");

logger.LogDebug("Azure API key loaded successfully");

// Register the chat client
IChatClient chatClient = new AzureOpenAIClient(
        new Uri(endpoint),
        new System.ClientModel.ApiKeyCredential(apiKey))
    .GetChatClient(deploymentName)
    .AsIChatClient();

builder.Services.AddSingleton(chatClient);

const string systemInstructions = "You help to extract information from a software enginier project that a client wants to be build. " +
    "You should be able to extract budget and goal of the project. You don't need ask more than these 2 questions." +
    "You should be able to return this information structure to another agent through A2A.";

var discoveryAgent = builder.AddAIAgent("project-descovery", instructions: systemInstructions);
logger.LogInformation("AI agent created successfully");

var app = builder.Build();


app.MapOpenApi();
app.UseSwagger();
app.UseSwaggerUI();


AgentCard agentCard = new()
{
    Name = "Project Discovery Agent",
    Description = "An Agent that helps to extract information from the user related to software engineer project",
    Version = "1.0",
    Capabilities = new() { Streaming = true }
};


// expondo o agent
// exposing the agent
app.MapA2A(discoveryAgent, "/a2a/discovery", agentCard: agentCard);
logger.LogInformation("A2A endpoint mapped successfully at /a2a/discovery");


// isso tem que existir para que um agente possa se comunicar com o outro inicial.
// this has to exist for one agent to be able to communicate with the other initial agent.
logger.LogInformation("Mapping agent-card.json endpoint...");
app.MapGet(".well-known/agent-card.json", () => {

    logger.LogDebug("Agent card requested via .well-known/agent-card.json");
    var json = File.ReadAllText("agent-card.json");

    return json;
});
logger.LogInformation("Agent card endpoint mapped successfully");

logger.LogInformation("Starting A2A Budget Agent application...");
app.Run();
logger.LogInformation("A2A Budget Agent stopped");