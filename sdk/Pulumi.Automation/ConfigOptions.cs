// Copyright 2024, Pulumi Corporation

namespace Pulumi.Automation
{
    public class ConfigOptions
    {
        public bool Path { get; set; }
        public string? ConfigFile { get; set; }
        public bool ShowSecrets { get; set; }

        public ConfigOptions(bool path = false, string? configFile = null, bool showSecrets = false)
        {
            Path = path;
            ConfigFile = configFile;
            ShowSecrets = showSecrets;
        }
    }

    public class GetAllConfigOptions
    {
        public bool Path { get; set; }
        public string? ConfigFile { get; set; }
        public bool ShowSecrets { get; set; }

        public GetAllConfigOptions(bool path = false, string? configFile = null, bool showSecrets = false)
        {
            Path = path;
            ConfigFile = configFile;
            ShowSecrets = showSecrets;
        }
    }
}
