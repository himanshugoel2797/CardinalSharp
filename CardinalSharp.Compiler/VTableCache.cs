using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace CardinalSharp.Compiler
{
    internal class VTableCache
    {
        Dictionary<Type, VTableBuilder> tableBuilders = new();
        HashSet<MethodInfo> methods = new();
        TypeResolver resolver;
        TypeCache typeCache;

        public VTableCache(TypeResolver resolver)
        {
            this.resolver = resolver;
            this.typeCache = new TypeCache(resolver);
        }

        public void Add(Type t)
        {
            if (t.IsArray) t = t.GetElementType();
            if (!tableBuilders.ContainsKey(t))
            {
                VTableBuilder builder = new VTableBuilder(this);
                tableBuilders[t] = builder;
                builder.Build(t, resolver);
                typeCache.Add(t);
            }
        }
        public VTable Get(Type t)
        {
            if (!tableBuilders.ContainsKey(t)) Add(t);
            return tableBuilders[t].Emit();
        }

        public void CompileMethod(MethodInfo m)
        {
            methods.Add(m);
        }

        public void Compile()
        {
            VTable[] finalTables = new VTable[tableBuilders.Count];
            VTableBuilder[] builders = tableBuilders.Values.ToArray();
            for (int i = 0; i < builders.Length; i++)
                finalTables[i] = builders[i].Emit();

            string typeTable = "";

            foreach (VTable vTable in finalTables)
            {
                vTable.UpdateOffsets();
                vTable.BuildTypeConversionTable();
                typeTable += typeCache.Get(vTable.CurrentType, isStatic: true).Emit();
                typeTable += typeCache.Get(vTable.CurrentType, isStatic: false).Emit();
            }

            foreach (VTable v in finalTables)
            {
                //TODO: Generate code for all methods in the vtables
                //TODO: Emit code implementing the vtable
                //TODO: Generate structure representing each type

                //TODO: Generate static field structures and methods
            }
        }
    }
}
