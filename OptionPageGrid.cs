

using Microsoft.VisualStudio.Shell;
using System.ComponentModel;

namespace CSharpInlineLaunchPad;

public class OptionPageGrid : DialogPage
{
    [Category("Execution Settings")]
    [DisplayName("Auto-Save Before Run")]
    [Description("Automatically saves all open modified files in the solution before running the code.")]
    public bool SaveAllBeforeRun { get; set; } = true;
}