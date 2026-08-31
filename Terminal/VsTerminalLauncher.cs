using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace CSharpInlineLaunchPad.Terminal;

internal static class VsTerminalLauncher
{
    private const string TerminalAssembly = "Microsoft.VisualStudio.Terminal.dll";
    private const string TargetTabName = "C# LaunchPad";
    private static object? activeTerminalInstance = null;

    public static async Task<bool> LaunchAsync(
        string workingDirectory,
        string projectPath,
        CancellationToken cancellationToken)
    {
        try
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            // 0. AUTO-SAVE OPEN DOCUMENTS IF OPTION IS ENABLED
            await SaveModifiedDocumentsAsync();

            string exeName = Path.GetFileNameWithoutExtension(projectPath) + ".exe";

            // 1. DUAL-LAUNCH LOGIC: Detect SDK-style (.NET Core/5+) vs Legacy (.NET Framework)
            string projContent = File.Exists(projectPath) ? File.ReadAllText(projectPath) : string.Empty;
            bool isSdkStyle = projContent.Contains("Sdk=\"Microsoft.NET.Sdk\"") || projContent.Contains("<Project Sdk=");

            string msbuildExe = GetMsBuildPath();

            string runCommand = isSdkStyle
                // Modern SDK-style project: use dotnet run
                ? $"taskkill /IM \"{exeName}\" /F >nul 2>&1 & cls & dotnet run --nologo -v q -p:WarningLevel=0 --project \"{projectPath}\"\r\n"
                // Legacy .NET Framework project: build with resolved MSBuild path, /nologo flag, and launch executable directly
                : $"taskkill /IM \"{exeName}\" /F >nul 2>&1 & cls & {msbuildExe} \"{projectPath}\" /t:Build /p:Configuration=Debug /v:q /nologo & .\\bin\\Debug\\{exeName}\r\n";

            // ---------------------------------------------------------
            // 2. REUSE EXISTING TAB (Preserves User's Custom Docking)
            // ---------------------------------------------------------
            IVsWindowFrame? existingFrame = GetPrimaryFrameAndPurgeGhosts(TargetTabName);

            if (existingFrame != null)
            {
                bool inputSent = false;

                if (activeTerminalInstance != null)
                {
                    inputSent = TrySendInput(activeTerminalInstance, runCommand);
                }

                if (!inputSent)
                {
                    existingFrame.GetProperty((int)__VSFPROPID.VSFPROPID_DocView, out object docView);
                    if (docView != null)
                    {
                        inputSent = TrySendInput(docView, runCommand);
                        if (inputSent)
                        {
                            activeTerminalInstance = docView;
                        }
                    }
                }

                if (inputSent)
                {
                    existingFrame.Show(); // Brings tab into view and activates focus
                    return true;
                }

                existingFrame.CloseFrame((uint)__FRAMECLOSE.FRAMECLOSE_NoSave);
                activeTerminalInstance = null;
            }

            // ---------------------------------------------------------
            // 3. INITIAL CREATION (Docks to Bottom Panel ONCE)
            // ---------------------------------------------------------
            var markerType = FindType("Microsoft.VisualStudio.Terminal.SVsTerminalService");
            var terminalServiceInterface = FindType("Microsoft.VisualStudio.Terminal.IVsTerminalService");
            var optionsType = FindType("Microsoft.VisualStudio.Terminal.TerminalWindowOptions");
            var profileType = FindType("Microsoft.VisualStudio.Terminal.ProfileConfig");

            if (markerType == null || terminalServiceInterface == null || optionsType == null || profileType == null)
            {
                await ShowErrorAsync("C# Inline LaunchPad could not find the Visual Studio Terminal API.");
                return false;
            }

            var service = await AsyncServiceProvider.GlobalProvider.GetServiceAsync(markerType);
            if (service == null) return false;

            var createServiceMethod = terminalServiceInterface.GetMethods()
                .FirstOrDefault(m => m.Name == "CreateTerminalService" && m.GetParameters().Length == 0);

            if (createServiceMethod == null) return false;

            var terminalService = createServiceMethod.Invoke(service, null);
            if (terminalService == null) return false;

            var profileConstructor = profileType.GetConstructors()
                .FirstOrDefault(c => c.GetParameters().Length == 4);

            if (profileConstructor == null) return false;

            string initialArguments = $"/K {runCommand}";

            object profile = profileConstructor.Invoke(new object[]
            {
                TargetTabName,
                "cmd.exe",
                initialArguments,
                false
            });

            var addProfileMethod = terminalService.GetType().GetMethods()
                .FirstOrDefault(m => m.Name == "AddCachedProfile" && m.GetParameters().Length == 1);

            if (addProfileMethod != null)
            {
                addProfileMethod.Invoke(terminalService, new[] { profile });
            }

            object options = Activator.CreateInstance(optionsType)!;
            optionsType.GetProperty("Name")?.SetValue(options, TargetTabName);
            optionsType.GetProperty("WorkingDirectory")?.SetValue(options, workingDirectory);
            optionsType.GetProperty("Profile")?.SetValue(options, profile);
            optionsType.GetProperty("Focus")?.SetValue(options, true);
            optionsType.GetProperty("AllowUserInput")?.SetValue(options, true);

            var createWindowMethod = terminalService.GetType().GetMethods()
                .FirstOrDefault(m => m.Name == "CreateTerminalWindowAsync" && m.GetParameters().Length == 2);

            if (createWindowMethod == null) return false;

            var methodParameters = createWindowMethod.GetParameters();
            object[] invocationArguments = methodParameters[0].ParameterType == typeof(CancellationToken)
                ? new object[] { cancellationToken, options }
                : new object[] { options, cancellationToken };

            object? result = createWindowMethod.Invoke(terminalService, invocationArguments);
            if (result is Task task)
            {
                await task;
                var resultProp = task.GetType().GetProperty("Result");
                if (resultProp != null)
                {
                    activeTerminalInstance = resultProp.GetValue(task);
                }
            }

            IVsWindowFrame? newFrame = GetPrimaryFrameAndPurgeGhosts(TargetTabName);
            if (newFrame != null)
            {
                DockToBottomPanel(newFrame);
            }

            return true;
        }
        catch (Exception ex)
        {
            await ShowErrorAsync("C# Inline LaunchPad could not start the integrated Terminal.\n\n" + ex.Message);
            return false;
        }
    }

    private static string GetMsBuildPath()
    {
        try
        {
            string? ideDir = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule?.FileName);
            if (ideDir != null)
            {
                // Navigate from <VS_ROOT>\Common7\IDE to <VS_ROOT>\MSBuild\Current\Bin\MSBuild.exe
                string candidatePath = Path.GetFullPath(Path.Combine(ideDir, @"..\..\MSBuild\Current\Bin\MSBuild.exe"));
                if (File.Exists(candidatePath))
                {
                    return $"\"{candidatePath}\"";
                }
            }
        }
        catch { }

        return "msbuild"; // Fallback to system PATH if directory navigation fails
    }

    private static async Task SaveModifiedDocumentsAsync()
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

        try
        {
            var package = CSharpInlineLaunchPad.Instance;
            if (package != null && package.GetDialogPage(typeof(OptionPageGrid)) is OptionPageGrid options)
            {
                if (options.SaveAllBeforeRun)
                {
                    var dte = (EnvDTE.DTE?)await package.GetServiceAsync(typeof(EnvDTE.DTE));
                    dte?.ExecuteCommand("File.SaveAll");
                }
            }
        }
        catch
        {
            // Fail silently if DTE or options are temporarily unavailable
        }
    }

    private static void DockToBottomPanel(IVsWindowFrame frame)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        try
        {
            Guid emptyGuid = Guid.Empty;
            frame.SetProperty((int)__VSFPROPID.VSFPROPID_FrameMode, (VSFRAMEMODE)(-1));
            frame.SetFramePos((VSSETFRAMEPOS)(-1), ref emptyGuid, 0, 0, 0, 0);
            frame.Show();
        }
        catch { }
    }

    private static bool TrySendInput(object? target, string text)
    {
        if (target == null) return false;

        try
        {
            var method = target.GetType().GetMethod("SendInput", new[] { typeof(string) })
                      ?? target.GetType().GetMethods().FirstOrDefault(m => m.Name == "SendInput" && m.GetParameters().Length == 1);

            if (method != null)
            {
                method.Invoke(target, new object[] { text });
                return true;
            }

            var properties = target.GetType().GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (var prop in properties)
            {
                if (prop.CanRead && prop.PropertyType.IsClass && prop.PropertyType != typeof(string))
                {
                    var child = prop.GetValue(target);
                    if (child != null && child != target)
                    {
                        var childMethod = child.GetType().GetMethod("SendInput", new[] { typeof(string) })
                                       ?? child.GetType().GetMethods().FirstOrDefault(m => m.Name == "SendInput" && m.GetParameters().Length == 1);
                        if (childMethod != null)
                        {
                            childMethod.Invoke(child, new object[] { text });
                            return true;
                        }
                    }
                }
            }
        }
        catch { }

        return false;
    }

    private static IVsWindowFrame? GetPrimaryFrameAndPurgeGhosts(string tabTitle)
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        var uiShell = ServiceProvider.GlobalProvider.GetService(typeof(SVsUIShell)) as IVsUIShell;
        if (uiShell == null) return null;

        if (uiShell.GetToolWindowEnum(out IEnumWindowFrames enumFrames) != 0 || enumFrames == null)
            return null;

        List<IVsWindowFrame> matchingFrames = new();
        IVsWindowFrame[] frameBuffer = new IVsWindowFrame[1];

        while (enumFrames.Next(1, frameBuffer, out uint fetched) == 0 && fetched == 1)
        {
            frameBuffer[0].GetProperty((int)__VSFPROPID.VSFPROPID_Caption, out object captionObj);
            if (captionObj is string caption && caption.Contains(tabTitle))
            {
                matchingFrames.Add(frameBuffer[0]);
            }
        }

        if (matchingFrames.Count == 0) return null;

        for (int i = 1; i < matchingFrames.Count; i++)
        {
            matchingFrames[i].CloseFrame((uint)__FRAMECLOSE.FRAMECLOSE_NoSave);
        }

        return matchingFrames[0];
    }

    private static Type? FindType(string fullName)
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                var type = assembly.GetType(fullName, false);
                if (type != null) return type;
            }
            catch { }
        }

        try
        {
            string? visualStudioPath = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule?.FileName);
            if (visualStudioPath == null) return null;

            string terminalPath = Path.Combine(visualStudioPath, "CommonExtensions", "Microsoft", "Terminal", TerminalAssembly);
            if (!File.Exists(terminalPath)) return null;

            var assembly = Assembly.LoadFrom(terminalPath);
            return assembly.GetType(fullName, false);
        }
        catch
        {
            return null;
        }
    }

    public static async Task ShowErrorAsync(string message)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        VsShellUtilities.ShowMessageBox(
            ServiceProvider.GlobalProvider,
            message,
            "C# Inline LaunchPad",
            OLEMSGICON.OLEMSGICON_CRITICAL,
            OLEMSGBUTTON.OLEMSGBUTTON_OK,
            OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
    }
}