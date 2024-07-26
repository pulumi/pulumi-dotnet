namespace Pulumi.Automation;

using System.Collections.Generic;

public class ImportResource
{
    /// <summary>
    /// The name of the resource to import
    /// </summary>
    public string? Name { get; init; }
    /// <summary>
    /// The type of the resource to import
    /// </summary>
    public string? Type { get; init; }
    /// <summary>
    /// The ID of the resource to import. The format of the ID is specific to the resource type
    /// </summary>
    public string? Id { get; init; }

    public string? Parent { get; init; }

    public string? Provider { get; init; }

    public string? Version { get; init; }

    public string? LogicalName { get; init; }

    public List<string>? Properties { get; init; }

    public bool? Component { get; init; }

    public bool? Remote { get; init; }
}
