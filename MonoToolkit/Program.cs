using System;
using System.Diagnostics;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.IO.MemoryMappedFiles;
using System.Threading;
using System.CodeDom.Compiler;
using Microsoft.CSharp;
using System.Reflection;

namespace MonoToolkit
{
    class AttachedProcess
    {
        public int ProcessId { get; set; }
        public string ProcessName { get; set; }
        public DateTime AttachedAt { get; set; }
        public bool IsActive { get; set; }
    }

    class Program
    {
        private static void SetDefaultColor()
        {
            Console.ForegroundColor = ConsoleColor.Green;
        }

        private static void WriteErrorLine(string message)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(message);
            SetDefaultColor();
        }

        private static void WriteWarningLine(string message)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(message);
            SetDefaultColor();
        }

        private static void WriteInfoLine(string message)
        {
            SetDefaultColor();
            Console.WriteLine(message);
        }

        
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, int dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("psapi.dll", SetLastError = true)]
        static extern bool EnumProcessModulesEx(IntPtr hProcess, [Out] IntPtr[] lphModule, uint cb, out uint lpcbNeeded, uint dwFilterFlag);

        [DllImport("psapi.dll", CharSet = CharSet.Auto)]
        static extern uint GetModuleFileNameEx(IntPtr hProcess, IntPtr hModule, StringBuilder lpBaseName, uint nSize);

        
        const uint LIST_MODULES_ALL = 0x03;

        private const uint PROCESS_CREATE_THREAD = 0x0002;
        private const uint PROCESS_QUERY_INFORMATION = 0x0400;
        private const uint PROCESS_VM_OPERATION = 0x0008;
        private const uint PROCESS_VM_WRITE = 0x0020;
        private const uint PROCESS_VM_READ = 0x0010;
        private const uint MEM_COMMIT = 0x1000;
        private const uint MEM_RESERVE = 0x2000;
        private const uint MEM_RELEASE = 0x8000;
        private const uint PAGE_READWRITE = 0x04;
        
        private static string promptPrefix;
        private const int TargetWidth = 120;
        private const int TargetHeight = 35;
        private static Dictionary<int, AttachedProcess> attachedProcesses = new Dictionary<int, AttachedProcess>();
        private static string injectorDllPath;
        private static string modsDir;

        static void Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.InputEncoding = System.Text.Encoding.UTF8;
            
            string exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            string exeDir = Path.GetDirectoryName(exePath) ?? Environment.CurrentDirectory;
            injectorDllPath = Path.Combine(exeDir, "MonoToolkitCore.dll");
            modsDir = Path.Combine(exeDir, "Mods");

            try
            {
                Directory.CreateDirectory(modsDir);
            }
            catch
            {
            }
            
            try
            {
                if (!Console.IsOutputRedirected)
                {
                    Console.SetWindowSize(TargetWidth, TargetHeight);
                    Console.SetBufferSize(TargetWidth, TargetHeight);
                }
                Console.CursorVisible = false;
            }
            catch
            {
            }
            SetDefaultColor();
            
            Console.Clear();
            Console.SetCursorPosition(0, 0);
            
            PrintBanner();
            
            promptPrefix = BuildPromptPrefix();
            
            while (true)
            {
                if (Console.WindowWidth != TargetWidth || Console.WindowHeight != TargetHeight)
                {
                    try
                    {
                        Console.SetWindowSize(TargetWidth, TargetHeight);
                        Console.SetBufferSize(TargetWidth, TargetHeight);
                        Console.Clear();
                        SetDefaultColor();
                        PrintBanner();
                    }
                    catch
                    {
                    }
                }
                
                Console.CursorVisible = true;
                SetDefaultColor();
                Console.Write(promptPrefix);
                string input = Console.ReadLine();
                Console.CursorVisible = false;
                
                if (string.IsNullOrWhiteSpace(input))
                    continue;
                
                ProcessCommand(input.Trim());
            }
        }

        static string BuildPromptPrefix()
        {
            string machine = Environment.MachineName;
            return "guest@" + machine + ":~$ ";
        }

        static void ProcessCommand(string command)
        {
            try
            {
                string[] parts = command.Split(' ');
                string cmd = parts[0].ToLower();

                switch (cmd)
                {
                    case "help":
                        ShowHelp(parts);
                        break;
                    case "clear":
                    case "cls":
                        Console.Clear();
                        SetDefaultColor();
                        PrintBanner();
                        break;
                    case "exit":
                        WriteInfoLine("Exiting MonoToolkit...");
                        Console.WriteLine("");
                        System.Threading.Thread.Sleep(500);
                        Environment.Exit(0);
                        break;
                    case "ver":
                        ShowVersion();
                        break;
                    case "process":
                    case "ps":
                        ListProcesses();
                        break;
                    case "attach":
                        HandleAttach(parts);
                        break;
                    case "detach":
                        HandleDetach(parts);
                        break;
                    case "status":
                        ShowStatus();
                        break;
                    case "inject":
                        HandleInject(parts);
                        break;
                    case "run":
                        HandleRun(parts);
                        break;
                    default:
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine(cmd + ": command not found");
                        SetDefaultColor();
                        Console.WriteLine("");
                        break;
                }
            }
            finally
            {
                SetDefaultColor();
            }
        }

        static void ShowHelp(string[] parts)
        {
            if (parts.Length > 1)
            {
                
                var displayedCommands = new System.Collections.Generic.HashSet<string>();
                
                for (int i = 1; i < parts.Length; i++)
                {
                    string helpCmd = parts[i].ToLower();
                    
                    if (displayedCommands.Contains(helpCmd))
                        continue;
                    
                    displayedCommands.Add(helpCmd);
                    
                    switch (helpCmd)
                    {
                        case "attach":
                            Console.WriteLine("ATTACH     Attach to a Mono process and inject MonoToolkitCore.dll");
                            break;
                        
                        case "cls":
                            Console.WriteLine("CLS        Clears the screen.");
                            break;
                        
                        case "detach":
                            Console.WriteLine("DETACH     Detach from a process and eject the DLL");
                            break;
                        
                        case "exit":
                            Console.WriteLine("EXIT       Quits the MonoToolkit program.");
                            break;
                        
                        case "help":
                            Console.WriteLine("HELP       Provides Help information for MonoToolkit commands.");
                            break;
                        
                        case "process":
                        case "ps":
                            Console.WriteLine("PROCESS    Lists all running Mono runtime processes.");
                            break;
                        
                        case "run":
                            Console.WriteLine("RUN        Execute C# code directly in attached process");
                            break;
                        
                        case "status":
                            Console.WriteLine("STATUS     Shows all attached processes and their status");
                            break;
                        
                        case "inject":
                            Console.WriteLine("INJECT     Inject any DLL into a process by PID and DLL name");
                            break;
                        
                        case "ver":
                            Console.WriteLine("VER        Displays the MonoToolkit description and credits.");
                            break;
                        
                        default:
                            break;
                    }
                }
                
                Console.WriteLine("");
                return;
            }
            
            Console.WriteLine("For more information on a specific command, type HELP command-name");
            Console.WriteLine("");
            Console.WriteLine("ATTACH     Attach to a Mono process and inject MonoToolkitCore.dll");
            Console.WriteLine("CLS        Clears the screen.");
            Console.WriteLine("DETACH     Detach from a process and eject the DLL");
            Console.WriteLine("EXIT       Quits the MonoToolkit program.");
            Console.WriteLine("HELP       Provides Help information for MonoToolkit commands.");
            Console.WriteLine("INJECT     Inject any DLL into a process by PID and DLL name");
            Console.WriteLine("PROCESS    Lists all running Mono runtime processes.");
            Console.WriteLine("RUN        Execute C# code directly in attached process");
            Console.WriteLine("STATUS     Shows all attached processes and their status");
            Console.WriteLine("VER        Displays the MonoToolkit description and credits.");
            Console.WriteLine("");
        }

        static void ShowVersion()
        {
            Console.WriteLine("MonoToolkit [Version 1.0.0]");
            Console.WriteLine("");
            Console.WriteLine("Description:");
            Console.WriteLine("  A process finder and injector for Mono runtime Unity games.");
            Console.WriteLine("  Allows you to manipulate inject mid-runtime DLLs, execute C# code, and more.");
            Console.WriteLine("  and more.");
            Console.WriteLine("");
            Console.WriteLine("Credits:");
            Console.WriteLine("  Created by: ModSpidr");
            Console.WriteLine("");
        }

        static void ListProcesses()
        {
            Console.WriteLine("Scanning for Mono runtime processes...");
            Console.WriteLine("");

            Process[] allProcesses = Process.GetProcesses();
            var monoProcesses = new System.Collections.Generic.List<Process>();

            foreach (Process process in allProcesses)
            {
                try
                {
                    foreach (ProcessModule module in process.Modules)
                    {
                        string moduleName = module.ModuleName.ToLower();
                        if (moduleName.Contains("mono") && moduleName.EndsWith(".dll"))
                        {
                            monoProcesses.Add(process);
                            break;
                        }
                    }
                }
                catch
                {
                    continue;
                }
            }

            if (monoProcesses.Count == 0)
            {
                Console.WriteLine("No Mono runtime processes found.");
                Console.WriteLine("");
                return;
            }

            Console.WriteLine("PID      Process Name                    Window Title");
            Console.WriteLine(new string('\u2500', TargetWidth));

            foreach (Process proc in monoProcesses)
            {
                try
                {
                    string pid = proc.Id.ToString().PadRight(8);
                    string name = proc.ProcessName.Length > 30 
                        ? proc.ProcessName.Substring(0, 27) + "..." 
                        : proc.ProcessName.PadRight(30);
                    string title = "";
                    
                    if (!string.IsNullOrEmpty(proc.MainWindowTitle))
                    {
                        title = proc.MainWindowTitle.Length > 60 
                            ? proc.MainWindowTitle.Substring(0, 57) + "..." 
                            : proc.MainWindowTitle;
                    }

                    Console.WriteLine($"{pid} {name}  {title}");
                }
                catch
                {
                }
            }

            Console.WriteLine("");
            Console.WriteLine($"Found {monoProcesses.Count} Mono runtime process(es).");
            Console.WriteLine("");
        }

        static void HandleAttach(string[] parts)
        {
            if (parts.Length < 2)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("[ERROR] Usage: attach <PID|process_name>");
                Console.WriteLine("");
                return;
            }

            string target = string.Join(" ", parts, 1, parts.Length - 1);
            Process targetProcess = null;

            
            if (int.TryParse(target, out int pid))
            {
                try
                {
                    targetProcess = Process.GetProcessById(pid);
                }
                catch
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"[ERROR] No process found with PID {pid}");
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("");
                    return;
                }
            }
            else
            {
                
                Process[] processes = Process.GetProcessesByName(target);
                if (processes.Length == 0)
                {
                    
                    processes = Process.GetProcesses()
                        .Where(p => p.ProcessName.IndexOf(target, StringComparison.OrdinalIgnoreCase) >= 0)
                        .ToArray();
                }

                if (processes.Length == 0)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"[ERROR] No process found matching '{target}'");
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("");
                    return;
                }
                else if (processes.Length > 1)
                {
                    Console.WriteLine("");
                    WriteWarningLine($"[WARNING] Multiple processes found matching '{target}':");
                    foreach (var p in processes)
                    {
                        Console.WriteLine($"  PID {p.Id}: {p.ProcessName}");
                    }
                    Console.WriteLine("");
                    Console.WriteLine("Please specify the exact PID.");
                    Console.WriteLine("");
                    return;
                }

                targetProcess = processes[0];
            }

            
            if (attachedProcesses.ContainsKey(targetProcess.Id))
            {
                Console.WriteLine("");
                WriteWarningLine($"[WARNING] Already attached to {targetProcess.ProcessName} (PID: {targetProcess.Id})");
                Console.WriteLine("");
                return;
            }

            
            if (!File.Exists(injectorDllPath))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[ERROR] MonoToolkitCore.dll not found at: {injectorDllPath}");
                WriteInfoLine("[INFO] You need to build the MonoToolkitCore project first.");
                Console.WriteLine("");
                return;
            }

            Console.WriteLine($"Attaching to {targetProcess.ProcessName} (PID: {targetProcess.Id})...");
            Console.WriteLine($"Injecting: {Path.GetFileName(injectorDllPath)}");

            bool injectionSuccess = InjectDLL(targetProcess.Id, injectorDllPath);

            if (injectionSuccess)
            {
                attachedProcesses[targetProcess.Id] = new AttachedProcess
                {
                    ProcessId = targetProcess.Id,
                    ProcessName = targetProcess.ProcessName,
                    AttachedAt = DateTime.Now,
                    IsActive = true 
                };

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("[SUCCESS] DLL injected successfully!");
                Console.WriteLine($"[SUCCESS] Attached to {targetProcess.ProcessName} (PID: {targetProcess.Id})");
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("");
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("[ERROR] Failed to inject DLL");
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("");
            }
        }

        static void HandleDetach(string[] parts)
        {
            if (parts.Length < 2)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("[ERROR] Usage: detach <PID>");
                Console.WriteLine("");
                return;
            }

            if (!int.TryParse(parts[1], out int pid))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("[ERROR] Invalid PID. Must be a number.");
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("");
                return;
            }

            if (!attachedProcesses.ContainsKey(pid))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[ERROR] Not attached to process with PID {pid}");
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("");
                return;
            }

            AttachedProcess attached = attachedProcesses[pid];
            Console.WriteLine("");
            Console.WriteLine($"Detaching from {attached.ProcessName} (PID: {pid})...");

            bool ejectionSuccess = EjectDLL(pid);

            if (ejectionSuccess)
            {
                attachedProcesses.Remove(pid);
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("[SUCCESS] DLL ejected successfully!");
                Console.WriteLine($"[SUCCESS] Detached from PID {pid}");
                Console.ForegroundColor = ConsoleColor.Green;
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("[ERROR] Failed to eject DLL");
                Console.ForegroundColor = ConsoleColor.Green;
            }

            Console.WriteLine("");
        }

        static void ShowStatus()
        {
            Console.WriteLine("Attached Processes:");
            Console.WriteLine("");

            if (attachedProcesses.Count == 0)
            {
                Console.WriteLine("No processes currently attached.");
                Console.WriteLine("");
                return;
            }

            Console.WriteLine("PID      Process Name              Attached At          Status");
            Console.WriteLine(new string('\u2500', TargetWidth));

            
            List<int> processesToRemove = new List<int>();

            foreach (var kvp in attachedProcesses)
            {
                AttachedProcess attached = kvp.Value;
                string pid = attached.ProcessId.ToString().PadRight(8);
                string name = attached.ProcessName.Length > 24
                    ? attached.ProcessName.Substring(0, 21) + "..."
                    : attached.ProcessName.PadRight(24);
                string time = attached.AttachedAt.ToString("yyyy-MM-dd HH:mm:ss");
                
                
                bool processExists = false;
                try
                {
                    Process.GetProcessById(attached.ProcessId);
                    processExists = true;
                }
                catch { }

                
                bool dllActive = CheckHeartbeat(attached.ProcessId);
                attached.IsActive = dllActive;

                string status;
                if (!processExists)
                {
                    status = "Process Exited";
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    processesToRemove.Add(attached.ProcessId); 
                }
                else if (dllActive)
                {
                    status = "Active (DLL Running)";
                    Console.ForegroundColor = ConsoleColor.Green;
                }
                else
                {
                    status = "Inactive (DLL Not Responding)";
                    Console.ForegroundColor = ConsoleColor.Yellow;
                }

                Console.WriteLine($"{pid} {name}  {time}  {status}");
                Console.ForegroundColor = ConsoleColor.Green;
            }

            
            foreach (int pidToRemove in processesToRemove)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                CleanupProcessResources(pidToRemove);
                attachedProcesses.Remove(pidToRemove);
            }

            Console.WriteLine("");
        }

        static bool InjectDLL(int processId, string dllPath)
        {
            IntPtr hProcess = IntPtr.Zero;
            
            try
            {
                
                hProcess = OpenProcess(
                    PROCESS_CREATE_THREAD | PROCESS_QUERY_INFORMATION | PROCESS_VM_OPERATION | PROCESS_VM_WRITE | PROCESS_VM_READ,
                    false,
                    processId);

                if (hProcess == IntPtr.Zero)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("[ERROR] Failed to open process. Try running as Administrator.");
                    Console.ForegroundColor = ConsoleColor.Green;
                    return false;
                }

                
                IntPtr monoDllBase = GetRemoteModuleHandle(hProcess, "mono.dll");
                if (monoDllBase == IntPtr.Zero)
                {
                    monoDllBase = GetRemoteModuleHandle(hProcess, "mono-2.0-bdwgc.dll");
                }

                if (monoDllBase == IntPtr.Zero)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("[ERROR] Mono runtime not found. This is not a Mono game.");
                    Console.ForegroundColor = ConsoleColor.Green;
                    return false;
                }

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"[INFO] Mono runtime detected at 0x{monoDllBase.ToInt64():X}");
                Console.ForegroundColor = ConsoleColor.Green;

                
                if (!File.Exists(dllPath))
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"[ERROR] DLL not found: {dllPath}");
                    Console.ForegroundColor = ConsoleColor.Green;
                    return false;
                }

                byte[] assemblyData = File.ReadAllBytes(dllPath);

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("[INFO] Injecting assembly using Mono API...");
                Console.ForegroundColor = ConsoleColor.Green;

                
                using (MonoInjector injector = new MonoInjector(hProcess, monoDllBase))
                {
                    IntPtr assembly = injector.Inject(
                        assemblyData,
                        "MonoToolkitCore",  
                        "Loader",           
                        "Load");            

                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"[INFO] Assembly loaded at 0x{assembly.ToInt64():X16}");
                    Console.ForegroundColor = ConsoleColor.Green;
                }

                
                Thread.Sleep(1000);

                
                bool initialized = false;
                for (int i = 0; i < 5; i++)
                {
                    if (CheckHeartbeat(processId))
                    {
                        initialized = true;
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine("[INFO] DLL heartbeat detected - initialization successful!");
                        Console.ForegroundColor = ConsoleColor.Green;
                        break;
                    }
                    Thread.Sleep(500);
                }

                if (!initialized)
                {
                    WriteWarningLine("[WARNING] No heartbeat detected yet. The DLL may still be initializing.");
                    WriteInfoLine("[INFO] Check the log file at: " + Path.Combine(Path.GetTempPath(), $"MonoToolkit_{processId}.log"));
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[ERROR] Exception during injection: {ex.Message}");
                Console.ForegroundColor = ConsoleColor.Green;
                return false;
            }
            finally
            {
                if (hProcess != IntPtr.Zero)
                {
                    CloseHandle(hProcess);
                }
            }
        }

        static IntPtr GetRemoteModuleHandle(IntPtr hProcess, string moduleName)
        {
            IntPtr[] hMods = new IntPtr[1024];
            uint cbNeeded;

            if (EnumProcessModulesEx(hProcess, hMods, (uint)(IntPtr.Size * hMods.Length), out cbNeeded, LIST_MODULES_ALL))
            {
                int moduleCount = (int)(cbNeeded / IntPtr.Size);

                for (int i = 0; i < moduleCount; i++)
                {
                    StringBuilder modName = new StringBuilder(260);
                    if (GetModuleFileNameEx(hProcess, hMods[i], modName, 260) > 0)
                    {
                        string fullPath = modName.ToString();
                        string fileName = Path.GetFileName(fullPath).ToLower();

                        if (fileName == moduleName.ToLower())
                        {
                            return hMods[i];
                        }
                    }
                }
            }

            return IntPtr.Zero;
        }

        static bool EjectDLL(int processId)
        {
            
            
            try
            {
                string shutdownFlag = Path.Combine(Path.GetTempPath(), $"MonoToolkit_Shutdown_{processId}.flag");
                File.WriteAllText(shutdownFlag, DateTime.Now.ToString());

                WriteInfoLine("[INFO] Shutdown signal sent to DLL. Waiting for cleanup...");
                
                
                for (int i = 0; i < 50; i++)
                {
                    if (!CheckHeartbeat(processId))
                    {
                        WriteInfoLine("[INFO] DLL has stopped responding - cleanup successful.");
                        break;
                    }
                    Thread.Sleep(100);
                }
                
                
                CleanupProcessResources(processId);
                
                return true;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[ERROR] Ejection failed: {ex.Message}");
                Console.ForegroundColor = ConsoleColor.Green;
                return false;
            }
        }

        static void CleanupProcessResources(int processId)
        {
            try
            {
                
                string shutdownFlag = Path.Combine(Path.GetTempPath(), $"MonoToolkit_Shutdown_{processId}.flag");
                if (File.Exists(shutdownFlag))
                {
                    File.Delete(shutdownFlag);
                    Console.WriteLine("[INFO] Removed shutdown flag file");
                }

                
                try
                {
                    string mmfName = $"MonoToolkit_Heartbeat_{processId}";
                    using (var mmf = MemoryMappedFile.OpenExisting(mmfName))
                    {
                        
                        mmf.Dispose();
                    }
                }
                catch
                {
                    
                }

            }
            catch (Exception ex)
            {
                WriteWarningLine($"[WARNING] Cleanup warning: {ex.Message}");
            }
        }

        static bool CheckHeartbeat(int processId)
        {
            try
            {
                string mmfName = $"MonoToolkit_Heartbeat_{processId}";
                
                using (var mmf = MemoryMappedFile.OpenExisting(mmfName))
                using (var accessor = mmf.CreateViewAccessor())
                {
                    long timestamp = accessor.ReadInt64(0);
                    DateTime lastHeartbeat = new DateTime(timestamp);
                    
                    
                    return (DateTime.Now - lastHeartbeat).TotalSeconds < 3;
                }
            }
            catch
            {
                
                return false;
            }
        }

        static void HandleInject(string[] parts)
        {
            if (parts.Length != 3)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("[ERROR] Usage: inject <pid> <dllname>");
                Console.WriteLine("");
                return;
            }

            if (!int.TryParse(parts[1], out int pid))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[ERROR] Invalid PID: {parts[1]}");
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("");
                return;
            }

            string dllName = parts[2];
            string fullModsDir = Path.GetFullPath(modsDir ?? Environment.CurrentDirectory);
            string dllPath = Path.GetFullPath(Path.Combine(fullModsDir, dllName));

            if (!dllPath.StartsWith(fullModsDir, StringComparison.OrdinalIgnoreCase))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[ERROR] Invalid DLL path: {dllName}");
                Console.WriteLine("[ERROR] DLLs must be placed inside the 'Mods' folder next to MonoToolkit.exe");
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("");
                return;
            }

            
            if (!File.Exists(dllPath))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[ERROR] DLL not found: {dllName}");
                Console.WriteLine($"[ERROR] Make sure {dllName} is in the 'Mods' folder next to MonoToolkit.exe");
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("");
                return;
            }

            Process targetProcess;
            try
            {
                targetProcess = Process.GetProcessById(pid);
            }
            catch (ArgumentException)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[ERROR] Process with PID {pid} not found");
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("");
                return;
            }

            
            if (!attachedProcesses.ContainsKey(pid))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[ERROR] Not attached to process with PID {pid}");
                WriteInfoLine("[INFO] Use 'attach {pid}' first to attach to the process");
                Console.WriteLine("");
                return;
            }

            Console.WriteLine("");
            Console.WriteLine($"Injecting {dllName} into {targetProcess.ProcessName} (PID: {pid})...");

            bool injectionSuccess = InjectCustomDLL(pid, dllPath);

            if (injectionSuccess)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"[SUCCESS] {dllName} injected successfully into {targetProcess.ProcessName}");
                Console.ForegroundColor = ConsoleColor.Green;
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[ERROR] Failed to inject {dllName}");
                Console.ForegroundColor = ConsoleColor.Green;
            }

            Console.WriteLine("");
        }

        static void HandleRun(string[] parts)
        {
            if (parts.Length < 3)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("[ERROR] Usage: run <pid> <script.cs>");
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("");
                return;
            }

            if (!int.TryParse(parts[1], out int pid))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[ERROR] Invalid PID: {parts[1]}");
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("");
                return;
            }

            
            string scriptName = string.Join(" ", parts.Skip(2));
            string fullModsDir = Path.GetFullPath(modsDir ?? Environment.CurrentDirectory);
            string scriptPath = Path.GetFullPath(Path.Combine(fullModsDir, scriptName));

            if (!scriptPath.StartsWith(fullModsDir, StringComparison.OrdinalIgnoreCase))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[ERROR] Invalid script path: {scriptName}");
                Console.WriteLine("[ERROR] Scripts must be placed inside the 'Mods' folder next to MonoToolkit.exe");
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("");
                return;
            }

            
            if (!File.Exists(scriptPath))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[ERROR] Script not found: {scriptName}");
                Console.WriteLine($"[ERROR] Make sure {scriptName} is in the 'Mods' folder next to MonoToolkit.exe");
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("");
                return;
            }

            
            Process targetProcess;
            try
            {
                targetProcess = Process.GetProcessById(pid);
            }
            catch (ArgumentException)
            {
                Console.WriteLine("");
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[ERROR] Process with PID {pid} not found");
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("");
                return;
            }

            if (!attachedProcesses.ContainsKey(pid))
            {
                Console.WriteLine("");
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[ERROR] Not attached to process with PID {pid}");
                WriteInfoLine("[INFO] Use 'attach {pid}' first to attach to the process");
                Console.WriteLine("");
                return;
            }

            Console.WriteLine($"Compiling and running {scriptName} in {targetProcess.ProcessName} (PID: {pid})...");

            try
            {
                
                byte[] compiledAssembly = CompileScript(scriptPath, pid);
                if (compiledAssembly == null)
                    return;

                
                bool success = InjectCompiledScript(pid, compiledAssembly, Path.GetFileNameWithoutExtension(scriptName));
                
                if (success)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"[SUCCESS] {scriptName} compiled and executed successfully in {targetProcess.ProcessName}");
                    Console.ForegroundColor = ConsoleColor.Green;
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"[ERROR] Failed to execute {scriptName}");
                    Console.ForegroundColor = ConsoleColor.Green;
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[ERROR] Exception while processing {scriptName}: {ex.Message}");
                Console.ForegroundColor = ConsoleColor.Green;
            }

            Console.WriteLine("");
        }

        static void AddGameAssemblies(CompilerParameters parameters, string sourceCode, int processId)
        {
            try
            {
                if (!attachedProcesses.ContainsKey(processId))
                {
                    WriteWarningLine("[WARNING] Process not attached - cannot detect game assemblies");
                    return;
                }

                Console.WriteLine("[INFO] Detecting assemblies from target process...");
                
                
                Process targetProcess = Process.GetProcessById(processId);
                string processPath = targetProcess.MainModule.FileName;
                string gameDir = Path.GetDirectoryName(processPath);
                
                Console.WriteLine($"[INFO] Game directory: {gameDir}");

                
                List<string> searchPaths = new List<string>();
                
                
                searchPaths.Add(gameDir);
                
                
                string[] possibleDataFolders = Directory.GetDirectories(gameDir, "*_Data", SearchOption.TopDirectoryOnly);
                foreach (string dataFolder in possibleDataFolders)
                {
                    string managedPath = Path.Combine(dataFolder, "Managed");
                    if (Directory.Exists(managedPath))
                    {
                        searchPaths.Add(managedPath);
                        Console.WriteLine($"[INFO] Found managed folder: {managedPath}");
                    }
                }

                
                string[] commonGameAssemblies = {
                    "UnityEngine.dll",
                    "UnityEngine.*.dll",
                    "Assembly-CSharp.dll", 
                    "Assembly-CSharp-firstpass.dll",
                    "Unity.*.dll",
                    "GameNetcodeStuff.dll",
                    "Netcode*.dll",
                    "netstandard.dll"
                };

                
                string[] usedNamespaces = ExtractUsedNamespaces(sourceCode);
                
                int addedCount = 0;
                HashSet<string> addedAssemblies = new HashSet<string>();

                foreach (string searchPath in searchPaths)
                {
                    if (!Directory.Exists(searchPath)) continue;

                    foreach (string pattern in commonGameAssemblies)
                    {
                        try
                        {
                            string[] files;
                            if (pattern.Contains("*"))
                            {
                                files = Directory.GetFiles(searchPath, pattern, SearchOption.TopDirectoryOnly);
                            }
                            else
                            {
                                string fullPath = Path.Combine(searchPath, pattern);
                                files = File.Exists(fullPath) ? new[] { fullPath } : new string[0];
                            }

                            foreach (string assemblyPath in files)
                            {
                                string fileName = Path.GetFileName(assemblyPath);
                                
                                
                                if (addedAssemblies.Contains(fileName.ToLower())) continue;
                                
                                
                                bool isNeeded = false;
                                
                                if (fileName.StartsWith("UnityEngine", StringComparison.OrdinalIgnoreCase))
                                {
                                    isNeeded = usedNamespaces.Any(ns => ns.StartsWith("UnityEngine"));
                                }
                                else if (fileName.StartsWith("Assembly-CSharp", StringComparison.OrdinalIgnoreCase))
                                {
                                    isNeeded = true; 
                                }
                                else if (fileName.Contains("Netcode") || fileName.Contains("GameNetcodeStuff"))
                                {
                                    isNeeded = usedNamespaces.Any(ns => ns.Contains("Netcode") || ns.Contains("GameNetcodeStuff"));
                                }
                                else if (fileName.StartsWith("Unity.", StringComparison.OrdinalIgnoreCase))
                                {
                                    isNeeded = usedNamespaces.Any(ns => ns.StartsWith("Unity."));
                                }
                                else if (fileName.Equals("netstandard.dll", StringComparison.OrdinalIgnoreCase))
                                {
                                    isNeeded = true; 
                                }

                                if (isNeeded)
                                {
                                    try
                                    {
                                        parameters.ReferencedAssemblies.Add(assemblyPath);
                                        addedAssemblies.Add(fileName.ToLower());
                                        addedCount++;
                                    }
                                    catch (Exception ex)
                                    {
                                        WriteWarningLine($"[WARNING] Could not add {fileName}: {ex.Message}");
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            WriteWarningLine($"[WARNING] Error searching for {pattern}: {ex.Message}");
                        }
                    }
                }

                Console.WriteLine($"[INFO] Added {addedCount} game assembly references");
                
                if (addedCount == 0)
                {
                    WriteWarningLine("[WARNING] No Unity assemblies found. Make sure the target is a Unity game.");
                    Console.WriteLine("[INFO] Searched paths:");
                    foreach (string path in searchPaths)
                        Console.WriteLine($"  - {path}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Failed to detect game assemblies: {ex.Message}");
            }
        }

        static string[] ExtractUsedNamespaces(string sourceCode)
        {
            try
            {
                List<string> namespaces = new List<string>();
                string[] lines = sourceCode.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

                foreach (string line in lines)
                {
                    string trimmed = line.Trim();
                    if (trimmed.StartsWith("using ") && trimmed.EndsWith(";"))
                    {
                        string ns = trimmed.Substring(6, trimmed.Length - 7).Trim();
                        if (!string.IsNullOrEmpty(ns) && !namespaces.Contains(ns))
                        {
                            namespaces.Add(ns);
                        }
                    }
                }

                return namespaces.ToArray();
            }
            catch
            {
                return new string[0];
            }
        }

        static byte[] CompileScript(string scriptPath, int processId)
        {
            try
            {
                string sourceCode = File.ReadAllText(scriptPath);
                
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("[INFO] Compiling C# script...");
                Console.ForegroundColor = ConsoleColor.Green;

                
                CompilerParameters parameters = new CompilerParameters();
                parameters.GenerateExecutable = false;
                parameters.GenerateInMemory = true;
                parameters.IncludeDebugInformation = false;
                
                
                parameters.ReferencedAssemblies.Add("System.dll");
                parameters.ReferencedAssemblies.Add("System.Core.dll");
                parameters.ReferencedAssemblies.Add("mscorlib.dll");
                
                
                AddGameAssemblies(parameters, sourceCode, processId);

                
                using (CSharpCodeProvider provider = new CSharpCodeProvider())
                {
                    CompilerResults results = provider.CompileAssemblyFromSource(parameters, sourceCode);
                    
                    if (results.Errors.HasErrors)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("[ERROR] Compilation failed:");
                        foreach (CompilerError error in results.Errors)
                        {
                            Console.WriteLine($"  Line {error.Line}: {error.ErrorText}");
                        }
                        Console.ForegroundColor = ConsoleColor.Green;
                        return null;
                    }

                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("[INFO] Compilation successful!");
                    Console.ForegroundColor = ConsoleColor.Green;

                    
                    Assembly compiledAssembly = results.CompiledAssembly;
                    
                    
                    try
                    {
                        byte[] rawBytes = File.ReadAllBytes(compiledAssembly.Location);
                        return rawBytes;
                    }
                    catch
                    {
                        string tempPath = Path.GetTempFileName() + ".dll";

                        parameters.GenerateInMemory = false;
                        parameters.OutputAssembly = tempPath;

                        CompilerResults tempResults = provider.CompileAssemblyFromSource(parameters, sourceCode);
                        if (tempResults.Errors.HasErrors)
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine("[ERROR] Temporary compilation failed:");
                            foreach (CompilerError error in tempResults.Errors)
                            {
                                Console.WriteLine($"  Line {error.Line}: {error.ErrorText}");
                            }
                            Console.ForegroundColor = ConsoleColor.Green;
                            return null;
                        }

                        byte[] assemblyData = File.ReadAllBytes(tempPath);
                        File.Delete(tempPath);
                        return assemblyData;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[ERROR] Compilation error: {ex.Message}");
                Console.ForegroundColor = ConsoleColor.Green;
                return null;
            }
        }

        static bool InjectCompiledScript(int processId, byte[] assemblyData, string scriptName)
        {
            IntPtr hProcess = IntPtr.Zero;
            
            try
            {
                
                hProcess = OpenProcess(
                    PROCESS_CREATE_THREAD | PROCESS_QUERY_INFORMATION | PROCESS_VM_OPERATION | PROCESS_VM_WRITE | PROCESS_VM_READ,
                    false,
                    processId);

                if (hProcess == IntPtr.Zero)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("[ERROR] Failed to open process. Try running as Administrator.");
                    Console.ForegroundColor = ConsoleColor.Green;
                    return false;
                }

                
                IntPtr monoDllBase = GetRemoteModuleHandle(hProcess, "mono.dll");
                if (monoDllBase == IntPtr.Zero)
                {
                    monoDllBase = GetRemoteModuleHandle(hProcess, "mono-2.0-bdwgc.dll");
                }

                if (monoDllBase == IntPtr.Zero)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("[ERROR] Mono runtime not found. This is not a Mono game.");
                    Console.ForegroundColor = ConsoleColor.Green;
                    return false;
                }

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"[INFO] Injecting compiled script using Mono API...");
                Console.ForegroundColor = ConsoleColor.Green;

                
                using (MonoInjector injector = new MonoInjector(hProcess, monoDllBase))
                {
                    IntPtr assembly = injector.Inject(
                        assemblyData,
                        scriptName,      
                        "Loader",        
                        "Load");         
                    
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"[INFO] Script assembly loaded and Loader.Load() executed successfully");
                    Console.ForegroundColor = ConsoleColor.Green;
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[ERROR] Failed to inject compiled script: {ex.Message}");
                Console.WriteLine($"[INFO] Make sure your script has namespace '{scriptName}' with class 'Loader' and method 'Load()'");
                Console.ForegroundColor = ConsoleColor.Green;
                return false;
            }
            finally
            {
                if (hProcess != IntPtr.Zero)
                {
                    CloseHandle(hProcess);
                }
            }
        }

        static bool InjectCustomDLL(int processId, string dllPath)
        {
            IntPtr hProcess = IntPtr.Zero;
            
            try
            {
                
                hProcess = OpenProcess(
                    PROCESS_CREATE_THREAD | PROCESS_QUERY_INFORMATION | PROCESS_VM_OPERATION | PROCESS_VM_WRITE | PROCESS_VM_READ,
                    false,
                    processId);

                if (hProcess == IntPtr.Zero)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("[ERROR] Failed to open process. Try running as Administrator.");
                    Console.ForegroundColor = ConsoleColor.Green;
                    return false;
                }

                
                IntPtr monoDllBase = GetRemoteModuleHandle(hProcess, "mono.dll");
                if (monoDllBase == IntPtr.Zero)
                {
                    monoDllBase = GetRemoteModuleHandle(hProcess, "mono-2.0-bdwgc.dll");
                }

                if (monoDllBase == IntPtr.Zero)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("[ERROR] Mono runtime not found. This is not a Mono game.");
                    Console.ForegroundColor = ConsoleColor.Green;
                    return false;
                }

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"[INFO] Mono runtime detected at 0x{monoDllBase.ToInt64():X}");
                Console.ForegroundColor = ConsoleColor.Green;

                
                byte[] assemblyData = File.ReadAllBytes(dllPath);

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("[INFO] Injecting assembly using Mono API...");
                Console.ForegroundColor = ConsoleColor.Green;

                
                string fileName = Path.GetFileNameWithoutExtension(dllPath);
                
                
                using (MonoInjector injector = new MonoInjector(hProcess, monoDllBase))
                {
                    try
                    {
                        IntPtr assembly = injector.Inject(
                            assemblyData,
                            fileName,        
                            "Loader",        
                            "Load");         
                        
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"[INFO] Assembly loaded and Loader.Load() executed successfully");
                        Console.ForegroundColor = ConsoleColor.Green;
                        return true;
                    }
                    catch (Exception ex)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"[ERROR] Failed to find or execute {fileName}.Loader.Load(): {ex.Message}");
                        Console.WriteLine($"[INFO] Make sure your DLL has namespace '{fileName}' with class 'Loader' and method 'Load()'");
                        Console.ForegroundColor = ConsoleColor.Green;
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[ERROR] Exception during injection: {ex.Message}");
                Console.ForegroundColor = ConsoleColor.Green;
                return false;
            }
            finally
            {
                if (hProcess != IntPtr.Zero)
                {
                    CloseHandle(hProcess);
                }
            }
        }

        static void PrintBanner()
        {
            Console.WriteLine("");
            Console.WriteLine(" \u2588\u2588\u2588\u2557   \u2588\u2588\u2588\u2557 \u2588\u2588\u2588\u2588\u2588\u2588\u2557 \u2588\u2588\u2588\u2557   \u2588\u2588\u2557 \u2588\u2588\u2588\u2588\u2588\u2588\u2557 \u2588\u2588\u2588\u2588\u2588\u2588\u2588\u2588\u2557 \u2588\u2588\u2588\u2588\u2588\u2588\u2557  \u2588\u2588\u2588\u2588\u2588\u2588\u2557 \u2588\u2588\u2557     \u2588\u2588\u2557  \u2588\u2588\u2557\u2588\u2588\u2557\u2588\u2588\u2588\u2588\u2588\u2588\u2588\u2588\u2557");
            Console.WriteLine(" \u2588\u2588\u2588\u2588\u2557 \u2588\u2588\u2588\u2588\u2551\u2588\u2588\u2554\u2550\u2550\u2550\u2588\u2588\u2557\u2588\u2588\u2588\u2588\u2557  \u2588\u2588\u2551\u2588\u2588\u2554\u2550\u2550\u2550\u2588\u2588\u2557\u255a\u2550\u2550\u2588\u2588\u2554\u2550\u2550\u255d\u2588\u2588\u2554\u2550\u2550\u2550\u2588\u2588\u2557\u2588\u2588\u2554\u2550\u2550\u2550\u2588\u2588\u2557\u2588\u2588\u2551     \u2588\u2588\u2551 \u2588\u2588\u2554\u255d\u2588\u2588\u2551\u255a\u2550\u2550\u2588\u2588\u2554\u2550\u2550\u255d");
            Console.WriteLine(" \u2588\u2588\u2554\u2588\u2588\u2588\u2588\u2554\u2588\u2588\u2551\u2588\u2588\u2551   \u2588\u2588\u2551\u2588\u2588\u2554\u2588\u2588\u2557 \u2588\u2588\u2551\u2588\u2588\u2551   \u2588\u2588\u2551   \u2588\u2588\u2551   \u2588\u2588\u2551   \u2588\u2588\u2551\u2588\u2588\u2551   \u2588\u2588\u2551\u2588\u2588\u2551     \u2588\u2588\u2588\u2588\u2588\u2554\u255d \u2588\u2588\u2551   \u2588\u2588\u2551");
            Console.WriteLine(" \u2588\u2588\u2551\u255a\u2588\u2588\u2554\u255d\u2588\u2588\u2551\u2588\u2588\u2551   \u2588\u2588\u2551\u2588\u2588\u2551\u255a\u2588\u2588\u2557\u2588\u2588\u2551\u2588\u2588\u2551   \u2588\u2588\u2551   \u2588\u2588\u2551   \u2588\u2588\u2551   \u2588\u2588\u2551\u2588\u2588\u2551   \u2588\u2588\u2551\u2588\u2588\u2551     \u2588\u2588\u2554\u2550\u2588\u2588\u2557 \u2588\u2588\u2551   \u2588\u2588\u2551");
            Console.WriteLine(" \u2588\u2588\u2551 \u255a\u2550\u255d \u2588\u2588\u2551\u255a\u2588\u2588\u2588\u2588\u2588\u2588\u2554\u255d\u2588\u2588\u2551 \u255a\u2588\u2588\u2588\u2588\u2551\u255a\u2588\u2588\u2588\u2588\u2588\u2588\u2554\u255d   \u2588\u2588\u2551   \u255a\u2588\u2588\u2588\u2588\u2588\u2588\u2554\u255d\u255a\u2588\u2588\u2588\u2588\u2588\u2588\u2554\u255d\u2588\u2588\u2588\u2588\u2588\u2588\u2588\u2557\u2588\u2588\u2551  \u2588\u2588\u2557\u2588\u2588\u2551   \u2588\u2588\u2551");
            Console.WriteLine(" \u255a\u2550\u255d     \u255a\u2550\u255d \u255a\u2550\u2550\u2550\u2550\u2550\u255d \u255a\u2550\u255d  \u255a\u2550\u2550\u2550\u255d \u255a\u2550\u2550\u2550\u2550\u2550\u255d    \u255a\u2550\u255d    \u255a\u2550\u2550\u2550\u2550\u2550\u255d  \u255a\u2550\u2550\u2550\u2550\u2550\u255d \u255a\u2550\u2550\u2550\u2550\u2550\u2550\u255d\u255a\u2550\u255d  \u255a\u2550\u255d\u255a\u2550\u255d   \u255a\u2550\u255d");
            Console.WriteLine("                                     Created by: ModSpidr");
            Console.WriteLine("");
            Console.WriteLine("Type 'help' for available commands");
            Console.WriteLine("");
        }
    }
}
