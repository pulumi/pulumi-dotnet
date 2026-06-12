// Copyright 2026, Pulumi Corporation.  All rights reserved.
/*
ESC (Environments, Secrets, Config) API

Pulumi ESC allows you to compose and manage hierarchical collections of configuration and secrets and consume them in various ways.

API version: 0.1.0
*/


#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using Pulumi.Esc.Sdk.Api;
using Pulumi.Esc.Sdk.Model;

namespace Pulumi.Esc.Sdk.Client
{
    /// <summary>
    /// Provides hosting configuration for Pulumi.Esc.Sdk
    /// </summary>
    public class HostConfiguration
    {
        /// <summary>
        /// The User-Agent header value sent with every request.
        /// </summary>
        public const string UserAgent = "esc-sdk/csharp/0.13.1-dev.0";

        private readonly IServiceCollection _services;
        private readonly JsonSerializerOptions _jsonOptions = CreateDefaultJsonOptions();

        private static JsonSerializerOptions CreateDefaultJsonOptions()
        {
            var options = new JsonSerializerOptions();
            JsonDefaults.EnsureTypeInfoResolver(options);
            return options;
        }

        internal bool HttpClientsAdded { get; private set; }

        /// <summary>
        /// Instantiates the class 
        /// </summary>
        /// <param name="services"></param>
        public HostConfiguration(IServiceCollection services)
        {
            _services = services;
            _jsonOptions.Converters.Add(new JsonStringEnumConverter());
            _jsonOptions.Converters.Add(new DateTimeJsonConverter());
            _jsonOptions.Converters.Add(new DateTimeNullableJsonConverter());
            _jsonOptions.Converters.Add(new DateOnlyJsonConverter());
            _jsonOptions.Converters.Add(new DateOnlyNullableJsonConverter());
            _jsonOptions.Converters.Add(new AccessJsonConverter());
            _jsonOptions.Converters.Add(new AccessorJsonConverter());
            _jsonOptions.Converters.Add(new CheckEnvironmentJsonConverter());
            _jsonOptions.Converters.Add(new CloneEnvironmentJsonConverter());
            _jsonOptions.Converters.Add(new CreateEnvironmentJsonConverter());
            _jsonOptions.Converters.Add(new CreateEnvironmentRevisionTagJsonConverter());
            _jsonOptions.Converters.Add(new CreateEnvironmentTagJsonConverter());
            _jsonOptions.Converters.Add(new EnvironmentDefinitionJsonConverter());
            _jsonOptions.Converters.Add(new EnvironmentDefinitionValuesJsonConverter());
            _jsonOptions.Converters.Add(new EnvironmentDiagnosticJsonConverter());
            _jsonOptions.Converters.Add(new EnvironmentDiagnosticsJsonConverter());
            _jsonOptions.Converters.Add(new EnvironmentRevisionJsonConverter());
            _jsonOptions.Converters.Add(new EnvironmentRevisionTagJsonConverter());
            _jsonOptions.Converters.Add(new EnvironmentRevisionTagsJsonConverter());
            _jsonOptions.Converters.Add(new EnvironmentTagJsonConverter());
            _jsonOptions.Converters.Add(new ErrorJsonConverter());
            _jsonOptions.Converters.Add(new EvaluatedExecutionContextJsonConverter());
            _jsonOptions.Converters.Add(new ExprJsonConverter());
            _jsonOptions.Converters.Add(new ExprBuiltinJsonConverter());
            _jsonOptions.Converters.Add(new InterpolationJsonConverter());
            _jsonOptions.Converters.Add(new ListEnvironmentTagsJsonConverter());
            _jsonOptions.Converters.Add(new ModelEnvironmentJsonConverter());
            _jsonOptions.Converters.Add(new OpenEnvironmentJsonConverter());
            _jsonOptions.Converters.Add(new OrgEnvironmentJsonConverter());
            _jsonOptions.Converters.Add(new OrgEnvironmentsJsonConverter());
            _jsonOptions.Converters.Add(new PosJsonConverter());
            _jsonOptions.Converters.Add(new PropertyAccessorJsonConverter());
            _jsonOptions.Converters.Add(new RangeJsonConverter());
            _jsonOptions.Converters.Add(new ReferenceJsonConverter());
            _jsonOptions.Converters.Add(new TraceJsonConverter());
            _jsonOptions.Converters.Add(new UpdateEnvironmentRevisionTagJsonConverter());
            _jsonOptions.Converters.Add(new UpdateEnvironmentTagJsonConverter());
            _jsonOptions.Converters.Add(new UpdateEnvironmentTagCurrentTagJsonConverter());
            _jsonOptions.Converters.Add(new UpdateEnvironmentTagNewTagJsonConverter());
            _jsonOptions.Converters.Add(new ValueJsonConverter());
            JsonSerializerOptionsProvider jsonSerializerOptionsProvider = new(_jsonOptions);
            _services.AddSingleton(jsonSerializerOptionsProvider);
            _services.AddSingleton<IApiFactory, ApiFactory>();
            _services.AddSingleton<EscApiEvents>();
            _services.AddTransient<IEscApi, EscApi>();
        }

        /// <summary>
        /// Configures the HttpClients.
        /// </summary>
        /// <param name="client"></param>
        /// <param name="builder"></param>
        /// <returns></returns>
        public HostConfiguration AddApiHttpClients
        (
            Action<HttpClient>? client = null, Action<IHttpClientBuilder>? builder = null)
        {
            if (client == null)
                client = c => c.BaseAddress = new Uri(ClientUtils.BASE_ADDRESS);

            List<IHttpClientBuilder> builders = new List<IHttpClientBuilder>();

            builders.Add(_services.AddHttpClient<IEscApi, EscApi>(client));

            if (builder != null)
                foreach (IHttpClientBuilder instance in builders)
                    builder(instance);

            HttpClientsAdded = true;

            return this;
        }

        /// <summary>
        /// Configures the JsonSerializerSettings
        /// </summary>
        /// <param name="options"></param>
        /// <returns></returns>
        public HostConfiguration ConfigureJsonOptions(Action<JsonSerializerOptions> options)
        {
            options(_jsonOptions);

            return this;
        }

        /// <summary>
        /// Adds tokens to your IServiceCollection
        /// </summary>
        /// <typeparam name="TTokenBase"></typeparam>
        /// <param name="token"></param>
        /// <returns></returns>
        public HostConfiguration AddTokens<TTokenBase>(TTokenBase token) where TTokenBase : TokenBase
        {
            return AddTokens(new TTokenBase[] { token });
        }

        /// <summary>
        /// Adds tokens to your IServiceCollection
        /// </summary>
        /// <typeparam name="TTokenBase"></typeparam>
        /// <param name="tokens"></param>
        /// <returns></returns>
        public HostConfiguration AddTokens<TTokenBase>(IEnumerable<TTokenBase> tokens) where TTokenBase : TokenBase
        {
            TokenContainer<TTokenBase> container = new TokenContainer<TTokenBase>(tokens);
            _services.AddSingleton(services => container);

            return this;
        }

        /// <summary>
        /// Adds a token provider to your IServiceCollection
        /// </summary>
        /// <typeparam name="TTokenProvider"></typeparam>
        /// <typeparam name="TTokenBase"></typeparam>
        /// <returns></returns>
        public HostConfiguration UseProvider<TTokenProvider, TTokenBase>()
            where TTokenProvider : TokenProvider<TTokenBase>
            where TTokenBase : TokenBase
        {
            _services.AddSingleton<TTokenProvider>();
            _services.AddSingleton<TokenProvider<TTokenBase>>(services => services.GetRequiredService<TTokenProvider>());

            return this;
        }
    }
}
