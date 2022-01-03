using System;
using System.Collections.Generic;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace CardinalSharp.Runtime
{
    internal class ExecutableMemory
    {
        public const int Size = 4 * 1024 * 1024; //4MiB

        MemoryMappedFile mem;
        MemoryMappedViewAccessor exec_accessor;
        MemoryMappedViewAccessor write_accessor;

        public ExecutableMemory()
        {
            mem = MemoryMappedFile.CreateNew(null, Size, MemoryMappedFileAccess.ReadWriteExecute, MemoryMappedFileOptions.None, HandleInheritability.None);
            exec_accessor = mem.CreateViewAccessor(0, Size, MemoryMappedFileAccess.ReadExecute);
            write_accessor = mem.CreateViewAccessor(0, Size, MemoryMappedFileAccess.Write);
        }

        public byte this[int offset]
        {
            get { return exec_accessor.ReadByte(offset); }
            set { write_accessor.Write(offset, value); }
        }

        public void Execute()
        {
            unsafe
            {
                byte* ptr = null;
                exec_accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref ptr);
                var f = Marshal.GetDelegateForFunctionPointer<Func<int>>((IntPtr)ptr);
                Console.WriteLine($"Finished Execution: {f()}");
                exec_accessor.SafeMemoryMappedViewHandle.ReleasePointer();
            }
        }
    }
}
