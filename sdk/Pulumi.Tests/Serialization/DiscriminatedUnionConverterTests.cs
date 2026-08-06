// Copyright 2016-2026, Pulumi Corporation

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Pulumi.Serialization;
using Xunit;

namespace Pulumi.Tests.Serialization
{
    public class DiscriminatedUnionConverterTests : ConverterTests
    {
        // "basic" and "bearer" deliberately share an identical property shape so that only
        // discriminator dispatch (not structural first-match) can tell them apart.
        [DiscriminatedUnionType("discriminantKind")]
        [DiscriminatedUnionCase("basic", typeof(BasicAuth))]
        [DiscriminatedUnionCase("bearer", typeof(BearerAuth))]
        [DiscriminatedUnionCase("apiKey", typeof(ApiKeyAuth))]
        public interface IAuthConfig
        {
        }

        [OutputType]
        public sealed class BasicAuth : IAuthConfig
        {
            public readonly string Value;

            [OutputConstructor]
            public BasicAuth(string value)
            {
                Value = value;
            }
        }

        [OutputType]
        public sealed class BearerAuth : IAuthConfig
        {
            public readonly string Value;

            [OutputConstructor]
            public BearerAuth(string value)
            {
                Value = value;
            }
        }

        [OutputType]
        public sealed class ApiKeyAuth : IAuthConfig
        {
            public readonly string Header;
            public readonly string Value;

            [OutputConstructor]
            public ApiKeyAuth(string header, string value)
            {
                Header = header;
                Value = value;
            }
        }

        [OutputType]
        public sealed class EndpointType
        {
            public readonly string Url;
            public readonly IAuthConfig Auth;

            [OutputConstructor]
            public EndpointType(string url, IAuthConfig auth)
            {
                Url = url;
                Auth = auth;
            }
        }

        [Fact]
        public async Task DispatchesOnTagEvenWhenEarlierCaseIsStructurallyCompatible()
        {
            var data = Converter.ConvertValue<IAuthConfig>(NoWarn, "", await SerializeToValueAsync(new Dictionary<string, object>
            {
                { "discriminantKind", "bearer" },
                { "value", "token" },
            }));

            var bearer = Assert.IsType<BearerAuth>(data.Value);
            Assert.Equal("token", bearer.Value);
            Assert.True(data.IsKnown);
        }

        [Fact]
        public async Task BasicTagConverts()
        {
            var data = Converter.ConvertValue<IAuthConfig>(NoWarn, "", await SerializeToValueAsync(new Dictionary<string, object>
            {
                { "discriminantKind", "basic" },
                { "value", "hunter2" },
            }));

            var basic = Assert.IsType<BasicAuth>(data.Value);
            Assert.Equal("hunter2", basic.Value);
            Assert.True(data.IsKnown);
        }

        [Fact]
        public async Task BearerTagConverts()
        {
            var data = Converter.ConvertValue<IAuthConfig>(NoWarn, "", await SerializeToValueAsync(new Dictionary<string, object>
            {
                { "discriminantKind", "bearer" },
                { "value", "token" },
            }));

            var bearer = Assert.IsType<BearerAuth>(data.Value);
            Assert.Equal("token", bearer.Value);
            Assert.True(data.IsKnown);
        }

        [Fact]
        public async Task ApiKeyTagConverts()
        {
            var data = Converter.ConvertValue<IAuthConfig>(NoWarn, "", await SerializeToValueAsync(new Dictionary<string, object>
            {
                { "discriminantKind", "apiKey" },
                { "header", "X-Api-Key" },
                { "value", "key" },
            }));

            var apiKey = Assert.IsType<ApiKeyAuth>(data.Value);
            Assert.Equal("X-Api-Key", apiKey.Header);
            Assert.Equal("key", apiKey.Value);
            Assert.True(data.IsKnown);
        }

        [Fact]
        public async Task UnknownTagLogs()
        {
            string? loggedError = null;
            Action<string> warn = error => loggedError = error;

            var data = Converter.ConvertValue<IAuthConfig>(warn, "", await SerializeToValueAsync(new Dictionary<string, object>
            {
                { "discriminantKind", "oauth" },
                { "value", "token" },
            }));

            Assert.Null(data.Value);
            Assert.Equal(
                "unknown \"discriminantKind\" value \"oauth\"; expected one of: apiKey, basic, bearer deserializing ",
                loggedError);
        }

        [Fact]
        public async Task MissingDiscriminatorPropertyLogs()
        {
            string? loggedError = null;
            Action<string> warn = error => loggedError = error;

            var data = Converter.ConvertValue<IAuthConfig>(warn, "", await SerializeToValueAsync(new Dictionary<string, object>
            {
                { "value", "token" },
            }));

            Assert.Null(data.Value);
            Assert.Equal(
                "missing discriminator property \"discriminantKind\"; expected one of: apiKey, basic, bearer deserializing ",
                loggedError);
        }

        [Fact]
        public async Task NestedUnionInsideOutputTypeConverts()
        {
            var data = Converter.ConvertValue<EndpointType>(NoWarn, "", await SerializeToValueAsync(new Dictionary<string, object>
            {
                { "url", "https://example.com" },
                {
                    "auth",
                    new Dictionary<string, object>
                    {
                        { "discriminantKind", "bearer" },
                        { "value", "token" },
                    }
                },
            }));

            Assert.Equal("https://example.com", data.Value.Url);
            var bearer = Assert.IsType<BearerAuth>(data.Value.Auth);
            Assert.Equal("token", bearer.Value);
            Assert.True(data.IsKnown);
        }

        [Fact]
        public async Task SecretUnionValueConverts()
        {
            var data = Converter.ConvertValue<IAuthConfig>(NoWarn, "", CreateSecretValue(await SerializeToValueAsync(new Dictionary<string, object>
            {
                { "discriminantKind", "bearer" },
                { "value", "token" },
            })));

            var bearer = Assert.IsType<BearerAuth>(data.Value);
            Assert.Equal("token", bearer.Value);
            Assert.True(data.IsKnown);
            Assert.True(data.IsSecret);
        }
    }
}
