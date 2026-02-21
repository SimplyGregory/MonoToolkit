using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace MonoToolkit
{
    public class ExportedFunction
    {
        public string Name { get; set; }
        public IntPtr Address { get; set; }
    }

    class MonoInjector : IDisposable
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr CreateRemoteThread(IntPtr hProcess, IntPtr lpThreadAttribute, IntPtr dwStackSize,
            IntPtr lpStartAddress, IntPtr lpParameter, uint dwCreationFlags, IntPtr lpThreadId);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);

        [DllImport("kernel32.dll")]
        static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer,
            UIntPtr nSize, out UIntPtr lpNumberOfBytesRead);

        private const string mono_get_root_domain = "mono_get_root_domain";
        private const string mono_thread_attach = "mono_thread_attach";
        private const string mono_image_open_from_data = "mono_image_open_from_data";
        private const string mono_assembly_load_from_full = "mono_assembly_load_from_full";
        private const string mono_assembly_get_image = "mono_assembly_get_image";
        private const string mono_class_from_name = "mono_class_from_name";
        private const string mono_class_get_method_from_name = "mono_class_get_method_from_name";
        private const string mono_runtime_invoke = "mono_runtime_invoke";
        private const string mono_image_strerror = "mono_image_strerror";

        private readonly Dictionary<string, IntPtr> Exports = new Dictionary<string, IntPtr>
        {
            { mono_get_root_domain, IntPtr.Zero },
            { mono_thread_attach, IntPtr.Zero },
            { mono_image_open_from_data, IntPtr.Zero },
            { mono_assembly_load_from_full, IntPtr.Zero },
            { mono_assembly_get_image, IntPtr.Zero },
            { mono_class_from_name, IntPtr.Zero },
            { mono_class_get_method_from_name, IntPtr.Zero },
            { mono_runtime_invoke, IntPtr.Zero },
            { mono_image_strerror, IntPtr.Zero }
        };

        private readonly IntPtr _processHandle;
        private readonly IntPtr _monoDllBase;
        private readonly MemoryManager _memoryManager;
        private IntPtr _rootDomain;
        private bool _attach;

        public MonoInjector(IntPtr processHandle, IntPtr monoDllBase)
        {
            _processHandle = processHandle;
            _monoDllBase = monoDllBase;
            _memoryManager = new MemoryManager(processHandle);
            ObtainMonoExports();
        }

        private void ObtainMonoExports()
        {
            
            List<ExportedFunction> exportedFunctions;
            try
            {
                exportedFunctions = GetExportedFunctions(_processHandle, _monoDllBase);
            }
            catch (Exception)
            {
                exportedFunctions = new List<ExportedFunction>();
            }

            foreach (var ef in exportedFunctions)
            {
                if (Exports.ContainsKey(ef.Name))
                {
                    Exports[ef.Name] = ef.Address;
                }
            }

            foreach (var kvp in Exports)
            {
                if (kvp.Value == IntPtr.Zero)
                {
                    throw new InvalidOperationException($"Failed to obtain the address of {kvp.Key}()");
                }
            }
        }

        private List<ExportedFunction> GetExportedFunctions(IntPtr processHandle, IntPtr moduleHandle)
        {
            var functions = new List<ExportedFunction>();

            try
            {
                
                byte[] dosHeader = new byte[64];
                if (!ReadProcessMemory(processHandle, moduleHandle, dosHeader, new UIntPtr(64), out _))
                {
                    return functions;
                }

                if (dosHeader[0] != 0x4D || dosHeader[1] != 0x5A) 
                {
                    return functions;
                }

                uint peOffset = BitConverter.ToUInt32(dosHeader, 60);
                
                
                byte[] peHeader = new byte[1024];
                IntPtr peHeaderAddr = IntPtr.Add(moduleHandle, (int)peOffset);
                if (!ReadProcessMemory(processHandle, peHeaderAddr, peHeader, new UIntPtr(1024), out _))
                {
                    return functions;
                }

                if (peHeader[0] != 0x50 || peHeader[1] != 0x45) 
                {
                    return functions;
                }

                
                
                
                
                int optionalHeaderOffset = 24;
                int exportRvaOffset = optionalHeaderOffset + 112;

                uint exportRva = BitConverter.ToUInt32(peHeader, exportRvaOffset);
                uint exportSize = BitConverter.ToUInt32(peHeader, exportRvaOffset + 4);
                
                if (exportRva == 0)
                {

                    return functions;
                }

                
                byte[] exportDir = new byte[40];
                IntPtr exportDirAddr = IntPtr.Add(moduleHandle, (int)exportRva);
                if (!ReadProcessMemory(processHandle, exportDirAddr, exportDir, new UIntPtr(40), out _))
                {
                    return functions;
                }

                uint numberOfFunctions = BitConverter.ToUInt32(exportDir, 0x14);
                uint numberOfNames = BitConverter.ToUInt32(exportDir, 0x18);
                uint addressTableRva = BitConverter.ToUInt32(exportDir, 0x1C);
                uint namePointerTableRva = BitConverter.ToUInt32(exportDir, 0x20);

                if (numberOfNames == 0)
                {
                    return functions;
                }

                byte[] nameArray = new byte[numberOfNames * 4];
                IntPtr nameArrayAddr = IntPtr.Add(moduleHandle, (int)namePointerTableRva);
                if (!ReadProcessMemory(processHandle, nameArrayAddr, nameArray, new UIntPtr(numberOfNames * 4), out _))
                {
                    return functions;
                }

                byte[] addrArray = new byte[numberOfFunctions * 4];
                IntPtr addrArrayAddr = IntPtr.Add(moduleHandle, (int)addressTableRva);
                if (!ReadProcessMemory(processHandle, addrArrayAddr, addrArray, new UIntPtr(numberOfFunctions * 4), out _))
                {
                    return functions;
                }

                for (int i = 0; i < numberOfNames; i++)
                {
                    try
                    {
                        uint nameRva = BitConverter.ToUInt32(nameArray, i * 4);
                        uint funcRva = BitConverter.ToUInt32(addrArray, i * 4);

                        byte[] nameBuffer = new byte[256];
                        IntPtr nameAddr = IntPtr.Add(moduleHandle, (int)nameRva);
                        if (ReadProcessMemory(processHandle, nameAddr, nameBuffer, new UIntPtr(256), out _))
                        {
                            int nameEnd = Array.IndexOf(nameBuffer, (byte)0);
                            if (nameEnd > 0)
                            {
                                string name = Encoding.ASCII.GetString(nameBuffer, 0, nameEnd);
                                IntPtr funcAddr = IntPtr.Add(moduleHandle, (int)funcRva);
                                functions.Add(new ExportedFunction { Name = name, Address = funcAddr });
                            }
                        }
                    }
                    catch
                    {
                        continue;
                    }
                }
            }
            catch
            {
                
            }

            return functions;
        }

        public IntPtr Inject(byte[] rawAssembly, string @namespace, string className, string methodName)
        {
            IntPtr rawImage, assembly, image, @class, method;

            _rootDomain = GetRootDomain();
            rawImage = OpenImageFromData(rawAssembly);
            _attach = true;
            assembly = OpenAssemblyFromImage(rawImage);
            image = GetImageFromAssembly(assembly);
            @class = GetClassFromName(image, @namespace, className);
            method = GetMethodFromName(@class, methodName);
            RuntimeInvoke(method);
            return assembly;
        }

        public IntPtr InjectAssembly(byte[] rawAssembly)
        {
            IntPtr rawImage, assembly;

            _rootDomain = GetRootDomain();
            rawImage = OpenImageFromData(rawAssembly);
            _attach = true;
            assembly = OpenAssemblyFromImage(rawImage);
            return assembly;
        }

        private IntPtr GetRootDomain()
        {
            IntPtr rootDomain = Execute(Exports[mono_get_root_domain]);
            if (rootDomain == IntPtr.Zero)
                throw new InvalidOperationException($"{mono_get_root_domain}() returned NULL");
            return rootDomain;
        }

        private IntPtr OpenImageFromData(byte[] assembly)
        {
            IntPtr statusPtr = _memoryManager.Allocate(4);
            IntPtr rawImage = Execute(Exports[mono_image_open_from_data],
                _memoryManager.AllocateAndWrite(assembly), new IntPtr(assembly.Length), new IntPtr(1), statusPtr);

            int status = BitConverter.ToInt32(_memoryManager.Read(statusPtr, 4), 0);
            if (status != 0)
            {
                IntPtr messagePtr = Execute(Exports[mono_image_strerror], new IntPtr(status));
                byte[] messageBytes = _memoryManager.Read(messagePtr, 256);
                int messageEnd = Array.IndexOf(messageBytes, (byte)0);
                string message = messageEnd > 0 ? Encoding.UTF8.GetString(messageBytes, 0, messageEnd) : "Unknown error";
                throw new InvalidOperationException($"{mono_image_open_from_data}() failed: {message}");
            }

            return rawImage;
        }

        private IntPtr OpenAssemblyFromImage(IntPtr image)
        {
            IntPtr statusPtr = _memoryManager.Allocate(4);
            IntPtr assembly = Execute(Exports[mono_assembly_load_from_full],
                image, _memoryManager.AllocateAndWrite(new byte[1]), statusPtr, IntPtr.Zero);

            int status = BitConverter.ToInt32(_memoryManager.Read(statusPtr, 4), 0);
            if (status != 0)
            {
                IntPtr messagePtr = Execute(Exports[mono_image_strerror], new IntPtr(status));
                byte[] messageBytes = _memoryManager.Read(messagePtr, 256);
                int messageEnd = Array.IndexOf(messageBytes, (byte)0);
                string message = messageEnd > 0 ? Encoding.UTF8.GetString(messageBytes, 0, messageEnd) : "Unknown error";
                throw new InvalidOperationException($"{mono_assembly_load_from_full}() failed: {message}");
            }

            return assembly;
        }

        private IntPtr GetImageFromAssembly(IntPtr assembly)
        {
            IntPtr image = Execute(Exports[mono_assembly_get_image], assembly);
            if (image == IntPtr.Zero)
                throw new InvalidOperationException($"{mono_assembly_get_image}() returned NULL");
            return image;
        }

        private IntPtr GetClassFromName(IntPtr image, string @namespace, string className)
        {
            IntPtr @class = Execute(Exports[mono_class_from_name], image, 
                _memoryManager.AllocateAndWrite(@namespace ?? ""), _memoryManager.AllocateAndWrite(className));
            if (@class == IntPtr.Zero)
                throw new InvalidOperationException($"{mono_class_from_name}() returned NULL");
            return @class;
        }

        private IntPtr GetMethodFromName(IntPtr @class, string methodName)
        {
            IntPtr method = Execute(Exports[mono_class_get_method_from_name], @class, 
                _memoryManager.AllocateAndWrite(methodName), IntPtr.Zero);
            if (method == IntPtr.Zero)
                throw new InvalidOperationException($"{mono_class_get_method_from_name}() returned NULL");
            return method;
        }

        private void RuntimeInvoke(IntPtr method)
        {
            Execute(Exports[mono_runtime_invoke], method, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
        }

        private IntPtr Execute(IntPtr address, params IntPtr[] args)
        {
            IntPtr retValPtr = _memoryManager.Allocate(IntPtr.Size);

            byte[] code = Assemble64(address, retValPtr, args);
            IntPtr alloc = _memoryManager.Allocate(code.Length, true);
            _memoryManager.Write(alloc, code);

            IntPtr thread = CreateRemoteThread(_processHandle, IntPtr.Zero, IntPtr.Zero, alloc, IntPtr.Zero, 0, IntPtr.Zero);
            if (thread == IntPtr.Zero)
                throw new InvalidOperationException("Failed to create a remote thread");

            if (WaitForSingleObject(thread, 5000) != 0)
            {
                CloseHandle(thread);
                throw new InvalidOperationException("Failed to wait for remote thread");
            }

            CloseHandle(thread);

            byte[] resultData = _memoryManager.Read(retValPtr, IntPtr.Size);
            return new IntPtr(BitConverter.ToInt64(resultData, 0));
        }

        private byte[] Assemble64(IntPtr functionPtr, IntPtr retValPtr, IntPtr[] args)
        {
            Assembler asm = new Assembler();

            asm.SubRsp(40);

            if (_attach)
            {
                asm.MovRax(Exports[mono_thread_attach]);
                asm.MovRcx(_rootDomain);
                asm.CallRax();
            }

            asm.MovRax(functionPtr);

            for (int i = 0; i < args.Length; i++)
            {
                switch (i)
                {
                    case 0:
                        asm.MovRcx(args[i]);
                        break;
                    case 1:
                        asm.MovRdx(args[i]);
                        break;
                    case 2:
                        asm.MovR8(args[i]);
                        break;
                    case 3:
                        asm.MovR9(args[i]);
                        break;
                }
            }

            asm.CallRax();
            asm.AddRsp(40);
            asm.MovRaxTo(retValPtr);
            asm.Return();

            return asm.ToByteArray();
        }

        public void Dispose()
        {
            _memoryManager?.Dispose();
        }
    }
}
