# C# Inline LaunchPad

A lightweight Visual Studio extension that brings JetBrains Rider-style inline execution to C# console projects. Run your program with one click and see its output in a terminal pane inside the IDE, instead of in a separate console window.

<img src="https://raw.githubusercontent.com/jafar-alshishani/CSharpInlineLaunchPad/master/Resources/square-terminal.png" width="64" alt="C# Inline LaunchPad Icon" />

## Features

* **Toolbar Execution & Auto-Save Toggle:** Run the active console project from the main toolbar. A dedicated option lets you turn auto-saving before each run on or off.
* **Integrated Terminal Pane:** Standard input and output appear in a native terminal tab in the same window, with no `cmd.exe` pop-ups.
* **`Alt + F5` Hotkey:** Run the project without reaching for the mouse.
* **Interactive Console Input:** Programs that call `Console.ReadLine()` work in the terminal pane, and input typed in the pane reaches your program.
* **Runs from the project folder:** Relative paths such as `File.ReadAllText("data.txt")` resolve next to your project files.
* **UTF-8 output:** Symbols and non-English text display correctly.
* **Exit code message:** Each run ends with `Process finished with exit code N`, so you know when the program is done and how it ended.
* **Tab reuse:** Re-running reuses the same terminal tab and stops the previous run of the same program, which avoids file-lock errors.
* **.NET and .NET Framework:** Supports SDK-style projects (.NET Core / .NET 5+) and legacy .NET Framework console projects.

| Toolbar Launch Button | Integrated Terminal Output |
| :---: | :---: |
| ![Dedicated Run Button](https://raw.githubusercontent.com/jafar-alshishani/CSharpInlineLaunchPad/master/assets/a.png) | ![C# LaunchPad Terminal](https://raw.githubusercontent.com/jafar-alshishani/CSharpInlineLaunchPad/master/assets/2_3.png) |
| *Toolbar integration* | *Interactive terminal execution* |

## Installation

1. Download the `.vsix` installer from the [Releases](https://github.com/jafar-alshishani/CSharpInlineLaunchPad/releases) tab or the [Visual Studio Marketplace](https://marketplace.visualstudio.com/items?itemName=jafar-alshishani.csharp-inline-launchpad).
2. Double-click `CSharpInlineLaunchPad.vsix` to install.
3. Restart Visual Studio.

## Requirements

* Visual Studio 2026 (tested), or Visual Studio 2022 with the integrated Terminal window (installable; not yet tested)
* A .NET SDK (.NET 6+), or a .NET Framework 4.x console project

## Tested on

* Visual Studio 2026 with SDK-style (.NET) console projects
* .NET Framework 4.6.2 console projects

## Known limitations

* The program runs without the debugger attached, like Ctrl+F5. Use the normal Start Debugging command for breakpoints.
* Visual Studio 2022 is allowed by the installer but has not been tested at runtime.
* Legacy projects with a non-default output folder (for example `bin\x64\Debug`) are not yet supported.
* Multi-target projects, VB.NET, F# and C++ projects are not yet supported.

## Feedback

Found a problem or have an idea? Please [open an issue](https://github.com/jafar-alshishani/CSharpInlineLaunchPad/issues).

## License

Distributed under the [MIT License](LICENSE.txt).