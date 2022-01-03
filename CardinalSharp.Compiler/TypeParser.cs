using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace CardinalSharp.Compiler
{
    //Reads member info and produces a TypeRecord
    internal class TypeParser
    {
        private readonly TypeResolver resolver;

        public TypeParser(Records records, TypeResolver resolver)
        {
            Records = records;
            this.resolver = resolver;
        }

        public Records Records { get; }

        public void Parse(Type t) => ParseInternal(t, false);

        public void ParseStatic(Type t) => ParseInternal(t, true);

        private void ParseInternal(Type t, bool expectStatic)
        {
            var name = NameMangler.Mangle(t, isStatic: expectStatic);
            if (Records.TypeNames.Contains(name))
                return;

            var tr = new TypeRecord(name, isStatic: expectStatic);

            //Parse fields
            foreach (var fi in resolver.Resolve(t).GetFields())
                if (fi.IsStatic == expectStatic)
                    tr.AddField(NameMangler.Mangle(fi.FieldType), fi.Name);

            Records.TypeRecords.Add(tr);
            Records.TypeNames.Add(name);
        }
    }
}
