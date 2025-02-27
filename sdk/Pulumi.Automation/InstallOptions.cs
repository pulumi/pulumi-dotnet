namespace Pulumi.Automation
{
    public class InstallOptions
    {
        /// <summary>
        /// Skip installing plugins.
        /// </summary>
        public bool NoPlugins { get; set; }

        /// <summary>
        /// Skip installing dependencies.
        /// </summary>
        public bool NoDependencies { get; set; }

        /// <summary>
        /// Reinstall a plugin even if it already exists.
        /// </summary>
        public bool Reinstall { get; set; }

        /// <summary>
        /// Use language version tools to setup and install the language runtime.
        /// </summary>
        public bool UseLanguageVersionTools { get; set; }
    }
}
