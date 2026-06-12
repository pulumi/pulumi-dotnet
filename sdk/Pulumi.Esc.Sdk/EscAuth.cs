// Copyright 2024, Pulumi Corporation.  All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Pulumi.Esc.Sdk
{
    /// <summary>
    /// Represents the credentials stored in the Pulumi workspace.
    /// </summary>
    internal class PulumiCredentials
    {
        [JsonPropertyName("current")]
        public string? Current { get; set; }

        [JsonPropertyName("accessTokens")]
        public Dictionary<string, string>? AccessTokens { get; set; }

        [JsonPropertyName("accounts")]
        public Dictionary<string, PulumiAccountCredentials>? Accounts { get; set; }
    }

    /// <summary>
    /// Represents the account credentials stored in the Pulumi workspace.
    /// </summary>
    internal class PulumiAccountCredentials
    {
        [JsonPropertyName("accessToken")]
        public string? AccessToken { get; set; }

        [JsonPropertyName("username")]
        public string? Username { get; set; }

        [JsonPropertyName("organizations")]
        public List<string>? Organizations { get; set; }
    }

    /// <summary>
    /// Represents the ESC credentials file that overrides the current account.
    /// </summary>
    internal class EscCredentials
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }
    }

    /// <summary>
    /// Provides authentication helpers for the Pulumi ESC SDK.
    /// </summary>
    public static class EscAuth
    {
        private const string DefaultPulumiCloudUrl = "https://api.pulumi.com";
        private const string PulumiAccessTokenEnvVar = "PULUMI_ACCESS_TOKEN";
        private const string PulumiBackendUrlEnvVar = "PULUMI_BACKEND_URL";

        /// <summary>
        /// Gets the default access token by checking (in order):
        /// 1. PULUMI_ACCESS_TOKEN environment variable
        /// 2. Currently logged-in account in Pulumi CLI or ESC CLI
        /// </summary>
        /// <returns>The access token string.</returns>
        /// <exception cref="InvalidOperationException">When no access token can be found.</exception>
        public static string GetDefaultAccessToken()
        {
            // Check environment variable first
            var envToken = Environment.GetEnvironmentVariable(PulumiAccessTokenEnvVar);
            if (!string.IsNullOrEmpty(envToken))
            {
                return envToken;
            }

            // Try to read from workspace credentials
            var (credentials, escCredentials) = ReadWorkspaceCredentials();
            var currentUrl = GetCurrentUrl(credentials, escCredentials);

            if (currentUrl != null && credentials?.Accounts != null &&
                credentials.Accounts.TryGetValue(currentUrl, out var account) &&
                !string.IsNullOrEmpty(account.AccessToken))
            {
                return account.AccessToken;
            }

            // Fall back to accessTokens map
            if (currentUrl != null && credentials?.AccessTokens != null &&
                credentials.AccessTokens.TryGetValue(currentUrl, out var token) &&
                !string.IsNullOrEmpty(token))
            {
                return token;
            }

            throw new InvalidOperationException(
                "No default Pulumi Access Token found. Either set the PULUMI_ACCESS_TOKEN " +
                "environment variable, or log in using the Pulumi or ESC CLI.");
        }

        /// <summary>
        /// Gets the default backend URL by checking (in order):
        /// 1. PULUMI_BACKEND_URL environment variable
        /// 2. Currently logged-in account URL from Pulumi CLI or ESC CLI
        /// </summary>
        /// <returns>The backend URL string.</returns>
        public static string GetDefaultBackendUrl()
        {
            var envUrl = Environment.GetEnvironmentVariable(PulumiBackendUrlEnvVar);
            if (!string.IsNullOrEmpty(envUrl))
            {
                return envUrl;
            }

            var (credentials, escCredentials) = ReadWorkspaceCredentials();
            var currentUrl = GetCurrentUrl(credentials, escCredentials);

            return currentUrl ?? DefaultPulumiCloudUrl;
        }

        /// <summary>
        /// Converts a backend URL (e.g. "https://api.pulumi.com") to the ESC API URL.
        /// </summary>
        /// <param name="backendUrl">The Pulumi backend URL.</param>
        /// <returns>The ESC API base URL (e.g. "https://api.pulumi.com/api/esc").</returns>
        public static string GetEscApiUrl(string backendUrl)
        {
            var uri = new Uri(backendUrl.TrimEnd('/'));
            return $"{uri.Scheme}://{uri.Host}{(uri.IsDefaultPort ? "" : $":{uri.Port}")}/api/esc";
        }

        private static string? GetCurrentUrl(PulumiCredentials? credentials, EscCredentials? escCredentials)
        {
            // ESC CLI credentials override the default Pulumi CLI "current" URL
            if (!string.IsNullOrEmpty(escCredentials?.Name))
            {
                return escCredentials!.Name;
            }

            return credentials?.Current;
        }

        private static (PulumiCredentials?, EscCredentials?) ReadWorkspaceCredentials()
        {
            var pulumiHome = GetPulumiHomeDir();

            PulumiCredentials? credentials = null;
            EscCredentials? escCredentials = null;

            // Read main Pulumi credentials
            var credentialsPath = Path.Combine(pulumiHome, "credentials.json");
            if (File.Exists(credentialsPath))
            {
                try
                {
                    var json = File.ReadAllText(credentialsPath);
                    credentials = JsonSerializer.Deserialize<PulumiCredentials>(json, JsonDefaults.Options);
                }
                catch (JsonException)
                {
                    // Ignore malformed credentials files
                }
            }

            // Read ESC CLI credentials (overrides "current" account)
            var escCredentialsPath = Path.Combine(pulumiHome, ".esc", "credentials.json");
            if (File.Exists(escCredentialsPath))
            {
                try
                {
                    var json = File.ReadAllText(escCredentialsPath);
                    escCredentials = JsonSerializer.Deserialize<EscCredentials>(json, JsonDefaults.Options);
                }
                catch (JsonException)
                {
                    // Ignore malformed credentials files
                }
            }

            return (credentials, escCredentials);
        }

        private static string GetPulumiHomeDir()
        {
            // Allow override via PULUMI_HOME environment variable
            var pulumiHome = Environment.GetEnvironmentVariable("PULUMI_HOME");
            if (!string.IsNullOrEmpty(pulumiHome))
            {
                return pulumiHome;
            }

            // Default to ~/.pulumi
            var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(homeDir, ".pulumi");
        }
    }
}
