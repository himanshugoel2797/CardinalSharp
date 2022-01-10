using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CardinalSharp.Compiler
{
    internal class TypeCache
    {
        Dictionary<Type, TypeParser> tableBuilders = new Dictionary<Type, TypeParser>();
        Dictionary<Type, TypeParser> staticBuilders = new Dictionary<Type, TypeParser>();
        TypeResolver resolver;

        public TypeCache(TypeResolver resolver)
        {
            this.resolver = resolver;
        }

        public void Add(Type t)
        {
            if (!tableBuilders.ContainsKey(t))
            {
                TypeParser builder = new TypeParser(this);
                tableBuilders[t] = builder;
                builder.Build(t, resolver, false);
            }
            if (!staticBuilders.ContainsKey(t))
            {
                TypeParser builder = new TypeParser(this);
                staticBuilders[t] = builder;
                builder.Build(t, resolver, true);
            }
        }

        public TypeParser Get(Type t, bool isStatic = false)
        {
            if (isStatic) return staticBuilders[t];
            return tableBuilders[t];
        }
    }
}
