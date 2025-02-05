using System.Collections.Generic;
namespace Pulumi.Experimental.Provider
{
    public class Metadata
    {
        public string Name { get; set; } = "";
        public string Version { get; set; } = "";
        public string? DisplayName { get; set; }
    }

    public class PropertyDefinition
    {
        public string? Description { get; set; }
        public string? Type { get; set; }
        public string? Ref { get; set; }
        public bool Optional { get; set; }
    }

    public class TypeDefinition
    {
        public string Name { get; set; } = "";
        public string? Description { get; set; }
        public Dictionary<string, PropertyDefinition> Properties { get; set; } = new();
        public Dictionary<string, string> PropertiesMapping { get; set; } = new();
    }

    public class ComponentDefinition
    {
        public string? Description { get; set; }
        public Dictionary<string, PropertyDefinition> Inputs { get; set; } = new();
        public Dictionary<string, string> InputsMapping { get; set; } = new();
        public Dictionary<string, PropertyDefinition> Outputs { get; set; } = new();
        public Dictionary<string, string> OutputsMapping { get; set; } = new();
    }
}