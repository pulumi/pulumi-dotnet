using System;

namespace Pulumi.Automation.Serialization;

public class UpdateKindConverter : IStringToEnumConverter<UpdateKind>
{
    public UpdateKind Convert(string input) => input switch
    {
        "update" => UpdateKind.Update,
        "preview" => UpdateKind.Preview,
        "refresh" => UpdateKind.Refresh,
        "rename" => UpdateKind.Rename,
        "destroy" => UpdateKind.Destroy,
        "import" => UpdateKind.Import,
        "resource-import" => UpdateKind.ResourceImport,
        _ => throw new InvalidOperationException($"'{input}' is not a valid {typeof(UpdateKind).FullName}"),
    };
}
