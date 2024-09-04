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
        /// Returns the current organization name.
        /// </summary>
        public string OrganizationName => _deployment.OrganizationName;

        /// <summary>
        /// Whether or not the application is currently being previewed or actually applied.
        /// </summary>
        public bool IsDryRun => _deployment.IsDryRun;

        /// <summary>
        /// Dynamically invokes the function '<paramref name="token"/>', which is offered by a
        /// provider plugin.
        /// <para/>
        /// The result of <see cref="Invoke"/> will be a <see cref="Output"/> resolved to the
        /// result value of the provider plugin.
        /// <para/>
        /// Similar to the earlier <see cref="InvokeAsync"/>, but supports passing input values
        /// and returns an Output value.
        /// <para/>
        /// The <paramref name="args"/> inputs can be a bag of computed values(including, `T`s,
        /// <see cref="Task{TResult}"/>s, <see cref="Output{T}"/>s etc.).
        /// </summary>
        public Output<T> Invoke<T>(
            string token,
            InvokeArgs args,
            InvokeOptions? options = null,
            RegisterPackageRequest? registerPackageRequest = null)
            => _deployment.Invoke<T>(token, args, options, registerPackageRequest);

        /// <summary>
        /// Dynamically invokes the function '<paramref name="token"/>', which is offered by a
        /// provider plugin.
        /// <para/>
        /// The result of <see cref="InvokeSingle"/> will be a <see cref="Output"/> resolved to the
        /// result value of the provider plugin.
        /// <para/>
        /// Similar to the earlier <see cref="InvokeSingleAsync"/>, but supports passing input values
        /// and returns an Output value.
        /// <para/>
        /// The <paramref name="args"/> inputs can be a bag of computed values(including, `T`s,
        /// <see cref="Task{TResult}"/>s, <see cref="Output{T}"/>s etc.).
        /// </summary>
        public Output<T> InvokeSingle<T>(
            string token,
            InvokeArgs args,
            InvokeOptions? options = null,
            RegisterPackageRequest? registerPackageRequest = null)
            => _deployment.InvokeSingle<T>(token, args, options, registerPackageRequest);

        /// <summary>
        /// Dynamically invokes the function '<paramref name="token"/>', which is offered by a
        /// provider plugin.
        /// <para/>
        /// The result of <see cref="InvokeAsync"/> will be a <see cref="Task"/> resolved to the
        /// result value of the provider plugin.
        /// <para/>
        /// The <paramref name="args"/> inputs can be a bag of computed values(including, `T`s,
        /// <see cref="Task{TResult}"/>s, <see cref="Output{T}"/>s etc.).
        /// </summary>
        public Task<T> InvokeAsync<T>(
            string token,
            InvokeArgs args,
            InvokeOptions? options = null,
            RegisterPackageRequest? registerPackageRequest = null)
            => _deployment.InvokeAsync<T>(token, args, options, registerPackageRequest);

        /// <summary>
        /// Dynamically invokes the function '<paramref name="token"/>', which is offered by a
        /// provider plugin.
        /// <para/>
        /// The result of <see cref="InvokeSingleAsync"/> will be a <see cref="Task"/> resolved to the
        /// result value of the provider plugin which is expected to be a dictionary with single value.
        /// <para/>
        /// The <paramref name="args"/> inputs can be a bag of computed values(including, `T`s,
        /// <see cref="Task{TResult}"/>s, <see cref="Output{T}"/>s etc.).
        /// </summary>
        public Task<T> InvokeSingleAsync<T>(
            string token,
            InvokeArgs args,
            InvokeOptions? options = null,
            RegisterPackageRequest? registerPackageRequest = null)
            => _deployment.InvokeSingleAsync<T>(token, args, options, registerPackageRequest);

        /// <summary>
        /// Same as <see cref="InvokeAsync{T}(string, InvokeArgs, InvokeOptions, RegisterPackageRequest)"/>, however the
        /// return value is ignored.
        /// </summary>
        public Task InvokeAsync(string token, InvokeArgs args, InvokeOptions? options = null, RegisterPackageRequest? registerPackageRequest = null)
            => _deployment.InvokeAsync(token, args, options, registerPackageRequest);

        /// <summary>
        /// Dynamically calls the function '<paramref name="token"/>', which is offered by a
        /// provider plugin.
        /// <para/>
        /// The result of <see cref="Call{T}"/> will be a <see cref="Output{T}"/> resolved to the
        /// result value of the provider plugin.
        /// <para/>
        /// The <paramref name="args"/> inputs can be a bag of computed values(including, `T`s,
        /// <see cref="Task{TResult}"/>s, <see cref="Output{T}"/>s etc.).
        /// </summary>
        public Output<T> Call<T>(
            string token,
            CallArgs args,
            Resource? self = null,
            CallOptions? options = null,
            RegisterPackageRequest? registerPackageRequest = null)
            => _deployment.Call<T>(token, args, self, options, registerPackageRequest);

        /// <summary>
        /// Same as <see cref="Call{T}"/>, however the return value is ignored.
        /// </summary>
        public void Call(
            string token,
            CallArgs args,
            Resource? self = null,
            CallOptions? options = null,
            RegisterPackageRequest? registerPackageRequest = null)
            => _deployment.Call(token, args, self, options, registerPackageRequest);

        internal IDeploymentInternal Internal => (IDeploymentInternal)_deployment;
    }
}
