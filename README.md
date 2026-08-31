# C# Inline LaunchPad

A lightweight Visual Studio 2026 extension designed to provide single-click inline execution of C# console applications and quick terminal access right from your main toolbar.

![C# Inline LaunchPad Toolbar](https://github.com/jafar-alshishani/CSharpInlineLaunchPad/blob/master/Resources/square-terminal.png)

## Features

* **Instant Toolbar & Shortcut Access:** Adds a dedicated purple `>_` terminal launch button directly alongside the standard Start/Debug controls (`priority="0x0950"`), and defaults to **`Alt + F5`** out of the box.
* **Customizable Keybindings:** Fully exposed under `Tools -> Options -> Environment -> Keyboard` via the command `CSharpInlineLaunchPad.Run`.
* **Smart Terminal Reuse:** Automatically reuses existing terminal instances instead of cluttering your workspace with duplicate tabs.
* **Process Lifecycle Control:** Prevents file-lock errors (`cli process in use`) by cleanly terminating active background runs when re-launching.
* **Automated Document Saving:** Automatically saves modified project files before executing `dotnet run` (toggleable via Tools menu or Options).
* **Zero Telemetry & Noise Suppression:** Keeps output clean by filtering out redundant CLI banner noise.

## Installation

1. Download the `.vsix` installer from the [Releases](https://github.com/jafar-alshishani/CSharpInlineLaunchPad/releases) tab or Visual Studio Marketplace.
2. Double-click `CSharpInlineLaunchPad.vsix` to install.
3. Restart Visual Studio 2022.

## Requirement

* Visual Studio 2026 (v17.0+)
* `.NET Core` / `.NET 6+` SDK

## License

Distributed under the [MIT License](LICENSE.txt).
