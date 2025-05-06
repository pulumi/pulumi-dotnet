// Copyright 2016-2022, Pulumi Corporation

namespace Pulumi.Automation
{
    /// <summary>
    /// Information about the remote execution image.
    /// </summary>
    public class ExecutorImage
    {
        /// <summary>
        /// The Docker image to use.
        /// </summary>
        public string Image { get; set; }

        /// <summary>
        /// Credentials for the remote execution Docker image.
        /// </summary>
        public DockerImageCredentials? DockerImageCredentials { get; set; }
    }
}
