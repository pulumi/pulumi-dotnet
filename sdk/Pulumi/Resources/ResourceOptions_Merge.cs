// Copyright 2016-2021, Pulumi Corporation

namespace Pulumi
{
    public partial class ResourceOptions
    {
        internal static void MergeNormalOptions(ResourceOptions options1, ResourceOptions options2)
        {
            options1.Id = options2.Id ?? options1.Id;
            options1.Parent = options2.Parent ?? options1.Parent;
            options1.Protect = options2.Protect ?? options1.Protect;
            options1.Urn = options2.Urn ?? options1.Urn;
            options1.Version = options2.Version ?? options1.Version;
            options1.PluginDownloadURL = options2.PluginDownloadURL ?? options1.PluginDownloadURL;
            options1.Provider = options2.Provider ?? options1.Provider;
            options1.CustomTimeouts = options2.CustomTimeouts ?? options1.CustomTimeouts;
            options1.RetainOnDelete = options2.RetainOnDelete ?? options1.RetainOnDelete;
            options1.DeletedWith = options2.DeletedWith ?? options1.DeletedWith;

            options1.IgnoreChanges.AddRange(options2.IgnoreChanges);
            options1.ResourceTransformations.AddRange(options2.ResourceTransformations);
            options1.Aliases.AddRange(options2.Aliases);
            options1.ReplaceOnChanges.AddRange(options2.ReplaceOnChanges);

            options1.DependsOn = options1.DependsOn.Concat(options2.DependsOn);

            options1.Hooks = options1.Hooks.Concat(options2.Hooks);
        }
    }
}
