using Azure.AI.OpenAI;
using Azure.Identity;
using FroyoFoundry.AIAgent.Tools;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;

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
""";

// Create the agent configuration
AIAgent agent = new AzureOpenAIClient(
    endpoint: new Uri(endpoint),
    credential: new AzureCliCredential())
    .GetChatClient(deploymentName)
    .AsIChatClient()
    .AsAIAgent(
        instructions: instructions,
        name: "FroyoRecommender",
        tools:
         [
            AIFunctionFactory.Create(ListFlavorsTool.ListFlavors)
        ]);


// Add middleware to filter out forbidden words from both input and output.  This is just a simple example
// of how you can intercept and modify the messages going to and from the agent.
var agentWithMiddleware = agent
    .AsBuilder()
    .Use(runFunc: GuardMiddleware, runStreamingFunc: null)  // Using the non-streaming for handling streaming as well
    .Build();


// Invoke the agent
// Console.WriteLine(await agent.RunAsync("I'm alergic to peanuts. What do you recommend?"));

// Invoke with streaming

// Create a session to maintain context across interactions
AgentSession session = await agentWithMiddleware.CreateSessionAsync();

Console.WriteLine("Ask FroyoRecommender anything. Type 'exit' or press Enter on an empty line to quit.\n");

while (true)
{
    Console.Write("You: ");
    var input = Console.ReadLine();

    if (string.IsNullOrWhiteSpace(input) || input.Equals("exit", StringComparison.OrdinalIgnoreCase) || input.Equals("quit", StringComparison.OrdinalIgnoreCase))
        break;

    Console.Write("FroyoRecommender: ");
    await foreach (var chunk in agentWithMiddleware.RunStreamingAsync(input, session))
    {
        Console.Write(chunk);
    }
    Console.WriteLine("\n");
}


async Task<AgentResponse> GuardMiddleware(IEnumerable<ChatMessage> messages, AgentSession? session, AgentRunOptions? options, AIAgent innerAgent, CancellationToken cancellationToken)
{
    // Remove certain words from the user input as a simple guard example.  If the prompt contains any of the forbidden words, we can return a custom response instead of invoking the inner agent.
    var forbiddenWords = new[] { "badword1", "badword2" };
    
    Console.WriteLine("GuardMiddleware: Checking for forbidden words...");

    var filteredMessages = FilteredMessages(messages, forbiddenWords);

    Console.WriteLine("GuardMiddleware: Filtered messages:");
    foreach (var msg in filteredMessages)    {
        Console.WriteLine($"- {msg.Role}: {msg.Text}");
    }

    // Proceed with the inner agent run
    var response = await innerAgent.RunAsync(filteredMessages, session, options, cancellationToken);

    // Check the output
    response.Messages = FilteredMessages(response.Messages, forbiddenWords);

    return response;

    List<ChatMessage> FilteredMessages(IEnumerable<ChatMessage> messages, string[] forbiddenWords)
    {
        return messages.Select(m => new ChatMessage(m.Role, FilterContent(m.Text, forbiddenWords))).ToList();
    }

    static string FilterContent(string content, string[] forbiddenWords)
    {
        foreach (var keyword in forbiddenWords)
        {
            if (content.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                return "[REDACTED]: Forbidden content removed.";
            }
        }

        return content;
    }

}