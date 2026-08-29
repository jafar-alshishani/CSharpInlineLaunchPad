using Microsoft.VisualStudio.Shell;
using System;
using System.ComponentModel.Design;
using Task = System.Threading.Tasks.Task;

namespace CSharpInlineLaunchPad.Commands;

internal sealed class ToggleAutoSaveCommand
{
    // Ensure this matches the IDSymbol value (0x0200) in VSCommandTable.vsct
    public const int CommandId = 0x0200;

    // Ensure this matches your PackageCmdSet GUID in VSCommandTable.vsct
    public static readonly Guid CommandSet = new Guid("7F4A7D6D-6B37-4D5D-9B48-1A7A3F9B4A11");

    private readonly AsyncPackage package;

    private ToggleAutoSaveCommand(AsyncPackage package, OleMenuCommandService commandService)
    {
        this.package = package ?? throw new ArgumentNullException(nameof(package));

        var menuCommandID = new CommandID(CommandSet, CommandId);
        var menuItem = new OleMenuCommand(this.Execute, menuCommandID);

        // Dynamically updates the checkmark right before the menu opens
        menuItem.BeforeQueryStatus += OnBeforeQueryStatus;

        commandService.AddCommand(menuItem);
    }

    public static ToggleAutoSaveCommand? Instance { get; private set; }

    public static async Task InitializeAsync(AsyncPackage package)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

        var commandService = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
        if (commandService != null)
        {
            Instance = new ToggleAutoSaveCommand(package, commandService);
        }
    }

    private void OnBeforeQueryStatus(object? sender, EventArgs e)
    {
        if (sender is OleMenuCommand menuItem)
        {
            var options = (OptionPageGrid)package.GetDialogPage(typeof(OptionPageGrid));
            menuItem.Checked = options.SaveAllBeforeRun;
        }
    }

    private void Execute(object? sender, EventArgs e)
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        var options = (OptionPageGrid)package.GetDialogPage(typeof(OptionPageGrid));
        options.SaveAllBeforeRun = !options.SaveAllBeforeRun;

        // Persists setting to VS storage so it syncs with Tools -> Options
        options.SaveSettingsToStorage();

        if (sender is OleMenuCommand menuItem)
        {
            menuItem.Checked = options.SaveAllBeforeRun;
        }
    }
}