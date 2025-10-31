public static class DenoExtensions
{
    public static IResourceBuilder<ExecutableResource> AddDeno(this IDistributedApplicationBuilder builder, string name, string workingDirectory, string command)
    {
        if(OperatingSystem.IsWindows())
        {
            var dir = Directory.GetCurrentDirectory();
            var frontendDirectory = Path.Combine(dir, workingDirectory.Replace("/", "\\"));
            return builder.AddExecutable(name, "powershell", frontendDirectory, "deno", "task", command);
        }
        else
        {
            return builder.AddExecutable(name, "bash", workingDirectory, "start-dev.sh");
        }            
    }
}
