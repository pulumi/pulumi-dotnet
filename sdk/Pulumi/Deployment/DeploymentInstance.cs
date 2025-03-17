using System.Threading.Tasks;

namespace Pulumi
{
    /// <summary>
    /// Metadata of the deployment that is currently running. Accessible via <see cref="Deployment.Instance"/>.
    /// </summary>
    public sealed class DeploymentInstance : IDeployment
    {
        private readonly IDeployment _deployment;

        internal DeploymentInstance(IDeployment deployment)
        {
            _deployment = deployment;
        }

        /// <summary>
        /// Returns the current stack name.
        /// </summary>
        public string StackName => _deployment.StackName;

        /// <summary>
        /// Returns the current project name.
        /// </summary>
        public string ProjectName => _deployment.ProjectName;

        /// <summary>
        /// Root directory, that is the location of the Pulumi.yaml file.
        /// </summary>
        public string RootDirectory => _deployment.RootDirectory;

        /// <summary>
        /// Returns the current organization name.
        /// </summary>
        public string OrganizationName => _deployment.OrganizationName;

        /// <summary>
        /// Whether or not the application is currently being previewed or actually applied.
        /// </summary>
        public bool IsDryRun => _deployment.IsDryRun;

        /// <inheritdoc />
        public Output<T> Invoke<T>(
            string token,
            InvokeArgs args,
            InvokeOptions? options,
            RegisterPackageRequest? registerPackageRequest)
            => _deployment.Invoke<T>(token, args, options, registerPackageRequest);

        /// <inheritdoc />
        public Output<T> Invoke<T>(
            string token,
            InvokeArgs args,
            InvokeOutputOptions options,
            RegisterPackageRequest? registerPackageRequest)
            => _deployment.Invoke<T>(token, args, options, registerPackageRequest);

        /// <inheritdoc />
        public Output<T> Invoke<T>(
            string token,
            InvokeArgs args,
            InvokeOptions? options = null)
            => _deployment.Invoke<T>(token, args, options, null);

        /// <inheritdoc />
        public Output<T> Invoke<T>(
            string token,
            InvokeArgs args,
            InvokeOutputOptions options)
            => _deployment.Invoke<T>(token, args, options, null);

        /// <inheritdoc />
        public Output<T> InvokeSingle<T>(
            string token,
            InvokeArgs args,
            InvokeOptions? options,
            RegisterPackageRequest? registerPackageRequest)
            => _deployment.InvokeSingle<T>(token, args, options, registerPackageRequest);

        /// <inheritdoc />
        public Output<T> InvokeSingle<T>(
            string token,
            InvokeArgs args,
            InvokeOutputOptions options,
            RegisterPackageRequest? registerPackageRequest)
            => _deployment.InvokeSingle<T>(token, args, options, registerPackageRequest);

        /// <inheritdoc />
        public Output<T> InvokeSingle<T>(
            string token,
            InvokeArgs args,
            InvokeOptions? options = null)
            => _deployment.InvokeSingle<T>(token, args, options, null);

        /// <inheritdoc />
        public Output<T> InvokeSingle<T>(
            string token,
            InvokeArgs args,
            InvokeOutputOptions options)
            => _deployment.InvokeSingle<T>(token, args, options, null);

        /// <inheritdoc />
        public Task<T> InvokeAsync<T>(
            string token,
            InvokeArgs args,
            InvokeOptions? options,
            RegisterPackageRequest? registerPackageRequest)
            => _deployment.InvokeAsync<T>(token, args, options, registerPackageRequest);

        /// <inheritdoc />
        public Task<T> InvokeAsync<T>(
            string token,
            InvokeArgs args,
            InvokeOptions? options = null)
            => _deployment.InvokeAsync<T>(token, args, options, null);

        /// <inheritdoc />
        public Task<T> InvokeSingleAsync<T>(
            string token,
            InvokeArgs args,
            InvokeOptions? options,
            RegisterPackageRequest? registerPackageRequest)
            => _deployment.InvokeSingleAsync<T>(token, args, options, registerPackageRequest);

        /// <inheritdoc />
        public Task<T> InvokeSingleAsync<T>(
            string token,
            InvokeArgs args,
            InvokeOptions? options = null)
            => _deployment.InvokeSingleAsync<T>(token, args, options, null);

        /// <summary>
        /// Same as <see cref="InvokeAsync{T}(string, InvokeArgs, InvokeOptions, RegisterPackageRequest)"/>, however the
        /// return value is ignored.
        /// </summary>
        public Task InvokeAsync(string token, InvokeArgs args, InvokeOptions? options, RegisterPackageRequest? registerPackageRequest)
            => _deployment.InvokeAsync(token, args, options, registerPackageRequest);

        /// <summary>
        /// Same as <see cref="InvokeAsync{T}(string, InvokeArgs, InvokeOptions, RegisterPackageRequest)"/>, however the
        /// return value is ignored.
        /// </summary>
        public Task InvokeAsync(string token, InvokeArgs args, InvokeOptions? options = null)
            => _deployment.InvokeAsync(token, args, options, null);

        /// <inheritdoc />
        public Output<T> Call<T>(
            string token,
            CallArgs args,
            Resource? self,
            CallOptions? options,
            RegisterPackageRequest? registerPackageRequest)
            => _deployment.Call<T>(token, args, self, options, registerPackageRequest);

        /// <inheritdoc />
        public Output<T> Call<T>(
            string token,
            CallArgs args,
            Resource? self = null,
            CallOptions? options = null)
            => _deployment.Call<T>(token, args, self, options, null);

        /// <inheritdoc />
        public void Call(
            string token,
            CallArgs args,
            Resource? self,
            CallOptions? options,
            RegisterPackageRequest? registerPackageRequest)
            => _deployment.Call(token, args, self, options, registerPackageRequest);

        /// <inheritdoc />
        public void Call(
            string token,
            CallArgs args,
            Resource? self = null,
            CallOptions? options = null)
            => _deployment.Call(token, args, self, options, null);

        internal IDeploymentInternal Internal => (IDeploymentInternal)_deployment;
    }
}
