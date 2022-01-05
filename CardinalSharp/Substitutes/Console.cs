using CardinalSharp.Compiler.Support;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CardinalSharp.Substitutes
{
    [TypeMapping(typeof(System.Console))]
    internal class Console : IConsole
    {
        [MethodMapping("console_writeline")]
        public static void WriteLine(int s, int s1) { }

        public override string? ToString()
        {
            return "Console";
        }

        public void WriteLine(int s) { }
    }

    interface IConsole
    {
        void WriteLine(int s);
    }
}
