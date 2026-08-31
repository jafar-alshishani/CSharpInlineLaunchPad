using System;
using System.ComponentModel.Design;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CSharpInlineLaunchPad.Terminal;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Task = System.Threading.Tasks.Task;

namespace CSharpInlineLaunchPad.Commands
{
    internal sealed class RunCSharpCommand
    {
        public const int CommandId = 0x0100;

        public static readonly Guid CommandSet =
            new Guid("7F4A7D6D-6B37-4D5D-9B48-1A7A3F9B4A11");

        private readonly AsyncPackage package;

        private RunCSharpCommand(
            AsyncPackage package,
            OleMenuCommandService commandService)
        {
            this.package = package;

            var commandId = new CommandID(
                CommandSet,
                CommandId);

            var command = new OleMenuCommand(
                OnExecute,
                commandId);

            commandService.AddCommand(command);
        }

        public static async Task InitializeAsync(
            AsyncPackage package)
        {
            await ThreadHelper.JoinableTaskFactory
                .SwitchToMainThreadAsync();

            var commandService =
                await package.GetServiceAsync(
                    typeof(IMenuCommandService))
                as OleMenuCommandService;

            if (commandService != null)
            {
                _ = new RunCSharpCommand(
                    package,
                    commandService);
            }
        }

        private void OnExecute(object sender, EventArgs e)
        {
            // Suppress VSSDK007 for top-level fire-and-forget command handler
#pragma warning disable VSSDK007
            _ = this.package.JoinableTaskFactory.RunAsync(async delegate
            {
                await ExecuteAsync();
            });
#pragma warning restore VSSDK007
        }

        private async Task ExecuteAsync()
        {
            await ThreadHelper.JoinableTaskFactory
                .SwitchToMainThreadAsync();

            try
            {
                var dte =
                    await package.GetServiceAsync<
                        EnvDTE.DTE,
                        EnvDTE.DTE>();

                if (dte?.Solution == null ||
                    string.IsNullOrEmpty(dte.Solution.FullName))
                {
                    ShowError("Please open a C# solution first.");
                    return;
                }

                string solutionDirectory =
                    Path.GetDirectoryName(
                        dte.Solution.FullName)!;

                string? projectPath = FindTargetProject(dte, solutionDirectory);

                if (projectPath == null)
                {
                    ShowError("C# Inline LaunchPad could not find a valid .csproj file in this solution.");
                    return;
                }

                await VsTerminalLauncher.LaunchAsync(
                    solutionDirectory,
                    projectPath,
                    CancellationToken.None);
            }
            catch (Exception ex)
            {
                ShowError(
                    "C# Inline LaunchPad could not run the project.\n\n" +
                    ex.Message);
            }
        }

        private static string? FindTargetProject(EnvDTE.DTE dte, string solutionDirectory)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            // 1. Priority: Startup Project configured in the solution
            try
            {
                if (dte.Solution?.SolutionBuild?.StartupProjects is Array startupProjects && startupProjects.Length > 0)
                {
                    foreach (var item in startupProjects)
                    {
                        if (item is string relPath && !string.IsNullOrWhiteSpace(relPath))
                        {
                            string fullPath = Path.IsPathRooted(relPath) ? relPath : Path.Combine(solutionDirectory, relPath);
                            if (File.Exists(fullPath) && fullPath.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
                            {
                                return fullPath;
                            }
                        }
                    }
                }
            }
            catch { }

            // 2. Secondary: Active Document's Containing Project
            try
            {
                if (dte.ActiveDocument?.ProjectItem?.ContainingProject != null)
                {
                    string activeProjPath = dte.ActiveDocument.ProjectItem.ContainingProject.FullName;
                    if (!string.IsNullOrEmpty(activeProjPath) && File.Exists(activeProjPath) && activeProjPath.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
                    {
                        return activeProjPath;
                    }
                }
            }
            catch { }

            // 3. Tertiary: Loaded Solution Projects via DTE (prefer non-test projects)
            try
            {
                if (dte.Solution != null)
                {
                    var projects = GetAllProjects(dte.Solution);
                    var validProjects = new System.Collections.Generic.List<string>();
                    foreach (var p in projects)
                    {
                        try
                        {
                            string fullName = p.FullName;
                            if (!string.IsNullOrEmpty(fullName) && File.Exists(fullName) && fullName.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
                            {
                                validProjects.Add(fullName);
                            }
                        }
                        catch { }
                    }

                    var bestProject = validProjects
                        .OrderBy(p => p.IndexOf("Test", StringComparison.OrdinalIgnoreCase) >= 0 ? 1 : 0)
                        .FirstOrDefault();

                    if (!string.IsNullOrEmpty(bestProject))
                    {
                        return bestProject;
                    }
                }
            }
            catch { }

            // 4. Fallback: Clean File System Scan (filtering out build artifacts & backup files)
            try
            {
                var candidate = Directory.EnumerateFiles(solutionDirectory, "*.csproj", SearchOption.AllDirectories)
                    .Where(f => !f.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar) &&
                                !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar) &&
                                !f.Contains(Path.DirectorySeparatorChar + ".vs" + Path.DirectorySeparatorChar) &&
                                !f.EndsWith(" - Copy.csproj", StringComparison.OrdinalIgnoreCase))
                    .OrderBy(f => f.IndexOf("Test", StringComparison.OrdinalIgnoreCase) >= 0 ? 1 : 0)
                    .FirstOrDefault();

                return candidate;
            }
            catch { }

            return null;
        }

        private static System.Collections.Generic.List<EnvDTE.Project> GetAllProjects(EnvDTE.Solution solution)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var list = new System.Collections.Generic.List<EnvDTE.Project>();
            if (solution?.Projects == null) return list;

            foreach (EnvDTE.Project proj in solution.Projects)
            {
                AddProjectsRecursive(proj, list);
            }
            return list;
        }

        private static void AddProjectsRecursive(EnvDTE.Project project, System.Collections.Generic.List<EnvDTE.Project> list)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (project == null) return;

            // Handle Solution Folders
            if (project.Kind == "{66A26720-8FB5-11D2-AA7E-00C04F688DDE}" || project.Kind == EnvDTE80.ProjectKinds.vsProjectKindSolutionFolder)
            {
                if (project.ProjectItems != null)
                {
                    foreach (EnvDTE.ProjectItem item in project.ProjectItems)
                    {
                        if (item.SubProject != null)
                        {
                            AddProjectsRecursive(item.SubProject, list);
                        }
                    }
                }
            }
            else
            {
                list.Add(project);
            }
        }

        private static void ShowError(string message)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            VsShellUtilities.ShowMessageBox(
                ServiceProvider.GlobalProvider,
                message,
                "C# Inline LaunchPad",
                OLEMSGICON.OLEMSGICON_CRITICAL,
                OLEMSGBUTTON.OLEMSGBUTTON_OK,
                OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
        }
    }
}