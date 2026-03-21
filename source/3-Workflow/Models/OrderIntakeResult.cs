namespace FroyoFoundry.Workflow.Models;

/// <summary>
/// Represents the result of validating and normalizing an incoming order.
/// </summary>
public sealed record OrderIntakeResult
{
    /// <summary>True when the order passed intake validation.</summary>
    public required bool IsValid { get; init; }

    /// <summary>The normalized order payload when validation succeeds.</summary>
    public Order? Order { get; init; }

    /// <summary>Error message explaining why intake failed.</summary>
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Represents a normalized customer order ready for downstream processing.
/// </summary>
public sealed record Order
{
    /// <summary>Name details for the customer who placed the order.</summary>
    public required string CustomerName { get; init; }

    /// <summary>Requested items in the order.</summary>
    public IReadOnlyList<OrderLineItem> LineItems { get; init; } = [];
}

/// <summary>
/// Represents a single order line item.
/// </summary>
public sealed record OrderLineItem
{
    /// <summary>Canonical three-letter FlavorId for the requested item.</summary>
    public required string FlavorId { get; init; }

    /// <summary>Requested quantity for the flavor.</summary>
    public required int Quantity { get; init; }
}