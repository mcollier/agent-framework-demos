using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;

var config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: false)
    .AddJsonFile("appsettings.Development.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

// Read Azure OpenAI configuration
var endpoint = config["AzureOpenAI:Endpoint"]
    ?? throw new InvalidOperationException("AzureOpenAI:Endpoint is not configured.");
var deploymentName = config["AzureOpenAI:DeploymentName"] ?? "gpt-4o-mini";

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

// Create the agent configuration
AIAgent agent = new AzureOpenAIClient(
    endpoint: new Uri(endpoint),
    credential: new AzureCliCredential())
    .GetChatClient(deploymentName)
    .AsAIAgent(instructions: instructions, name: "FroyoRecommender");


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