public static class DenoExtensions
{
    public static IResourceBuilder<ExecutableResource> AddDeno(this IDistributedApplicationBuilder builder, string name, string workingDirectory, string command)
    {
        return builder.AddExecutable(name, "bash", workingDirectory, "start-dev.sh");
    }
}
