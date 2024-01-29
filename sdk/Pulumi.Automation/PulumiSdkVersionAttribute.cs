// Copyright 2016-2024, Pulumi Corporation

using System;
using Semver;

namespace Pulumi.Automation
{
    [AttributeUsage(AttributeTargets.Assembly)]
    public sealed class PulumiSdkVersionAttribute : Attribute
    {
        public SemVersion Version { get; }

        public PulumiSdkVersionAttribute(string value)
        {
            value = value.Trim();
            if (value.StartsWith('v'))
            {
                value = value.Substring(1);
            }
            Version = SemVersion.Parse(value, SemVersionStyles.Strict);
        }
    }
}
