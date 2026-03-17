using Azure.AI.OpenAI;
using Azure.Identity;
using FroyoFoundry.AIAgent.Models;
using FroyoFoundry.AIAgent.Tools;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
// using OpenAI.Chat;

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

// Order intake agent
string orderIntakeInstructions = """
    You are the Order Intake Agent for Froyo Foundry.

    Your job is to process incoming orders, validate them against business rules, and produce a structured output that can be used by downstream agents in the order processing workflow.

    ## Responsibilities

    1. Validate incoming order data for required fields and correct formatting.
    2. Check orders against business rules (e.g., max quantity limits, restricted products).
    3. Produce a canonical order object that includes all necessary information for fulfillment.
    4. If the order violates any rules, produce a clear and specific error message indicating the issue.

    ## Business Rules

    - Maximum quantity per item is 10.
    - Minimum quantity per item is 1.
    - Restricted products include "Rainbow Sherbet" and "Chocolate Chip Cookie Dough".
    - Orders must include the name or flavor ID of the product and how many of each item.

    ## Output Requirements

    Return valid JSON only.

    For valid orders, structure your response as follows:

    {
        "isValid": true,
        "order": {
            "orderId": "string",
            "customerName": {
                "firstName": "string",
                "middleName": "string or null",
                "lastName": "string"
            },
            "customerEmail": "string",
            "shippingAddress": {
                "streetAddress": "string",
                "addressLine2": "string or null",
                "city": "string",
                "state": "string",
                "zipCode": "string"
            },
            "lineItems": [
                {
                    "flavorId": "string",
                    "quantity": 0
                }
            ]
        },
        "errorMessage": null
    }

    For invalid orders, structure your response as follows:

    {
        "isValid": false,
        "order": null,
        "errorMessage": "string describing the validation error"
    }
""";

AIAgent orderIntakeAgent = new AzureOpenAIClient(
    endpoint: new Uri(endpoint),
    credential: new AzureCliCredential())
    .GetChatClient(deploymentName)
    .AsIChatClient()
    .AsAIAgent(
        options: new ChatClientAgentOptions()
        {
            Name = "OrderIntakeAgent",
            ChatOptions = new ()
            {
                Instructions = orderIntakeInstructions,
                ResponseFormat = ChatResponseFormat.ForJsonSchema<OrderIntakeResult>(
                    schemaDescription: "The result of validating and processing an incoming order, including a canonical order object if valid or an error message if invalid.",
                    schemaName: "OrderIntakeResult")
            }
        });



// Fulfillment decision agent

// Customer messaging agent


// Create the workflow (as an agent?)

var workflow = AgentWorkflowBuilder.BuildSequential([orderIntakeAgent]);


// Invoke with streaming

// Create a session to maintain context across interactions
AgentSession session = await agent.CreateSessionAsync();

Console.WriteLine("Please enter your Froyo Foundry order.");

var input = Console.ReadLine();

await using StreamingRun run = await InProcessExecution.RunStreamingAsync(workflow, new ChatMessage(ChatRole.User, input));
await run.TrySendMessageAsync(new TurnToken(emitEvents: true));

string? lastExecutorId = null;
List<ChatMessage> result = [];
await foreach (WorkflowEvent evt in run.WatchStreamAsync())
{
    if (evt is AgentResponseUpdateEvent e)
    {
        if (e.ExecutorId != lastExecutorId)
        {
            lastExecutorId = e.ExecutorId;
            Console.WriteLine();
            Console.Write($"{e.ExecutorId}: ");
        }

        Console.Write(e.Update.Text);
    }
    else if (evt is WorkflowOutputEvent outputEvt)
    {
        result = outputEvt.As<List<ChatMessage>>()!;
        break;
    }
}

// Display final result
Console.WriteLine();
foreach (var message in result)
{
    // Console.WriteLine($"{message.Role}: {message.Contents}");
    foreach (var content in message.Contents)
    {
        Console.WriteLine($"{message.Role}: {content}");
    }
}