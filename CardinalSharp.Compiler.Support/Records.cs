using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CardinalSharp.Compiler
{
    public class Records
    {
        public List<TypeRecord> TypeRecords { get; set; }
        public List<MethodRecord> MethodRecords { get; set; }
        public HashSet<string> TypeNames { get; set; }
        public HashSet<string> MethodNames { get; set; }
        public Dictionary<string, string> NativeMethods { get; set; }

        public Records()
        {
            TypeRecords = new List<TypeRecord>();
            MethodRecords = new List<MethodRecord>();
            TypeNames = new HashSet<string>();
            MethodNames = new HashSet<string>();
            NativeMethods = new Dictionary<string, string>();
        }
    }
}
