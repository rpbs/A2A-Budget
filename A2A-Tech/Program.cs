using A2A;
using A2A.AspNetCore;
using Azure.AI.OpenAI;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Extensions.AI;
using A2A_Tech.Services;
using Microsoft.Extensions.Caching.Memory;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddSwaggerGen();

string endpoint = builder.Configuration["AZURE_OPENAI_ENDPOINT"]
    ?? throw new InvalidOperationException("AZURE_OPENAI_ENDPOINT is not set.");

string deploymentName = builder.Configuration["AZURE_FOUNDRY_PROJECT_DEPLOYMENT_NAME"] ?? "gpt-4.1-mini";

string apiKey = builder.Configuration["AZURE_API_KEY"]
    ?? throw new InvalidOperationException("AZURE_API_KEY is not set.");

// Register the chat client
IChatClient chatClient = new AzureOpenAIClient(
        new Uri(endpoint),
        new System.ClientModel.ApiKeyCredential(apiKey))
    .GetChatClient(deploymentName)
    .AsIChatClient();

builder.Services.AddSingleton(chatClient);

// Register pricing service and HttpClient factory
builder.Services.AddHttpClient();
builder.Services.AddSingleton<IMemoryCache, MemoryCache>();
builder.Services.AddSingleton<IPricingService, AzureRetailPricingService>();

const string systemInstructions = 
    "You are an agent responsible for creating budget scopes for applications that will be hosted on Azure. " +
    "based on budget (monthly) value information and project objectives (goals of it), " +
    "you will need to analyze the complexity, select the Azure services necessary to achieve the client's goals, and return this information to the user of how budget will be spent on each azure service per month. " +
    "to retrieve pricing information for Azure services, call the GET /pricing endpoint with query parameters: 'service' ('Virtual Machines', 'SQL Database', ...) and 'region' ('westus', 'eastus', ...). " +
    "At the end use the pricing data and the budget to build accurate budget scopes for the recommended services. " +
    "Provide the exact pricing numbers for each service tier and help with an estimated monthly cost breakdown.";

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

// isso disponibliza os endpoints que são necessários para que o agente possa se comunicar com outros agentes.
app.MapA2A(discoveryAgent, "/a2a/tech", agentCard: agentCard);

app.MapGet(".well-known/agent-card.json", () => {

    var json = File.ReadAllText("agent-card.json");

    return json;
});

// Simple pricing endpoint for agents or tools to query retail prices.
// Example: GET /pricing?service=Virtual%20Machines&region=westus
app.MapGet("/pricing", async (IPricingService pricingService, string service, string region) =>
{
    if (string.IsNullOrWhiteSpace(service) || string.IsNullOrWhiteSpace(region))
    {
        return Results.BadRequest(new { error = "query parameters 'service' and 'region' are required" });
    }

    var price = await pricingService.GetUnitPriceAsync(service, region);
    if (price is null)
    {
        return Results.NotFound(new { service, region });
    }

    return Results.Ok(new { service, region, unitPrice = price });
});

app.Run();