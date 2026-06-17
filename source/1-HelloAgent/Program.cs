using Azure;
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OpenAI;
using OpenAI.Chat;

var config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: false)
    .AddJsonFile("appsettings.Development.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

string instructions = """
You're a frozen yogurt (froyo) recommendation agent for Froyo Foundry, perveyors of the finest frozen yogurt the world, catering the to tastes of
software engineers. You have deep knowledge of all things froyo, including flavors, toppings, and pairings. You are friendly, enthusiastic,
and always ready to help customers find their perfect froyo match. You can also provide fun facts about frozen yogurt and share
the latest promotions at Froyo Foundry. Your goal is to create a delightful and personalized froyo experience for every customer.

Your available flavors include:
- Mint Condition
- Berry Blockchain Blast
- Cookie Container
- Recursive Raspberry
- Vanilla Exception
- Null Pointer Pistachio
- Java Jolt
- Peanut Butter Protocol
- Cloud Caramel Cache
- AIçaí Bowl
""";

// Use GitHub Models for development if available, but fall back to Azure OpenAI if not. 
// This allows us to develop without needing Azure OpenAI credentials, and also provides a more cost effective way to develop and test our agent.
// Read OpenAI configuration
// var endpoint = config["OpenAI:Endpoint"]
//     ?? "https://models.github.ai/inference";
// var deploymentName = config["OpenAI:DeploymentName"] ?? "openai/gpt-5-chat";

// string githubkey = config["OpenAI:Key"] ?? throw new InvalidOperationException("OpenAI:Key is not configured.");

// // Create the agent configuration using OpenAI provider and GitHub models
// //  (this is just for demonstration; in a real scenario, you'd use Azure OpenAI or another provider)
// AIAgent agent = new OpenAIClient(
//                     credential: new AzureKeyCredential(githubkey),
//                     options: new OpenAIClientOptions{ Endpoint = new Uri(endpoint) })
//     .GetChatClient(deploymentName)
//     .AsAIAgent(instructions: instructions, name: "FroyoRecommender");

var endpoint = config["AzureOpenAI:Endpoint"]
    ?? throw new InvalidOperationException("AzureOpenAI:Endpoint is not configured.");
var deploymentName = config["AzureOpenAI:DeploymentName"] ?? "gpt-5.2-chat";

// Create the agent configuration
AIAgent agent = new AzureOpenAIClient(
    endpoint: new Uri(endpoint),
    credential: new AzureCliCredential())
    .GetChatClient(deploymentName)
    .AsIChatClient()
    .AsAIAgent(
        instructions: instructions,
        name: "FroyoRecommender");


// Invoke the agent
// Console.WriteLine(await agent.RunAsync("I'm alergic to peanuts. What do you recommend?"));

// Invoke with streaming

// Create a session to maintain context across interactions
AgentSession session = await agent.CreateSessionAsync();

Console.WriteLine("Ask FroyoRecommender anything. Type 'exit' or press Enter on an empty line to quit.\n");

while (true)
{
    Console.Write("You: ");
    var input = Console.ReadLine();

    if (string.IsNullOrWhiteSpace(input) || input.Equals("exit", StringComparison.OrdinalIgnoreCase) || input.Equals("quit", StringComparison.OrdinalIgnoreCase))
        break;

    Console.Write("FroyoRecommender: ");
    await foreach (var chunk in agent.RunStreamingAsync(input, session))
    {
        Console.Write(chunk);
    }
    Console.WriteLine("\n");
}