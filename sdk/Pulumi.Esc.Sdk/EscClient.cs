// Copyright 2024, Pulumi Corporation.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Pulumi.Esc.Sdk.Api;
using Pulumi.Esc.Sdk.Client;
using Pulumi.Esc.Sdk.Extensions;
using Pulumi.Esc.Sdk.Model;

namespace Pulumi.Esc.Sdk
{
    /// <summary>
    /// Exception thrown when an ESC API operation fails.
    /// </summary>
    public class EscApiException : Exception
    {
        /// <summary>The HTTP status code of the failed response, if available.</summary>
        public HttpStatusCode? StatusCode { get; }

        /// <summary>The raw response content, if available.</summary>
        public string? RawContent { get; }

        /// <summary>
        /// Initializes a new instance of <see cref="EscApiException"/>.
        /// </summary>
        public EscApiException(string message) : base(message) { }

        /// <summary>
        /// Initializes a new instance of <see cref="EscApiException"/> with status code details.
        /// </summary>
        public EscApiException(string message, HttpStatusCode statusCode, string rawContent) : base(message)
        {
            StatusCode = statusCode;
            RawContent = rawContent;
        }
    }

    /// <summary>
    /// Options for cloning an environment.
    /// </summary>
    public class CloneEnvironmentOptions
    {
        /// <summary>Whether to preserve the revision history from the source environment.</summary>
        public bool PreserveHistory { get; set; }

        /// <summary>Whether to preserve access controls from the source environment.</summary>
        public bool PreserveAccess { get; set; }

        /// <summary>Whether to preserve environment tags from the source environment.</summary>
        public bool PreserveEnvironmentTags { get; set; }

        /// <summary>Whether to preserve revision tags from the source environment.</summary>
        public bool PreserveRevisionTags { get; set; }
    }

    /// <summary>
    /// A high-level client for the Pulumi ESC (Environments, Secrets, Config) API.
    /// Wraps the generated <see cref="IEscApi"/> and provides a more convenient interface.
    /// </summary>
    public class EscClient : IDisposable
    {
        private readonly ServiceProvider _serviceProvider;
        private readonly JsonSerializerOptions _jsonSerializerOptions;
        private bool _disposed;

        /// <summary>
        /// The underlying raw API client. Use this for advanced scenarios not covered by the convenience methods.
        /// </summary>
        public IEscApi RawApi { get; }

        private EscClient(ServiceProvider serviceProvider, IEscApi rawApi)
        {
            _serviceProvider = serviceProvider;
            _jsonSerializerOptions = serviceProvider.GetRequiredService<JsonSerializerOptionsProvider>().Options;
            RawApi = rawApi;
        }

        #region Factory Methods

        /// <summary>
        /// Creates a new <see cref="EscClient"/> with the specified access token and backend URL.
        /// </summary>
        /// <param name="accessToken">The Pulumi access token for authentication.</param>
        /// <param name="backendUrl">
        /// The ESC API base URL (e.g. "https://api.pulumi.com/api/esc").
        /// If null, defaults to the standard Pulumi Cloud ESC API endpoint.
        /// </param>
        /// <returns>A new <see cref="EscClient"/> instance.</returns>
        public static EscClient Create(string accessToken, string? backendUrl = null)
        {
            if (string.IsNullOrEmpty(accessToken))
                throw new ArgumentException("Access token must not be null or empty.", nameof(accessToken));

            var apiUrl = backendUrl ?? ClientUtils.BASE_ADDRESS;
            return BuildClient(accessToken, apiUrl);
        }

        /// <summary>
        /// Creates a new <see cref="EscClient"/> with default configuration.
        /// The access token is resolved from <c>PULUMI_ACCESS_TOKEN</c> environment variable
        /// or the currently logged-in Pulumi CLI / ESC CLI account.
        /// The backend URL is resolved from <c>PULUMI_BACKEND_URL</c> environment variable
        /// or the currently logged-in account URL.
        /// </summary>
        /// <returns>A new <see cref="EscClient"/> instance.</returns>
        /// <exception cref="InvalidOperationException">If no access token can be found.</exception>
        public static EscClient CreateDefault()
        {
            var accessToken = EscAuth.GetDefaultAccessToken();
            var backendUrl = EscAuth.GetDefaultBackendUrl();
            var apiUrl = EscAuth.GetEscApiUrl(backendUrl);
            return BuildClient(accessToken, apiUrl);
        }

        private static EscClient BuildClient(string accessToken, string apiUrl)
        {
            var services = new ServiceCollection();

            // Add logging
            services.AddLogging(builder => builder.AddProvider(NullLoggerProvider.Instance));

            // Configure the API
            services.AddApi(config =>
            {
                config.AddTokens(new ApiKeyToken(accessToken, ClientUtils.ApiKeyHeader.Authorization, "token "));
                config.AddApiHttpClients(client =>
                {
                    client.BaseAddress = new Uri(apiUrl);
                    client.DefaultRequestHeaders.Add("X-Pulumi-Source", "esc-sdk");
                    client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", HostConfiguration.UserAgent);
                });
            });

            var serviceProvider = services.BuildServiceProvider();
            var escApi = serviceProvider.GetRequiredService<IEscApi>();
            return new EscClient(serviceProvider, escApi);
        }

        #endregion

        #region Environment CRUD

        /// <summary>
        /// Creates a new environment with the given name in the given organization.
        /// </summary>
        public async Task CreateEnvironmentAsync(string orgName, string projectName, string envName, CancellationToken cancellationToken = default)
        {
            var createEnv = new CreateEnvironment(envName, projectName);
            var response = await RawApi.CreateEnvironmentAsync(createEnv, orgName, cancellationToken).ConfigureAwait(false);
            EnsureSuccess(response, "CreateEnvironment");
        }

        /// <summary>
        /// Clones an existing environment into a new environment.
        /// </summary>
        public async Task CloneEnvironmentAsync(
            string orgName, string srcProjectName, string srcEnvName,
            string destProjectName, string destEnvName,
            CloneEnvironmentOptions? options = null, CancellationToken cancellationToken = default)
        {
            var clone = new CloneEnvironment(destEnvName, destProjectName);
            if (options != null)
            {
                clone.PreserveHistory = options.PreserveHistory;
                clone.PreserveAccess = options.PreserveAccess;
                clone.PreserveEnvironmentTags = options.PreserveEnvironmentTags;
                clone.PreserveRevisionTags = options.PreserveRevisionTags;
            }

            var response = await RawApi.CloneEnvironmentAsync(clone, orgName, srcProjectName, srcEnvName, cancellationToken).ConfigureAwait(false);
            EnsureSuccess(response, "CloneEnvironment");
        }

        /// <summary>
        /// Deletes the environment with the given name.
        /// </summary>
        public async Task DeleteEnvironmentAsync(string orgName, string projectName, string envName, CancellationToken cancellationToken = default)
        {
            var response = await RawApi.DeleteEnvironmentAsync(orgName, projectName, envName, cancellationToken).ConfigureAwait(false);
            EnsureSuccess(response, "DeleteEnvironment");
        }

        /// <summary>
        /// Lists all environments in the given organization.
        /// </summary>
        /// <param name="orgName">Organization name.</param>
        /// <param name="continuationToken">Optional continuation token for pagination.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The list of environments.</returns>
        public async Task<OrgEnvironments> ListEnvironmentsAsync(string orgName, string? continuationToken = null, CancellationToken cancellationToken = default)
        {
            Option<string> contToken = continuationToken != null
                ? new Option<string>(continuationToken)
                : default;

            var response = await RawApi.ListEnvironmentsAsync(orgName, contToken, cancellationToken).ConfigureAwait(false);
            EnsureSuccess(response, "ListEnvironments");

            if (response.TryOk(out var result))
                return result;

            throw new EscApiException("ListEnvironments returned success but no data.");
        }

        #endregion

        #region Environment Definition (YAML)

        /// <summary>
        /// Reads the environment definition and returns the raw YAML string.
        /// </summary>
        public async Task<string> GetEnvironmentYamlAsync(string orgName, string projectName, string envName, CancellationToken cancellationToken = default)
        {
            var response = await RawApi.GetEnvironmentAsync(orgName, projectName, envName, cancellationToken).ConfigureAwait(false);
            EnsureSuccess(response, "GetEnvironment");
            return response.RawContent;
        }

        /// <summary>
        /// Reads the environment definition at a specific version and returns the raw YAML string.
        /// </summary>
        public async Task<string> GetEnvironmentAtVersionYamlAsync(string orgName, string projectName, string envName, string version, CancellationToken cancellationToken = default)
        {
            var response = await RawApi.GetEnvironmentAtVersionAsync(orgName, projectName, envName, version, cancellationToken).ConfigureAwait(false);
            EnsureSuccess(response, "GetEnvironmentAtVersion");
            return response.RawContent;
        }

        /// <summary>
        /// Reads the environment definition with static secrets in plaintext and returns the raw YAML string.
        /// </summary>
        public async Task<string> DecryptEnvironmentYamlAsync(string orgName, string projectName, string envName, CancellationToken cancellationToken = default)
        {
            var response = await RawApi.DecryptEnvironmentAsync(orgName, projectName, envName, cancellationToken).ConfigureAwait(false);
            EnsureSuccess(response, "DecryptEnvironment");
            return response.RawContent;
        }

        /// <summary>
        /// Updates the environment definition using a raw YAML string.
        /// </summary>
        /// <returns>Diagnostics from the update, if any.</returns>
        public async Task<EnvironmentDiagnostics?> UpdateEnvironmentYamlAsync(string orgName, string projectName, string envName, string yaml, CancellationToken cancellationToken = default)
        {
            // Bypass the generated code which JSON-serializes the YAML string body,
            // wrapping it in quotes. Send the raw YAML directly.
            var path = $"{ClientUtils.CONTEXT_PATH}/environments/{Uri.EscapeDataString(orgName)}/{Uri.EscapeDataString(projectName)}/{Uri.EscapeDataString(envName)}";
            var (statusCode, content) = await SendYamlRequestAsync(HttpMethod.Patch, path, yaml, cancellationToken).ConfigureAwait(false);

            if ((int)statusCode >= 200 && (int)statusCode < 300)
            {
                if (!string.IsNullOrEmpty(content))
                    return JsonSerializer.Deserialize<EnvironmentDiagnostics>(content, _jsonSerializerOptions);
                return null;
            }

            throw new EscApiException(
                $"UpdateEnvironmentYaml failed with status code {(int)statusCode} ({statusCode}): {content}",
                statusCode,
                content);
        }

        /// <summary>
        /// Updates the environment definition using an <see cref="EnvironmentDefinition"/> object.
        /// The definition is serialized to YAML before sending.
        /// </summary>
        /// <returns>Diagnostics from the update, if any.</returns>
        public async Task<EnvironmentDiagnostics?> UpdateEnvironmentAsync(string orgName, string projectName, string envName, EnvironmentDefinition definition, CancellationToken cancellationToken = default)
        {
            var yaml = EnvironmentDefinitionSerializer.SerializeToYaml(definition);
            return await UpdateEnvironmentYamlAsync(orgName, projectName, envName, yaml, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Reads the environment definition and returns a parsed <see cref="EnvironmentDefinition"/>.
        /// </summary>
        public async Task<EnvironmentDefinition?> GetEnvironmentAsync(string orgName, string projectName, string envName, CancellationToken cancellationToken = default)
        {
            var yaml = await GetEnvironmentYamlAsync(orgName, projectName, envName, cancellationToken).ConfigureAwait(false);
            return EnvironmentDefinitionSerializer.Deserialize(yaml);
        }

        /// <summary>
        /// Reads the environment definition at a specific version and returns a parsed <see cref="EnvironmentDefinition"/>.
        /// </summary>
        public async Task<EnvironmentDefinition?> GetEnvironmentAtVersionAsync(string orgName, string projectName, string envName, string version, CancellationToken cancellationToken = default)
        {
            var yaml = await GetEnvironmentAtVersionYamlAsync(orgName, projectName, envName, version, cancellationToken).ConfigureAwait(false);
            return EnvironmentDefinitionSerializer.Deserialize(yaml);
        }

        /// <summary>
        /// Reads the environment definition with static secrets in plaintext and returns a parsed <see cref="EnvironmentDefinition"/>.
        /// </summary>
        public async Task<EnvironmentDefinition?> DecryptEnvironmentAsync(string orgName, string projectName, string envName, CancellationToken cancellationToken = default)
        {
            var yaml = await DecryptEnvironmentYamlAsync(orgName, projectName, envName, cancellationToken).ConfigureAwait(false);
            return EnvironmentDefinitionSerializer.Deserialize(yaml);
        }

        /// <summary>
        /// Checks an environment YAML definition for errors.
        /// </summary>
        /// <returns>The check result, which may contain diagnostics even on error.</returns>
        public async Task<CheckEnvironment?> CheckEnvironmentYamlAsync(string orgName, string yaml, CancellationToken cancellationToken = default)
        {
            // Bypass the generated code which JSON-serializes the YAML string body,
            // wrapping it in quotes. Send the raw YAML directly.
            var path = $"{ClientUtils.CONTEXT_PATH}/environments/{Uri.EscapeDataString(orgName)}/yaml/check";
            var (statusCode, content) = await SendYamlRequestAsync(HttpMethod.Post, path, yaml, cancellationToken).ConfigureAwait(false);

            // CheckEnvironment returns the result in both 200 and 400 cases
            if (statusCode == HttpStatusCode.OK || statusCode == HttpStatusCode.BadRequest)
            {
                if (!string.IsNullOrEmpty(content))
                    return JsonSerializer.Deserialize<CheckEnvironment>(content, _jsonSerializerOptions);
                return null;
            }

            throw new EscApiException(
                $"CheckEnvironmentYaml failed with status code {(int)statusCode} ({statusCode}): {content}",
                statusCode,
                content);
        }

        /// <summary>
        /// Checks an <see cref="EnvironmentDefinition"/> for errors.
        /// The definition is serialized to YAML before sending.
        /// </summary>
        /// <returns>The check result, which may contain diagnostics even on error.</returns>
        public async Task<CheckEnvironment?> CheckEnvironmentAsync(string orgName, EnvironmentDefinition definition, CancellationToken cancellationToken = default)
        {
            var yaml = EnvironmentDefinitionSerializer.SerializeToYaml(definition);
            return await CheckEnvironmentYamlAsync(orgName, yaml, cancellationToken).ConfigureAwait(false);
        }

        #endregion

        #region Open / Read Environment

        /// <summary>
        /// Opens an environment session and returns the session ID and any diagnostics.
        /// </summary>
        /// <returns>A tuple of (sessionId, diagnostics).</returns>
        public async Task<(string Id, List<EnvironmentDiagnostic>? Diagnostics)> OpenEnvironmentAsync(string orgName, string projectName, string envName, CancellationToken cancellationToken = default)
        {
            var response = await RawApi.OpenEnvironmentAsync(orgName, projectName, envName, cancellationToken: cancellationToken).ConfigureAwait(false);
            EnsureSuccess(response, "OpenEnvironment");

            if (response.TryOk(out var openEnv))
                return (openEnv.Id, openEnv.Diagnostics?.Diagnostics);

            throw new EscApiException("OpenEnvironment returned success but no data.");
        }

        /// <summary>
        /// Opens an environment session at a specific version and returns the session ID and any diagnostics.
        /// </summary>
        /// <returns>A tuple of (sessionId, diagnostics).</returns>
        public async Task<(string Id, List<EnvironmentDiagnostic>? Diagnostics)> OpenEnvironmentAtVersionAsync(string orgName, string projectName, string envName, string version, CancellationToken cancellationToken = default)
        {
            var response = await RawApi.OpenEnvironmentAtVersionAsync(orgName, projectName, envName, version, cancellationToken: cancellationToken).ConfigureAwait(false);
            EnsureSuccess(response, "OpenEnvironmentAtVersion");

            if (response.TryOk(out var openEnv))
                return (openEnv.Id, openEnv.Diagnostics?.Diagnostics);

            throw new EscApiException("OpenEnvironmentAtVersion returned success but no data.");
        }

        /// <summary>
        /// Reads the resolved values of an open environment session.
        /// Returns both the full environment model and the unwrapped value dictionary.
        /// </summary>
        public async Task<(ModelEnvironment Environment, Dictionary<string, object?>? Values)> ReadOpenEnvironmentAsync(
            string orgName, string projectName, string envName, string openSessionId, CancellationToken cancellationToken = default)
        {
            var response = await RawApi.ReadOpenEnvironmentAsync(orgName, projectName, envName, openSessionId, cancellationToken).ConfigureAwait(false);
            EnsureSuccess(response, "ReadOpenEnvironment");

            if (response.TryOk(out var env))
            {
                var values = ValueMapper.MapValues(env);
                return (env, values);
            }

            throw new EscApiException("ReadOpenEnvironment returned success but no data.");
        }

        /// <summary>
        /// Reads a specific property from an open environment session.
        /// Returns both the raw Value and the unwrapped primitive.
        /// </summary>
        /// <remarks>
        /// The generated ReadOpenEnvironmentProperty API endpoint uses a double-slash (//) in its
        /// URL path to differentiate it from the ReadOpenEnvironment endpoint. .NET's Uri class
        /// normalizes // to /, breaking the request. As a workaround, this method reads the full
        /// environment and extracts the requested property using dot-separated path navigation.
        /// </remarks>
        public async Task<(Value Value, object? Primitive)> ReadOpenEnvironmentPropertyAsync(
            string orgName, string projectName, string envName, string openSessionId, string propertyPath, CancellationToken cancellationToken = default)
        {
            var (env, _) = await ReadOpenEnvironmentAsync(orgName, projectName, envName, openSessionId, cancellationToken).ConfigureAwait(false);
            var value = ResolvePropertyPath(env, propertyPath);
            var primitive = ValueMapper.MapValuePrimitive(value);
            return (value, primitive);
        }

        /// <summary>
        /// Navigates a dot-separated property path within a ModelEnvironment's properties.
        /// </summary>
        private Value ResolvePropertyPath(ModelEnvironment env, string propertyPath)
        {
            if (env.Properties == null)
                throw new EscApiException("Environment has no properties.");

            var segments = propertyPath.Split('.');

            if (!env.Properties.TryGetValue(segments[0], out var current))
                throw new EscApiException($"Property '{segments[0]}' not found in environment.");

            for (int i = 1; i < segments.Length; i++)
            {
                if (current.VarValue is JsonElement je && je.ValueKind == JsonValueKind.Object)
                {
                    if (!je.TryGetProperty(segments[i], out var childElement))
                        throw new EscApiException($"Property '{segments[i]}' not found at path '{string.Join(".", segments.Take(i + 1))}'.");

                    current = JsonSerializer.Deserialize<Value>(childElement.GetRawText(), _jsonSerializerOptions)
                        ?? throw new EscApiException($"Failed to deserialize value at path '{string.Join(".", segments.Take(i + 1))}'.");
                }
                else
                {
                    throw new EscApiException($"Cannot navigate into non-object value at '{string.Join(".", segments.Take(i))}'.");
                }
            }

            return current;
        }

        /// <summary>
        /// Opens and reads an environment in a single call.
        /// Returns the full environment model and the unwrapped value dictionary.
        /// </summary>
        public async Task<(ModelEnvironment Environment, Dictionary<string, object?>? Values)> OpenAndReadEnvironmentAsync(
            string orgName, string projectName, string envName, CancellationToken cancellationToken = default)
        {
            var (sessionId, _) = await OpenEnvironmentAsync(orgName, projectName, envName, cancellationToken).ConfigureAwait(false);
            return await ReadOpenEnvironmentAsync(orgName, projectName, envName, sessionId, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Opens and reads an environment at a specific version in a single call.
        /// Returns the full environment model and the unwrapped value dictionary.
        /// </summary>
        public async Task<(ModelEnvironment Environment, Dictionary<string, object?>? Values)> OpenAndReadEnvironmentAtVersionAsync(
            string orgName, string projectName, string envName, string version, CancellationToken cancellationToken = default)
        {
            var (sessionId, _) = await OpenEnvironmentAtVersionAsync(orgName, projectName, envName, version, cancellationToken).ConfigureAwait(false);
            return await ReadOpenEnvironmentAsync(orgName, projectName, envName, sessionId, cancellationToken).ConfigureAwait(false);
        }

        #endregion

        #region Environment Tags

        /// <summary>
        /// Lists all environment tags.
        /// </summary>
        public async Task<ListEnvironmentTags> ListEnvironmentTagsAsync(string orgName, string projectName, string envName, CancellationToken cancellationToken = default)
        {
            var response = await RawApi.ListEnvironmentTagsAsync(orgName, projectName, envName, cancellationToken: cancellationToken).ConfigureAwait(false);
            EnsureSuccess(response, "ListEnvironmentTags");

            if (response.TryOk(out var tags))
                return tags;

            throw new EscApiException("ListEnvironmentTags returned success but no data.");
        }

        /// <summary>
        /// Lists environment tags with pagination support.
        /// </summary>
        public async Task<ListEnvironmentTags> ListEnvironmentTagsPaginatedAsync(
            string orgName, string projectName, string envName,
            string? after = null, int? count = null, CancellationToken cancellationToken = default)
        {
            Option<string> afterOpt = after != null ? new Option<string>(after) : default;
            Option<int> countOpt = count.HasValue ? new Option<int>(count.Value) : default;

            var response = await RawApi.ListEnvironmentTagsAsync(orgName, projectName, envName, countOpt, afterOpt, cancellationToken).ConfigureAwait(false);
            EnsureSuccess(response, "ListEnvironmentTags");

            if (response.TryOk(out var tags))
                return tags;

            throw new EscApiException("ListEnvironmentTags returned success but no data.");
        }

        /// <summary>
        /// Gets an environment tag by name.
        /// </summary>
        public async Task<EnvironmentTag> GetEnvironmentTagAsync(string orgName, string projectName, string envName, string tagName, CancellationToken cancellationToken = default)
        {
            var response = await RawApi.GetEnvironmentTagAsync(orgName, projectName, envName, tagName, cancellationToken).ConfigureAwait(false);
            EnsureSuccess(response, "GetEnvironmentTag");

            if (response.TryOk(out var tag))
                return tag;

            throw new EscApiException("GetEnvironmentTag returned success but no data.");
        }

        /// <summary>
        /// Creates a new environment tag.
        /// </summary>
        public async Task<EnvironmentTag> CreateEnvironmentTagAsync(
            string orgName, string projectName, string envName,
            string tagName, string tagValue, CancellationToken cancellationToken = default)
        {
            var createTag = new CreateEnvironmentTag(tagName, tagValue);
            var response = await RawApi.CreateEnvironmentTagAsync(createTag, orgName, projectName, envName, cancellationToken).ConfigureAwait(false);
            EnsureSuccess(response, "CreateEnvironmentTag");

            if (response.TryOk(out var tag))
                return tag;

            throw new EscApiException("CreateEnvironmentTag returned success but no data.");
        }

        /// <summary>
        /// Updates an environment tag.
        /// </summary>
        public async Task<EnvironmentTag?> UpdateEnvironmentTagAsync(
            string orgName, string projectName, string envName, string tagName,
            string currentTagValue, string newTagName, string newTagValue, CancellationToken cancellationToken = default)
        {
            var update = new UpdateEnvironmentTag(
                new UpdateEnvironmentTagCurrentTag(currentTagValue),
                new UpdateEnvironmentTagNewTag(newTagName, newTagValue));

            var response = await RawApi.UpdateEnvironmentTagAsync(update, orgName, projectName, envName, tagName, cancellationToken).ConfigureAwait(false);
            EnsureSuccess(response, "UpdateEnvironmentTag");

            response.TryOk(out var tag);
            return tag;
        }

        /// <summary>
        /// Deletes an environment tag.
        /// </summary>
        public async Task DeleteEnvironmentTagAsync(string orgName, string projectName, string envName, string tagName, CancellationToken cancellationToken = default)
        {
            var response = await RawApi.DeleteEnvironmentTagAsync(orgName, projectName, envName, tagName, cancellationToken).ConfigureAwait(false);
            EnsureSuccess(response, "DeleteEnvironmentTag");
        }

        #endregion

        #region Revision Tags

        /// <summary>
        /// Lists all environment revision tags.
        /// </summary>
        public async Task<EnvironmentRevisionTags> ListEnvironmentRevisionTagsAsync(string orgName, string projectName, string envName, CancellationToken cancellationToken = default)
        {
            var response = await RawApi.ListEnvironmentRevisionTagsAsync(orgName, projectName, envName, cancellationToken: cancellationToken).ConfigureAwait(false);
            EnsureSuccess(response, "ListEnvironmentRevisionTags");

            if (response.TryOk(out var tags))
                return tags;

            throw new EscApiException("ListEnvironmentRevisionTags returned success but no data.");
        }

        /// <summary>
        /// Lists environment revision tags with pagination support.
        /// </summary>
        public async Task<EnvironmentRevisionTags> ListEnvironmentRevisionTagsPaginatedAsync(
            string orgName, string projectName, string envName,
            string? after = null, int? count = null, CancellationToken cancellationToken = default)
        {
            Option<string> afterOpt = after != null ? new Option<string>(after) : default;
            Option<int> countOpt = count.HasValue ? new Option<int>(count.Value) : default;

            var response = await RawApi.ListEnvironmentRevisionTagsAsync(orgName, projectName, envName, countOpt, afterOpt, cancellationToken).ConfigureAwait(false);
            EnsureSuccess(response, "ListEnvironmentRevisionTags");

            if (response.TryOk(out var tags))
                return tags;

            throw new EscApiException("ListEnvironmentRevisionTags returned success but no data.");
        }

        /// <summary>
        /// Gets an environment revision tag by name.
        /// </summary>
        public async Task<EnvironmentRevisionTag> GetEnvironmentRevisionTagAsync(string orgName, string projectName, string envName, string tagName, CancellationToken cancellationToken = default)
        {
            var response = await RawApi.GetEnvironmentRevisionTagAsync(orgName, projectName, envName, tagName, cancellationToken).ConfigureAwait(false);
            EnsureSuccess(response, "GetEnvironmentRevisionTag");

            if (response.TryOk(out var tag))
                return tag;

            throw new EscApiException("GetEnvironmentRevisionTag returned success but no data.");
        }

        /// <summary>
        /// Creates a new environment revision tag.
        /// </summary>
        public async Task CreateEnvironmentRevisionTagAsync(
            string orgName, string projectName, string envName,
            string tagName, int revision, CancellationToken cancellationToken = default)
        {
            var createTag = new CreateEnvironmentRevisionTag(tagName, revision);
            var response = await RawApi.CreateEnvironmentRevisionTagAsync(createTag, orgName, projectName, envName, cancellationToken).ConfigureAwait(false);
            EnsureSuccess(response, "CreateEnvironmentRevisionTag");
        }

        /// <summary>
        /// Updates an environment revision tag to point to a different revision.
        /// </summary>
        public async Task UpdateEnvironmentRevisionTagAsync(
            string orgName, string projectName, string envName,
            string tagName, int revision, CancellationToken cancellationToken = default)
        {
            var update = new UpdateEnvironmentRevisionTag(revision);
            var response = await RawApi.UpdateEnvironmentRevisionTagAsync(update, orgName, projectName, envName, tagName, cancellationToken).ConfigureAwait(false);
            EnsureSuccess(response, "UpdateEnvironmentRevisionTag");
        }

        /// <summary>
        /// Deletes an environment revision tag.
        /// </summary>
        public async Task DeleteEnvironmentRevisionTagAsync(string orgName, string projectName, string envName, string tagName, CancellationToken cancellationToken = default)
        {
            var response = await RawApi.DeleteEnvironmentRevisionTagAsync(orgName, projectName, envName, tagName, cancellationToken).ConfigureAwait(false);
            EnsureSuccess(response, "DeleteEnvironmentRevisionTag");
        }

        #endregion

        #region Revisions

        /// <summary>
        /// Lists all revisions of the environment.
        /// </summary>
        public async Task<List<EnvironmentRevision>> ListEnvironmentRevisionsAsync(string orgName, string projectName, string envName, CancellationToken cancellationToken = default)
        {
            var response = await RawApi.ListEnvironmentRevisionsAsync(orgName, projectName, envName, cancellationToken: cancellationToken).ConfigureAwait(false);
            EnsureSuccess(response, "ListEnvironmentRevisions");

            if (response.TryOk(out var revisions))
                return revisions;

            throw new EscApiException("ListEnvironmentRevisions returned success but no data.");
        }

        /// <summary>
        /// Lists revisions of the environment with pagination support.
        /// </summary>
        public async Task<List<EnvironmentRevision>> ListEnvironmentRevisionsPaginatedAsync(
            string orgName, string projectName, string envName,
            int? before = null, int? count = null, CancellationToken cancellationToken = default)
        {
            Option<int> beforeOpt = before.HasValue ? new Option<int>(before.Value) : default;
            Option<int> countOpt = count.HasValue ? new Option<int>(count.Value) : default;

            var response = await RawApi.ListEnvironmentRevisionsAsync(orgName, projectName, envName, beforeOpt, countOpt, cancellationToken).ConfigureAwait(false);
            EnsureSuccess(response, "ListEnvironmentRevisions");

            if (response.TryOk(out var revisions))
                return revisions;

            throw new EscApiException("ListEnvironmentRevisions returned success but no data.");
        }

        #endregion

        #region Helpers

        private static void EnsureSuccess(IApiResponse response, string operationName)
        {
            if (!response.IsSuccessStatusCode)
            {
                throw new EscApiException(
                    $"{operationName} failed with status code {(int)response.StatusCode} ({response.StatusCode}): {response.RawContent}",
                    response.StatusCode,
                    response.RawContent);
            }
        }

        /// <summary>
        /// Sends an HTTP request with a raw YAML body, bypassing the generated code's
        /// JsonSerializer.Serialize(string) bug that JSON-encodes the YAML string.
        /// </summary>
        private async Task<(HttpStatusCode StatusCode, string Content)> SendYamlRequestAsync(
            HttpMethod method, string path, string yaml, CancellationToken cancellationToken)
        {
            var api = (EscApi)RawApi;
            var baseUri = api.HttpClient.BaseAddress!;

            var uriBuilder = new UriBuilder
            {
                Scheme = baseUri.Scheme,
                Host = baseUri.Host,
                Port = baseUri.Port,
                Path = path
            };

            using var request = new HttpRequestMessage(method, uriBuilder.Uri);
            request.Content = new StringContent(yaml, Encoding.UTF8);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/x-yaml");
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var token = (ApiKeyToken)await api.ApiKeyProvider.GetAsync("Authorization", cancellationToken).ConfigureAwait(false);
            token.UseInHeader(request);

            using var response = await api.HttpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return (response.StatusCode, content);
        }

        #endregion

        #region IDisposable

        /// <summary>
        /// Disposes the underlying service provider and HttpClient resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes managed resources.
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _serviceProvider.Dispose();
                }
                _disposed = true;
            }
        }

        #endregion
    }
}
