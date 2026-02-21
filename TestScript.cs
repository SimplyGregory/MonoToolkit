using System;
using System.IO;

namespace TestScript
{
    public class Loader
    {
        public static void Load()
        {
            try
            {
                string downloadsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
                string testFile = Path.Combine(downloadsPath, "TestScript_Success.txt");
                
                File.WriteAllText(testFile, string.Format("TestScript executed successfully at {0}\n" +
                                           "Process ID: {1}\n" +
                                           "Process Name: {2}\n" +
                                           "Compiled and injected via MonoToolkit RUN command",
                                           DateTime.Now,
                                           System.Diagnostics.Process.GetCurrentProcess().Id,
                                           System.Diagnostics.Process.GetCurrentProcess().ProcessName));
                
                Console.WriteLine(string.Format("[TestScript] Success file created: {0}", testFile));
            }
            catch (Exception ex)
            {
                Console.WriteLine(string.Format("[TestScript] Error: {0}", ex.Message));
            }
        }
    }
}