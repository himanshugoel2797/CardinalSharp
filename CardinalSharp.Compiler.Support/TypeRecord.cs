using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CardinalSharp.Compiler
{
    public class TypeRecord
    {
        public TypeRecord(string name, bool isStatic)
        {
            this.Fields = new Dictionary<string, string>();
            this.Name = name;
            this.IsStatic = isStatic;
        }

        public string Name { get; set; }
        public bool IsStatic { get; set; }
        public Dictionary<string, string> Fields { get; set; }

        public void AddField(string t, string name)
        {
            Fields.Add(t, name);
        }
    }
}
