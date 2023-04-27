// Copyright 2016-2021, Pulumi Corporation

using System.Collections.Immutable;

namespace Pulumi.Automation
{
    public class WhoAmIResult
    {
        public string User { get; }
        public string? Url { get; }
        public ImmutableArray<string> Organizations { get; }

        public WhoAmIResult(string user, string? url, ImmutableArray<string> organizations)
        {
            this.User = user;
            this.Url = url;
            this.Organizations = organizations;
        }
    }
}
