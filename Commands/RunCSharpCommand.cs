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
            // RunAsync without blocking UI thread, handling exceptions cleanly
            ThreadHelper.JoinableTaskFactory.RunAsync(async delegate
            {
                await ExecuteAsync();
            }).FileAndForget("CSharpInlineLaunchPad/RunCommand");
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

                string? projectPath =
                    Directory
                        .GetFiles(
                            solutionDirectory,
                            "*.csproj",
                            SearchOption.AllDirectories)
                        .FirstOrDefault();

                if (projectPath == null)
                {
                    ShowError(
                        "C# Classroom could not find a .csproj file.");
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
                    "C# Classroom could not run the project.\n\n" +
                    ex.Message);
            }
        }

        private static void ShowError(string message)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            VsShellUtilities.ShowMessageBox(
                ServiceProvider.GlobalProvider,
                message,
                "C# Classroom",
                OLEMSGICON.OLEMSGICON_CRITICAL,
                OLEMSGBUTTON.OLEMSGBUTTON_OK,
                OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
        }
    }
}