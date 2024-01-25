using System;
using Semver;

namespace Pulumi.Automation
{
    [AttributeUsage(AttributeTargets.Assembly)]
    public class PulumiSDKVersion : Attribute
    {
        public SemVersion Version { get; private set; }

        public PulumiSDKVersion() : this("") { }

        public PulumiSDKVersion(string value)
        {
            value = value.Trim();
            if (value.StartsWith("v"))
            {
                value = value.Substring(1);
            }
            Version = SemVersion.Parse(value, SemVersionStyles.Strict);
        }
    }
}
