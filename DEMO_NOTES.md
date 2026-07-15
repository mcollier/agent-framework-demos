# Demo Notes

## 1 - Hello Agent
- Basic MAF agent
- Uses Azure OpenAI (Azure SDK + Azure CLI auth)
- Create a SESSION to maintain context across interactions (all in memory)

What flavors do you have?
- notice it makes up some information that was not in the system instructions

Do any contain peanuts?
- continues to make up content as it doesn't really know

ENTER to stop

## 2 - Extending
- same system prompt
- uses Azure OpenAI (and a different model)

### Extensions / Additional Functionality

- Tool calling - ListFlavorsTool
- Middleware - GuardMiddleware to filter for specific terms
What flavors do you have?

Do any contain peanuts?

How many questions have I asked you?

I love a bowl of Midnight Wolverine

## 3 - Workflow
- Similar setup, but with more agents in a workflow configuration
- Order Intake Agent
  - Validate against some business rules
  - JSON response (structured output)
- Fulfillment Decision Agent
  - Determine if an order can be fullfilled based on inventory levels, and provide a recommendation from available products.
  - JSON response (structured output)
- Customer Messaging Agent
  - Create a customer friendly message based on the ability to fulfill the order

### Good

My name is Michael Collier and I would like 2 tubs of Java Jolt and 5 of Vanilla Exception.

## Unavailable product

My name is Michael Collier and I would like 8 tubs of Java Jolt.

## Demo workflow failure

Run again - but stop after seeing the Fulfillment Agent start.

Workflow died - process stopped.
All state in process.
Recovery - would have to find the checkpoint and resume manually.