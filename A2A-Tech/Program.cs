using A2A;
using A2A.AspNetCore;
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Extensions.AI;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddSwaggerGen();

string endpoint = builder.Configuration["AZURE_OPENAI_ENDPOINT"]
    ?? throw new InvalidOperationException("AZURE_OPENAI_ENDPOINT is not set.");

string deploymentName = "gpt-4.1-mini";

string apiKey = builder.Configuration["AZURE_API_KEY"]
    ?? throw new InvalidOperationException("AZURE_API_KEY is not set.");

// Register the chat client
IChatClient chatClient = new AzureOpenAIClient(
        new Uri(endpoint),
        new System.ClientModel.ApiKeyCredential(apiKey))
    .GetChatClient(deploymentName)
    .AsIChatClient();

builder.Services.AddSingleton(chatClient);

const string systemInstructions = "You are an agent responsible for creating budget scopes for applications that will be hosted on Azure. " +
    "Based on financial budget information and project objectives, " +
    "you will need to analyze the complexity, select the Azure services necessary to achieve the client's goals, and return this information to the user.";

var discoveryAgent = builder.AddAIAgent("tech-descovery", instructions: systemInstructions);

var app = builder.Build();

app.MapOpenApi();
app.UseSwagger();
app.UseSwaggerUI();

AgentCard agentCard = new()
{
    Name = "Agent for creating budget scope for projects in azure",
    Description = "An Agent that helps to create budget scopes for applications that will be hosted on Azure",
    Version = "1.0",
    Capabilities = new() { Streaming = true }
};

app.MapA2A(discoveryAgent, "/a2a/tech", agentCard: agentCard);

app.MapGet(".well-known/agent-card.json", () => {

    var json = File.ReadAllText("agent-card.json");

    return json;
});

app.Run();