using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CardinalSharp.Compiler
{
    internal class VTableCache
    {
        Dictionary<Type, VTableBuilder> tableBuilders = new Dictionary<Type, VTableBuilder>();
        TypeResolver resolver;

        public VTableCache(TypeResolver resolver)
        {
            this.resolver = resolver;
        }

        public void Add(Type t)
        {
            if (!tableBuilders.ContainsKey(t))
            {
                VTableBuilder builder = new VTableBuilder(this);
                tableBuilders[t] = builder;
                builder.Build(t, resolver);
            }
        }
        public VTable Get(Type t)
        {
            if (!tableBuilders.ContainsKey(t)) Add(t);
            return tableBuilders[t].Emit();
        }

        public void Compile()
        {
            VTable[] finalTables = new VTable[tableBuilders.Count];
            VTableBuilder[] builders = tableBuilders.Values.ToArray();
            for (int i = 0; i < builders.Length; i++)
                finalTables[i] = builders[i].Emit();

            foreach (VTable vTable in finalTables)
            {
                vTable.UpdateOffsets();
                vTable.BuildTypeConversionTable();
            }

            //TODO: Generate code for all methods in the vtables
            //TODO: Emit code implementing the vtable
        }
    }
}
