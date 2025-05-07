// Copyright 2016-2025, Pulumi Corporation

namespace Pulumi.Automation
{
    /// <summary>
    /// Credentials for the remote execution Docker image.
    /// </summary>
    public class DockerImageCredentials
    {
        /// <summary>
        /// The username for the image.
        /// </summary>
        public string Username { get; set; }

        /// <summary>
        /// The password for the image.
        /// </summary>
        public string Password { get; set; }

        public DockerImageCredentials(string username, string password)
        {
            this.Username = username;
            this.Password = password;
        }
    }
}
