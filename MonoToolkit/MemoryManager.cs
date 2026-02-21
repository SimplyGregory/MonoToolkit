using System;
using System.Text;
using System.Runtime.InteropServices;

namespace MonoToolkit
{
    
    class MemoryManager : IDisposable
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr VirtualAllocEx(IntPtr hProcess, IntPtr lpAddress, UIntPtr dwSize, 
            uint flAllocationType, uint flProtect);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, 
            UIntPtr nSize, out UIntPtr lpNumberOfBytesWritten);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, 
            UIntPtr nSize, out UIntPtr lpNumberOfBytesRead);

        private const uint MEM_COMMIT = 0x1000;
        private const uint PAGE_READWRITE = 0x04;
        private const uint PAGE_EXECUTE_READWRITE = 0x40;

        private readonly IntPtr _processHandle;

        public MemoryManager(IntPtr processHandle)
        {
            _processHandle = processHandle;
        }

        public IntPtr Allocate(int size, bool executable = false)
        {
            uint protect = executable ? PAGE_EXECUTE_READWRITE : PAGE_READWRITE;
            return VirtualAllocEx(_processHandle, IntPtr.Zero, new UIntPtr((uint)size), MEM_COMMIT, protect);
        }

        public bool Write(IntPtr address, byte[] data)
        {
            return WriteProcessMemory(_processHandle, address, data, new UIntPtr((uint)data.Length), out _);
        }

        public byte[] Read(IntPtr address, int size)
        {
            byte[] buffer = new byte[size];
            if (ReadProcessMemory(_processHandle, address, buffer, new UIntPtr((uint)size), out _))
                return buffer;
            return null;
        }

        public IntPtr AllocateAndWrite(string value)
        {
            byte[] data = Encoding.UTF8.GetBytes(value + '\0');
            IntPtr addr = Allocate(data.Length);
            if (addr != IntPtr.Zero && Write(addr, data))
                return addr;
            return IntPtr.Zero;
        }

        public IntPtr AllocateAndWrite(byte[] value)
        {
            IntPtr addr = Allocate(value.Length);
            if (addr != IntPtr.Zero && Write(addr, value))
                return addr;
            return IntPtr.Zero;
        }

        public IntPtr AllocateAndWrite(int value)
        {
            byte[] data = BitConverter.GetBytes(value);
            IntPtr addr = Allocate(data.Length);
            if (addr != IntPtr.Zero && Write(addr, data))
                return addr;
            return IntPtr.Zero;
        }

        public void Dispose()
        {
            
        }
    }
}