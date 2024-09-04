namespace Pulumi;

internal static class DeploymentExtensions
{
    public static string GetCurrentStackName(this IDeployment deployment)
    {
        return $"{Deployment.Instance.ProjectName}-{Deployment.Instance.StackName}";
    }
}
