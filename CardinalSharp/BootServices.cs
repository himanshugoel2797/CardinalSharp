using CardinalSharp.Compiler.Support;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CardinalSharp
{
    internal class BootServices
    {
        public const int Multiboot2Magic = 0x36d76289;

        [MethodMapping("getBootloaderID")]
        public static int GetBootloaderID() { return 0; }
    }
}
