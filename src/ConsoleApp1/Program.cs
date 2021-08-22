using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Locator;
using Microsoft.Build.Logging;
using Microsoft.Build.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace ConsoleApp1
{
    public static class Program
    {
        public static async Task<int> Main(string[] args)
        {
            if (args.Any())
            {
                // MSBuild attempts to launch this app as a node, sleep for 5 seconds so it shows up in Task Manager
                Thread.Sleep(TimeSpan.FromSeconds(5));
                return 1;
            }

            // Determine the instance of MSBuild to use
#if NETCOREAPP3_1_OR_GREATER
            // For .NET Core, use the version of MSBuild that matches the current runtime
            var instance = MSBuildLocator.QueryVisualStudioInstances().FirstOrDefault(i => i.Version.Major == Environment.Version.Major);
#else
            // For .NET Framework, use the version of MSBuild that is 16
            VisualStudioInstance instance = MSBuildLocator.QueryVisualStudioInstances().FirstOrDefault(i => i.Version.Major == 16);
#endif

            if (instance == null)
            {
                return ShowMSBuildInstanceError();
            }

            // Register the MSBuild instance
            if (Environment.Is64BitProcess && RuntimeInformation.FrameworkDescription.Contains("Framework"))
            {
                MSBuildLocator.RegisterMSBuildPath(Path.Combine(instance.MSBuildPath, "amd64"));
            }
            else
            {
                MSBuildLocator.RegisterInstance(instance);
            }

            return await BuildProjectAsync("Restore");
        }

        private static async Task<int> BuildProjectAsync(params string[] targets)
        {
            ILogger[] loggers = new ILogger[]
            {
                new ConsoleLogger(LoggerVerbosity.Normal)
                {
                    Parameters = "ForceNoAlign"
                }
            };

            MuxLogger muxLogger = new MuxLogger()
            {
                Verbosity = LoggerVerbosity.Diagnostic
            };

            LoggerDescription forwardingLoggerDescription = new LoggerDescription(
                    loggerClassName: typeof(ConfigurableForwardingLogger).FullName,
                    loggerAssemblyName: typeof(ConfigurableForwardingLogger).Assembly.GetName().FullName,
                    loggerAssemblyFile: null,
                    loggerSwitchParameters: "",
                    verbosity: muxLogger.Verbosity);

            ForwardingLoggerRecord forwardingLoggerRecord = new ForwardingLoggerRecord(
                muxLogger,
                forwardingLoggerDescription);

            Dictionary<string, string> globalProperties = new Dictionary<string, string>();

            using ProjectCollection projectCollection = new ProjectCollection(globalProperties, null, null, ToolsetDefinitionLocations.Default, Environment.ProcessorCount, onlyLogCriticalEvents: false, loadProjectsReadOnly: true);

            FileInfo projectFileInfo = CreateProjectFile();

            BuildManager buildManager = new BuildManager("ConsoleApp1");

            BuildParameters buildParameters = new BuildParameters(projectCollection)
            {
                DisableInProcNode = true,
                EnableNodeReuse = false,
                MaxNodeCount = Environment.ProcessorCount,
                ResetCaches = true,
                ForwardingLoggers = new[]
                {
                    forwardingLoggerRecord,
                }
            };

            buildManager.BeginBuild(buildParameters);
            try
            {
                BuildRequestData buildRequest = new BuildRequestData(
                    projectFileInfo.FullName,
                    globalProperties: new Dictionary<string, string>(),
                    toolsVersion: null,
                    targetsToBuild: targets,
                    hostServices: null);

                BuildSubmission buildSubmission = buildManager.PendBuildRequest(buildRequest);

                foreach (ILogger logger in loggers)
                {
                    muxLogger.RegisterLogger(buildSubmission.SubmissionId, logger);
                }

                TaskCompletionSource<BuildResult> taskCompletionSource = new TaskCompletionSource<BuildResult>();

                buildManager.SetCurrentHost(@"C:\Program Files\dotnet\dotnet.exe");

                buildSubmission.ExecuteAsync(submission =>
                    {
                        taskCompletionSource.TrySetResult(buildSubmission.BuildResult);

                        muxLogger.UnregisterLoggers(buildSubmission.SubmissionId);
                    },
                    context: null);

                BuildResult buildResult = await taskCompletionSource.Task;

                if (buildResult.OverallResult != BuildResultCode.Success)
                {
                    if (!string.IsNullOrEmpty(buildResult.Exception?.Message))
                    {
                        Console.WriteLine(buildResult.Exception?.Message);

                        Console.WriteLine();
                        Console.WriteLine("Loaded assemblies:");
                        foreach (string line in AppDomain.CurrentDomain.GetAssemblies().Where(i => !i.IsDynamic && !string.IsNullOrEmpty(i.Location)).OrderBy(i => i.FullName).Select(i => $"{i.FullName} / {i.Location}"))
                        {
                            Console.WriteLine(line);
                        }
                    }
                }
            }
            finally
            {
                buildManager.EndBuild();
            }

            return 0;
        }

        private static FileInfo CreateProjectFile()
        {
            // Create or clean up a directory for the the project
            DirectoryInfo directoryInfo = Directory.CreateDirectory("Projects");

            directoryInfo.Delete(recursive: true);

            directoryInfo.Create();

            FileInfo projectFileInfo = new FileInfo(Path.Combine(directoryInfo.FullName, "ProjectA", "ProjectA.csproj"));

            projectFileInfo.Directory.Create();

            File.WriteAllText(
                projectFileInfo.FullName,
                @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net5.0</TargetFramework>
  </PropertyGroup>
</Project>");

            return projectFileInfo;
        }

        private static int ShowMSBuildInstanceError()
        {
            Console.Error.WriteLine("Unable to find an MSBuild instance to use");

            Console.WriteLine("Found the following instances:");

            foreach (VisualStudioInstance instance in MSBuildLocator.QueryVisualStudioInstances())
            {
                Console.WriteLine($"DiscoveryType: {instance.DiscoveryType}");
                Console.WriteLine($"MSBuildPath: {instance.MSBuildPath}");
                Console.WriteLine($"Name: {instance.Name}");
                Console.WriteLine($"Version: {instance.Version}");
                Console.WriteLine($"VisualStudioRootPath: {instance.VisualStudioRootPath}");
                Console.WriteLine("-------------------------------------------------------------");
            }

            return 1;
        }
    }
}