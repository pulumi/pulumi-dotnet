namespace Pulumi.Automation;

using System;
using System.Collections.Generic;

public sealed class ImportOptions
{
    /// <summary>
    /// The resource definitions to import into the stack.
    /// </summary>
    public List<ImportResource>? Resources { get; set; }
    /// <summary>
    /// The name table maps language names to parent and provider URNs. These names are
    /// used in the generated definitions, and should match the corresponding declarations
    /// in the source program. This table is required if any parents or providers are
    /// specified by the resources to import.
    /// </summary>
    public Dictionary<string, string>? NameTable { get; set; }
    /// <summary>
    /// Allow resources to be imported with protection from deletion enabled. Set to true by default.
    /// </summary>
    public bool? Protect { get; set; }
    /// <summary>
    /// Generate resource declaration code for the imported resources. Set to true by default.
    /// </summary>
    public bool? GenerateCode { get; set; }
    /// <summary>
    /// Specify the name of a converter to import resources from.
    /// </summary>
    public string? Converter { get; set; }
    /// <summary>
    /// Additional arguments to pass to the converter, if the user specified one.
    /// </summary>
    public List<string>? ConverterArgs { get; set; }
    /// <summary>
    /// Show config secrets when they appear.
    /// </summary>
    public bool? ShowSecrets { get; set; }
    /// <summary>
    /// Optional callback which is invoked whenever StandardOutput is written into
    /// </summary>
    public Action<string>? OnStandardOutput { get; set; }
    /// <summary>
    /// Optional callback which is invoked whenever StandardError is written into
    /// </summary>
    public Action<string>? OnStandardError { get; set; }
}
