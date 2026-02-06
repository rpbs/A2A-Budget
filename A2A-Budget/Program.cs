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

const string systemInstructions = "You help to extract information from a software enginier project that a client wants to be build. " +
    "You should be able to extract budget and goal of the project. You don't need ask more than these 2 questions." +
    "You should be able to return this information structure to another agent through A2A.";

var discoveryAgent = builder.AddAIAgent("project-descovery", instructions: systemInstructions);

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


// isso tem que existir para que um agente possa se comunicar com o outro inicial.
// this has to exist for one agent to be able to communicate with the other initial agent.
app.MapGet(".well-known/agent-card.json", () => {

    var json = File.ReadAllText("agent-card.json");

    return json;
});

app.Run();