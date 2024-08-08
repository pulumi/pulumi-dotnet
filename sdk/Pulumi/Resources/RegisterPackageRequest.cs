using System.Collections.Generic;

namespace Pulumi;

public sealed class RegisterPackageRequest
{
    public sealed class PackageParameterization
    {
        /// <summary>
        /// The name of the parameterized package
        /// </summary>
        public readonly string Name;
        /// <summary>
        /// The version of the parameterized package
        /// </summary>
        public readonly string Version;
        /// <summary>
        /// The paramter value for the parameterized package
        /// </summary>
        public readonly byte[] Value;

        public PackageParameterization(string name, string version, byte[] value)
        {
            Name = name;
            Version = version;
            Value = value;
        }
    }

    /// <summary>
    /// The plugin name
    /// </summary>
    public readonly string Name;
    /// <summary>
    /// The plugin version
    /// </summary>
    public readonly string Version;
    /// <summary>
    /// the optional plugin download url.
    /// </summary>
    public readonly string DownloadUrl;
    /// <summary>
    /// the optional plugin checksums
    /// </summary>
    public readonly Dictionary<string, byte[]> Checksums;

    public readonly PackageParameterization? Parameterization;

    public RegisterPackageRequest(
        string name,
        string version,
        string? downloadUrl,
        Dictionary<string, byte[]>? checksums = null,
        PackageParameterization? parameterization = null)
    {
        Name = name;
        Version = version;
        DownloadUrl = downloadUrl ?? "";
        Checksums = checksums ?? new Dictionary<string, byte[]>();
        Parameterization = parameterization;
    }
}
