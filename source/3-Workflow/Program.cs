using Azure.AI.OpenAI;
using Azure.Identity;
using FroyoFoundry.Workflow.Models;
using FroyoFoundry.Workflow.Tools;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
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
    - Restricted products include "Cloud Caramel Cache" and "Mint Condition".
    - Orders must include the name or flavor ID of the product and how many of each item.

    ## Output Requirements

    Return valid JSON only.

    For valid orders, structure your response as follows:

    {
        "isValid": true,
        "order": {
            "customerName": "string",
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
string fulfillmentInstructions = """
    You are the Fulfillment Decision Agent for Froyo Foundry.

    Your job is to determine whether incoming orders can be fulfilled based on inventory levels, and to provide recommendations and coupon codes when orders cannot be fully fulfilled.

    ## Responsibilities

    1. Analyze the canonical order object produced by the Order Intake Agent.
    2. Use the CheckInventory tool to check stock levels for each line item in the order.
    3. Determine if the order can be fully fulfilled, partially fulfilled, or not fulfilled at all.
    4. If the order cannot be fully fulfilled, use the GenerateCouponCode tool to create a 25% discount coupon for the customer.
    5. Recommend alternative products that are in stock if any items cannot be fulfilled.  Use the GetAvailableInventory tool to find suitable alternatives based on flavor profiles. Use the ListFlavors tool to get flavor details for your recommendations.

    ## Output Requirements

    Return valid JSON only.

    Structure your response as follows:

    {
        "orderId": "string",
        "customerEmail": "string",
        "items": [
            {
                "sku": "string",
                "productName": "string",
                "requestedQty": 0,
                "availableQty": 0,
                "fulfillableQty": 0,
                "shortfallQty": 0
            }
        ],
        "canFullyFulfill": false,
        "shouldGenerateCoupon": false,
        "coupon": {
            "code": "string",
            "discountPercent": 0
        },
        "alternativeRecommendations": [
            {
                "sku": "string",
                "productName": "string"
            }
        ]
    }

    - Each incoming line item provides a canonical three-letter `flavorId` such as `VNE`. Call `CheckInventory(flavorId)` directly; the tool converts that FlavorId to the inventory SKU `{FlavorId}-TUB`.
    - Populate output `sku` values with the corresponding inventory SKU in `{FlavorId}-TUB` format.
    - `coupon` must be `null` unless `shouldGenerateCoupon` is `true`.
    - `fulfillableQty` = min(`requestedQty`, `availableQty`).
    - `shortfallQty` = `requestedQty` − `fulfillableQty`.
    - `canFullyFulfill` = `true` only if `shortfallQty` is 0 for all items.
    - `shouldGenerateCoupon` = `true` when any item has a shortfall.

    ## Determinism Requirement
    - Rely solely on tool outputs for inventory data and coupon generation.
    - Do not fabricate information about stock levels, product attributes, or coupon codes.
    """;

AIAgent fulfillmentAgent = new AzureOpenAIClient(
    endpoint: new Uri(endpoint),
    credential: new AzureCliCredential())
    .GetChatClient(deploymentName)
    .AsIChatClient()
    .AsAIAgent(
        options: new ChatClientAgentOptions()
        {
            Name = "FulfillmentAgent",
            ChatOptions = new ()
            {
                Tools =
                [
                    AIFunctionFactory.Create(CheckInventoryTool.CheckInventory),
                    AIFunctionFactory.Create(CheckInventoryTool.GetAvailableInventory),
                    AIFunctionFactory.Create(GenerateCouponCodeTool.GenerateCouponCode),
                    AIFunctionFactory.Create(ListFlavorsTool.ListFlavors)
                ],
                Instructions = fulfillmentInstructions,
                ResponseFormat = ChatResponseFormat.ForJsonSchema<FulfillmentDecisionResult>(
                    schemaDescription: "The result of analyzing an order's fulfillment status, including inventory details, fulfillment capability, coupon generation decision, and alternative product recommendations.",
                    schemaName: "FulfillmentDecisionResult")
            }
        });

// Customer messaging agent
string customerMessagingInstructions = """
    You are the Customer Messaging Agent for Froyo Foundry.

    Your job is to craft clear and empathetic messages to customers about their order status based on the fulfillment analysis provided by the Fulfillment Decision Agent.

    ## Responsibilities

    1. Read the fulfillment decision output, including inventory details, fulfillment capability, coupon information, and alternative product recommendations.
    2. Determine the appropriate messaging scenario (full fulfillment, partial fulfillment, no fulfillment).
    3. Craft a clear, concise, and empathetic message to the customer regarding their order status.
    4. Include information about any coupons or alternative products if applicable.

    ## Email Scenarios

    ### Full Fulfillment
    If canFullyFulfill = true
    - confirm the full order will ship soon
    - positive tone

    ### Partial Fulfillment
    If some items are available but not the full quantity
    - explain that available items will ship
    - explain remaining items could not be fulfilled
    - include coupon if provided

    ### No Fulfillment
    If no items are available
    - explain the order cannot be fulfilled
    - include coupon if provided

    ## Writing Style

    Messages must be:
    - clear
    - concise
    - polite
    - customer-friendly

    Do not mention internal systems, agents, tools, or workflows.

    ## Output Requirements

    Return valid JSON only.

    Structure:

    {
        "orderId": "string",
        "message": "string"
    }
""";

AIAgent customerMessagingAgent = new AzureOpenAIClient(
    endpoint: new Uri(endpoint),
    credential: new AzureCliCredential())
    .GetChatClient(deploymentName)
    .AsIChatClient()
    .AsAIAgent(
        options: new ChatClientAgentOptions()
        {
            Name = "CustomerMessagingAgent",
            ChatOptions = new ()
            {
                Instructions = customerMessagingInstructions,
                ResponseFormat = ChatResponseFormat.ForJsonSchema<CustomerMessageResult>(
                    schemaDescription: "The result of analyzing an order's fulfillment status, including inventory details, fulfillment capability, coupon generation decision, and alternative product recommendations.",
                    schemaName: "CustomerMessageResult")
            }
        });


// Run as a workflow
// var workflow = new WorkflowBuilder(orderIntakeAgent)
//     .AddEdge(orderIntakeAgent, fulfillmentAgent)
//     .AddEdge(fulfillmentAgent, customerMessagingAgent)
//     .Build();


// var workflow = AgentWorkflowBuilder.BuildSequential(
//     [
//         orderIntakeAgent,
//         fulfillmentAgent,
//         customerMessagingAgent
//     ]);


// Run the workflow as an agent.
var workflowAgent = new WorkflowBuilder(orderIntakeAgent)
    .AddEdge(orderIntakeAgent, fulfillmentAgent)
    .AddEdge(fulfillmentAgent, customerMessagingAgent)
    .Build().AsAIAgent();


// Invoke with streaming

// Create a session to maintain context across interactions
// AgentSession session = await agent.CreateSessionAsync();

Console.WriteLine("Please enter your Froyo Foundry order.");

var input = Console.ReadLine();

// workflow
// await using StreamingRun run = await InProcessExecution.RunStreamingAsync(workflow, new ChatMessage(ChatRole.User, input));

// workflow as agent
var workflowResult = await workflowAgent.RunAsync(new ChatMessage(ChatRole.User, input));
foreach (ChatMessage msg in workflowResult.Messages)
{
    if (msg.AuthorName == "CustomerMessagingAgent")
    {
        if (msg.Contents is not null)
        {
            foreach (AIContent content in msg.Contents)
            {
                if (content is TextContent { Text: { Length: > 0 } text })
                {
                    Console.WriteLine($"Customer Message:\n{text}");
                }
            }
        }
    }
}


// Must send the turn token to trigger the agents.
// The agents are wrapped as executors. When they receive messages,
// they will cache the messages and only start processing when they receive a TurnToken.
// await run.TrySendMessageAsync(new TurnToken(emitEvents: true));

// string? lastExecutorId = null;
// List<ChatMessage> result = [];
// await foreach (WorkflowEvent evt in run.WatchStreamAsync())
// {
//     if (evt is AgentResponseUpdateEvent e)
//     {
//         if (e.ExecutorId != lastExecutorId)
//         {
//             lastExecutorId = e.ExecutorId;
//             Console.WriteLine();
//             Console.Write($"{e.ExecutorId}: ");
//         }

//         Console.Write(e.Update.Text);
//     }
// }

// Display final result
Console.WriteLine();