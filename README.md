# MonoToolkit 👋

Computer science enthusiast tooling project focused on **Unity / Mono** workflows and **reverse engineering**.
A clean, practical CLI for finding Mono processes, injecting a managed core, and running C# scripts inside an attached process.

<img src="./images/matrix.gif" alt="Matrix animation" align="right" width="220" />

[![.NET Framework](https://img.shields.io/badge/.NET%20Framework-4.7.2-512BD4?style=flat-square&logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![Platform](https://img.shields.io/badge/Platform-Windows-0078D4?style=flat-square&logo=windows&logoColor=white)](#)
[![Arch](https://img.shields.io/badge/Arch-x64-2ea44f?style=flat-square)](#)

---

## About This Project
- 🎯 Goal: a straightforward Mono injector + scripting runner that’s fast to use and easy to build.
- 🧩 Two parts:
  - `MonoToolkit` (CLI) — process discovery, attach/detach, inject, run.
  - `MonoToolkitCore` (injected library) — heartbeat + shutdown signal handling.
- 🧠 Built for practical reverse engineering workflows and modding experiments.

## Features
<hr />

- Detect running processes with Mono runtime modules.
- Attach + inject `MonoToolkitCore.dll` into a target process.
- Heartbeat/status tracking via memory-mapped file.
- Clean detach using a temp-file shutdown flag.
- `run` command: compile and inject a C# script into the attached process.

## Commands
<hr />

- `help` — show commands (or `help <command>`)
- `process` / `ps` — list Mono runtime processes
- `attach <PID|process_name>` — attach + inject core
- `status` — show attached process status
- `detach <PID>` — signal shutdown + cleanup
- `inject <pid> <dllname>` — inject an additional DLL from the exe directory
- `run <pid> <script.cs>` — compile + run a script from the exe directory
- `clear` / `cls` — clear screen
- `exit` — quit

## Build & Output
<hr />

Requirements:
- Windows
- .NET SDK that can build `net472` projects

Build (Release):
- `dotnet build .\MonoToolkit\MonoToolkit.csproj -c Release`
- `dotnet build .\MonoToolkitCore\MonoToolkitCore.csproj -c Release`

Outputs land in:
- `Build/MonoToolkit.exe`
- `Build/MonoToolkit.exe.config`
- `Build/MonoToolkitCore.dll`

## Runtime Artifacts (Temp)
<hr />

MonoToolkit uses the per-user temp directory (via `Path.GetTempPath()`), not any hardcoded personal path.

Typical artifacts:
- `MonoToolkit_<PID>.log`
- `MonoToolkit_Error_<PID>.log`
- `MonoToolkit_Shutdown_<PID>.flag`

Heartbeat object (not a file):
- Memory-mapped file name: `MonoToolkit_Heartbeat_<PID>`

## Safety / Legal
<hr />

Use this only on software you own or have explicit permission to test.
Injection tooling can break processes, trip anti-cheat, or violate terms of service.

## Contact
- YouTube: https://www.youtube.com/@ModSpidr
- Portfolio: https://gregorybridges.dev
- Email: contact@gregorybridges.dev
