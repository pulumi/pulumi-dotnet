// Copyright 2016-2025, Pulumi Corporation

namespace Pulumi.Automation
{
    /// <summary>
    /// Information about the remote execution image.
    /// </summary>
    public class ExecutorImage
    {
        public ExecutorImage(string image)
        {
            this.Image = image;
        }

        /// <summary>
        /// The Docker image to use.
        /// </summary>
        public string Image { get; set; }

        /// <summary>
        /// Credentials for the remote execution Docker image.
        /// </summary>
        public DockerImageCredentials? Credentials { get; set; }
    }
}
