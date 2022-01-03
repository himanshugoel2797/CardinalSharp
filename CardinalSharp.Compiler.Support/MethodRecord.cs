using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CardinalSharp.Compiler
{
    public class MethodRecord
    {
        public int MaxStackSize { get; set; }
        public bool InitLocals { get; set; }
        public bool IsStatic { get; }
        public bool IsConstructor { get; }
        public string Name { get; }
        public string[] Strings { get; set; }
        public SSAToken[] Tokens { get; set; }

        public MethodRecord(int maxStackSize, bool initLocals, bool isStatic, bool isConstructor, string name)
        {
            this.MaxStackSize = maxStackSize;
            this.InitLocals = initLocals;
            this.IsStatic = isStatic;
            this.IsConstructor = isConstructor;
            this.Name = name;
        }
    }
}
