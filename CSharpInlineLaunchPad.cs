using CSharpInlineLaunchPad.Commands;
using Microsoft.VisualStudio.Shell;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace CSharpInlineLaunchPad
{
    /// <summary>
    /// This is the class that implements the package exposed by this assembly.
    /// </summary>
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [ProvideMenuResource("Menus.ctmenu", 2)]
    [ProvideOptionPage(typeof(OptionPageGrid), "C# Inline LaunchPad", "General", 0, 0, true)]
    [Guid(CSharpInlineLaunchPad.PackageGuidString)]
    public sealed class CSharpInlineLaunchPad : AsyncPackage
    {
        /// <summary>
        /// CSharpClassroomPackage GUID string.
        /// </summary>
        public const string PackageGuidString = "e61f225c-7887-4e0e-bdc8-fc8326258445";

        /// <summary>
        /// Static reference to the active package instance for reading dialog page options across commands.
        /// </summary>
        public static CSharpInlineLaunchPad? Instance { get; private set; }

        #region Package Members

        /// <summary>
        /// Initialization of the package; this method is called right after the package is sited, so this is the place
        /// where you can put all the initialization code that rely on services provided by VisualStudio.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token to monitor for initialization cancellation, which can occur when VS is shutting down.</param>
        /// <param name="progress">A provider for progress updates.</param>
        /// <returns>A task representing the async work of package initialization, or an already completed task if there is none. Do not return null from this method.</returns>
        protected override async Task InitializeAsync(
            CancellationToken cancellationToken,
            IProgress<ServiceProgressData> progress)
        {
            Instance = this;

            await this.JoinableTaskFactory
                .SwitchToMainThreadAsync(cancellationToken);

            await RunCSharpCommand.InitializeAsync(this);
            await ToggleAutoSaveCommand.InitializeAsync(this);
        }

        #endregion
    }
}