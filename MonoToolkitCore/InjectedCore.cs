using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Threading;

namespace MonoToolkitCore
{
    
    public static class ModuleInitializer
    {
        public static void Initialize()
        {
            Loader.Load();
        }
    }

    public class Loader
    {
        private static bool hasInitialized = false;
        private static int initializedProcessId = -1;

        
        public static void Load()
        {
            int currentPid = System.Diagnostics.Process.GetCurrentProcess().Id;
            
            
            if (hasInitialized && initializedProcessId == currentPid && InjectedCore.IsRunning()) 
                return;
            
            hasInitialized = true;
            initializedProcessId = currentPid;
            
            InjectedCore.Start();
        }
        
        public static void ResetInitialization()
        {
            hasInitialized = false;
            initializedProcessId = -1;
        }
    }

    public class InjectedCore
    {
        private static Thread heartbeatThread;
        private static bool isRunning = false;
        private static MemoryMappedFile mmf;
        private static int currentProcessId;

        public static bool IsRunning()
        {
            return isRunning;
        }

        public static void Start()
        {
            try
            {
                currentProcessId = System.Diagnostics.Process.GetCurrentProcess().Id;
                
                
                string logPath = Path.Combine(
                    Path.GetTempPath(),
                    $"MonoToolkit_{currentProcessId}.log"
                );
                
                File.WriteAllText(logPath, 
                    $"[{DateTime.Now}] MonoToolkitCore injected successfully into PID {currentProcessId}\r\n");

                
                isRunning = true;
                heartbeatThread = new Thread(HeartbeatLoop);
                heartbeatThread.IsBackground = true;
                heartbeatThread.Start();

                File.AppendAllText(logPath, 
                    $"[{DateTime.Now}] Heartbeat system started\r\n");
            }
            catch (Exception ex)
            {
                try
                {
                    File.WriteAllText(
                        Path.Combine(Path.GetTempPath(), $"MonoToolkit_Error_{currentProcessId}.log"),
                        $"[{DateTime.Now}] ERROR: {ex}\r\n"
                    );
                }
                catch { }
            }
        }

        private static void HeartbeatLoop()
        {
            try
            {
                string mmfName = $"MonoToolkit_Heartbeat_{currentProcessId}";
                
                
                mmf = MemoryMappedFile.CreateOrOpen(mmfName, 8, MemoryMappedFileAccess.ReadWrite);

                while (isRunning)
                {
                    try
                    {
                        
                        string shutdownFlag = Path.Combine(
                            Path.GetTempPath(),
                            $"MonoToolkit_Shutdown_{currentProcessId}.flag"
                        );

                        if (File.Exists(shutdownFlag))
                        {
                            File.Delete(shutdownFlag);
                            Stop();
                            break;
                        }

                        
                        using (var accessor = mmf.CreateViewAccessor())
                        {
                            accessor.Write(0, DateTime.UtcNow.Ticks);
                        }
                    }
                    catch { }

                    Thread.Sleep(1000); 
                }
            }
            catch { }
        }

        public static void Stop()
        {
            try
            {
                isRunning = false;
                
                if (heartbeatThread != null && heartbeatThread.IsAlive)
                {
                    heartbeatThread.Join(2000);
                }
                heartbeatThread = null;

                if (mmf != null)
                {
                    mmf.Dispose();
                    mmf = null;
                }

                string logPath = Path.Combine(
                    Path.GetTempPath(),
                    $"MonoToolkit_{currentProcessId}.log"
                );
                
                File.AppendAllText(logPath, 
                    $"[{DateTime.Now}] MonoToolkitCore shutdown complete\r\n");
                    
                
                Loader.ResetInitialization();
            }
            catch { }
        }
    }
}
