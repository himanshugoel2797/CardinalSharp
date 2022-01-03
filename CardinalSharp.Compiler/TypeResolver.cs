using System;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CardinalSharp.Compiler.Support;

namespace CardinalSharp.Compiler
{
    internal class TypeResolver
    {
        Assembly CardinalSharpAsm { get; set; }
        Assembly RuntimeAsm { get; set; }

        Dictionary<Type, Type> TypeMap { get; set; }

        public TypeResolver()
        {
            TypeMap = new Dictionary<Type, Type>();
            RuntimeAsm = typeof(CardinalSharp.RuntimeLib.AssemblyRef).Assembly;
            CardinalSharpAsm = typeof(CardinalSharp.AssemblyRef).Assembly;

            //Go over all types in cardinalsharp and runtimelib and register TypeSubstituteAttributes
            foreach (var t in RuntimeAsm.GetTypes())
                RegisterType(t);
            foreach (var t in CardinalSharpAsm.GetTypes())
                RegisterType(t);
        }

        private void RegisterType(Type t)
        {
            var attrs = t.GetCustomAttributes(typeof(TypeMappingAttribute), false);
            foreach (var attr in attrs)
            {
                var attr_cast = attr as TypeMappingAttribute;
                if (attr_cast != null)
                    TypeMap[attr_cast.Target] = t;
            }
        }

        public Type Resolve(Type t)
        {
            if (TypeMap.ContainsKey(t)) return TypeMap[t];
            return t;
        }

        public bool IsNative(MethodInfo m)
        {
            m = Resolve(m);
            var mapping = m.GetCustomAttribute(typeof(MethodMappingAttribute));
            return (mapping != null);
        }

        public string GetNativeName(MethodInfo m)
        {
            m = Resolve(m);
            var mapping = m.GetCustomAttribute(typeof(MethodMappingAttribute));
            return ((MethodMappingAttribute)mapping!).MethodName;
        }

        public MethodInfo Resolve(MethodInfo m)
        {
            //Resolve parent type and find method again
            var parent_type = Resolve(m.ReflectedType);
            return parent_type.GetMethod(m.Name, m.GetParameters().Select(a => a.ParameterType).ToArray());
        }

        public ConstructorInfo Resolve(ConstructorInfo m)
        {
            //Resolve parent type and find method again
            var parent_type = Resolve(m.ReflectedType);
            return parent_type.GetConstructor(m.GetParameters().Select(a => a.ParameterType).ToArray());
        }
    }
}
