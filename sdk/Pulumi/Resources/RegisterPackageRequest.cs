using System.Collections.Generic;

namespace Pulumi;

public sealed class RegisterPackageRequest
{
    public sealed class PackageParameterization
    {
        /// <summary>
        /// The name of the parameterized package
        /// </summary>
        public string Name { get; }
        /// <summary>
        /// The version of the parameterized package
        /// </summary>
        public string Version { get; }
        /// <summary>
        /// The paramter value for the parameterized package
        /// </summary>
        public byte[] Value { get; }

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
    public string Name { get; }
    /// <summary>
    /// The plugin version
    /// </summary>
    public string Version { get; }
    /// <summary>
    /// the optional plugin download url.
    /// </summary>
    public string DownloadUrl { get; }
    /// <summary>
    /// the optional plugin checksums
    /// </summary>
    public Dictionary<string, byte[]> Checksums { get; }

    public PackageParameterization? Parameterization { get; }

    /// <summary>
    /// The optional extension parameterization for this package. An extension is
    /// layered onto the base provider identified by <see cref="Name"/> rather than
    /// replacing it, so a package sets either <see cref="Parameterization"/> or
    /// <see cref="Extension"/>, never both.
    /// </summary>
    public PackageParameterization? Extension { get; }

    public RegisterPackageRequest(
        string name,
        string version,
        string? downloadUrl,
        Dictionary<string, byte[]>? checksums = null,
        PackageParameterization? parameterization = null,
        PackageParameterization? extension = null)
    {
        Name = name;
        Version = version;
        DownloadUrl = downloadUrl ?? "";
        Checksums = checksums ?? new Dictionary<string, byte[]>();
        Parameterization = parameterization;
        Extension = extension;
    }
}
