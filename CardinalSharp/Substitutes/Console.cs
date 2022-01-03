using CardinalSharp.Compiler.Support;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CardinalSharp.Substitutes
{
    [TypeMapping(typeof(System.Console))]
    internal class Console
    {
        [MethodMapping("console_writeline")]
        public static void WriteLine(string s) { }
    }
}
