# Microsoft Agent Framework Demos

A progressive series of .NET console applications demonstrating key concepts of the Microsoft Agent Framework. Each demo builds on the previous one, introducing new capabilities step-by-step.

## What You'll Learn

- Setting up AI agents with streaming responses
- Managing conversation sessions
- Adding function calling (tools) to extend agent capabilities
- Implementing middleware for content filtering
- Building multi-agent workflows

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- An Azure OpenAI resource (for demos 2 & 3) **OR** GitHub Models API access (for demo 1)
- Azure CLI (for authentication): `az login`

## Project Structure

```
agent-framework-demos/
├── source/
│   ├── 1-HelloAgent/          # Basic agent with streaming
│   ├── 2-Extending/            # Tools + middleware
│   └── 3-Workflow/             # Multi-agent workflows
└── agent-framework-demos.sln   # Solution file
```

## Demos Overview

### 1. HelloAgent - Your First Agent
**Concepts:** Agent setup, streaming responses, session management, interactive REPL

A frozen yogurt recommendation chatbot that demonstrates:
- Creating an AI agent with custom instructions
- Streaming responses token-by-token
- Maintaining conversation context with `AgentSession`
- Building a simple command-line interface

**Configuration:** Uses GitHub Models (OpenAI) by default

### 2. Extending - Tools & Middleware
**Concepts:** Function calling, middleware pipeline, content filtering

Extends the froyo agent with:
- **Tools (Function Calling)**: Calls a `ListFlavors()` function to get real-time inventory
- **Middleware**: Filters forbidden words from input/output using a custom pipeline
- Local functions for helper logic

**Configuration:** Uses Azure OpenAI with Azure CLI authentication

### 3. Workflow - Multi-Agent Orchestration
**Concepts:** Agentic workflows, agent composition, order processing pipeline

A multi-step order processing system with:
- **Order Intake Agent**: Validates orders against business rules
- **Froyo Recommender Agent**: Product recommendations
- Demonstrates how agents can work together in a workflow

**Configuration:** Uses Azure OpenAI with Azure CLI authentication

## Getting Started

### 1. Clone the Repository

```bash
git clone https://github.com/mcollier/agent-framework-demos.git
cd agent-framework-demos
```

### 2. Configure Settings

Each demo has an `appsettings.json` file. Copy it to `appsettings.Development.json` and add your credentials:

#### Demo 1 (GitHub Models)
```json
{
  "OpenAI": {
    "Endpoint": "https://models.github.ai/inference",
    "DeploymentName": "openai/gpt-5-chat",
    "Key": "YOUR_GITHUB_PAT_HERE"
  }
}
```

#### Demos 2 & 3 (Azure OpenAI)
```json
{
  "AzureOpenAI": {
    "Endpoint": "https://YOUR_RESOURCE.openai.azure.com/",
    "DeploymentName": "gpt-4o-mini"
  }
}
```

> **Note:** `appsettings.Development.json` is git-ignored to keep secrets out of source control.

### 3. Authenticate with Azure (for demos 2 & 3)

```bash
az login
```

The demos use `AzureCliCredential` for passwordless authentication.

### 4. Run the Demos

Navigate to each demo folder and run:

```bash
cd source/1-HelloAgent
dotnet run

# Then try the others
cd ../2-Extending
dotnet run

cd ../3-Workflow
dotnet run
```

Type `exit` or press Enter on an empty line to quit.

## Key Concepts Demonstrated

### Agents
An **AI Agent** combines an LLM with instructions, tools, and state management to perform tasks autonomously.

```csharp
AIAgent agent = chatClient
    .AsAIAgent(
        instructions: "You're a helpful assistant...",
        name: "MyAgent",
        tools: [...]);
```

### Sessions
**AgentSession** maintains conversation history across multiple turns:

```csharp
AgentSession session = await agent.CreateSessionAsync();
await agent.RunAsync("What's the weather?", session);
await agent.RunAsync("How about tomorrow?", session); // Remembers context
```

### Tools (Function Calling)
Tools let agents call .NET functions to access external data:

```csharp
AIFunctionFactory.Create(ListFlavorsTool.ListFlavors)
```

The agent decides when to call functions based on the conversation.

### Middleware
Middleware intercepts messages before/after the agent runs:

```csharp
var agentWithMiddleware = agent
    .AsBuilder()
    .Use(runFunc: FilterContentMiddleware)
    .Build();
```

### Workflows
Orchestrate multiple agents to handle complex, multi-step tasks (see Demo 3).

## Next Steps

- **Add your own tools**: Extend agents with custom functions
- **Experiment with prompts**: Modify instructions to change agent behavior
- **Deploy to production**: Package as a web API or Azure Function
- **Explore Microsoft Foundry**: Deploy and evaluate agents at scale

## Resources

- [Microsoft Agent Framework Documentation](https://learn.microsoft.com/azure/ai-foundry/agent-framework/)
- [Azure OpenAI Service](https://learn.microsoft.com/azure/ai-services/openai/)
- [GitHub Models](https://github.com/marketplace/models)
- [Microsoft Foundry](https://learn.microsoft.com/azure/ai-foundry/)

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.
