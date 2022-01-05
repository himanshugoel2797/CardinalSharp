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
            return parent_type.GetMethod(m.Name, BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic, m.GetParameters().Select(a => a.ParameterType).ToArray());
        }

        public ConstructorInfo Resolve(ConstructorInfo m)
        {
            //Resolve parent type and find method again
            var parent_type = Resolve(m.ReflectedType);
            return parent_type.GetConstructor(m.GetParameters().Select(a => a.ParameterType).ToArray());
        }

        const int PointerSize = 8;
        public int GetTypeRefSize(Type t)
        {
            if (t.FullName == typeof(void).FullName)
                return 0;
            if (t.FullName == typeof(string).FullName)
                return PointerSize;
            if (t.FullName == typeof(long).FullName)
                return 8;
            if (t.FullName == typeof(ulong).FullName)
                return 8;
            if (t.FullName == typeof(double).FullName)
                return 8;
            if (t.FullName == typeof(int).FullName)
                return 4;
            if (t.FullName == typeof(uint).FullName)
                return 4;
            if (t.FullName == typeof(float).FullName)
                return 4;
            if (t.FullName == typeof(short).FullName)
                return 2;
            if (t.FullName == typeof(ushort).FullName)
                return 2;
            if (t.FullName == typeof(char).FullName)
                return 1;
            if (t.FullName == typeof(byte).FullName)
                return 1;
            if (t.FullName == typeof(sbyte).FullName)
                return 1;
            if (t.FullName == typeof(bool).FullName)
                return 1;

            return PointerSize;
        }

        public int[] GetTypeRefSize(params Type[] t)
        {
            return t.Select(a => GetTypeRefSize(a)).ToArray();
        }

        public string GetTypeRefName(Type t)
        {
            if (t.FullName == typeof(void).FullName)
                return "void";
            if (t.FullName == typeof(string).FullName)
                return "rt_obj_t*";
            if (t.FullName == typeof(long).FullName)
                return "int64_t";
            if (t.FullName == typeof(ulong).FullName)
                return "uint64_t";
            if (t.FullName == typeof(double).FullName)
                return "double";
            if (t.FullName == typeof(int).FullName)
                return "int32_t";
            if (t.FullName == typeof(uint).FullName)
                return "uint32_t";
            if (t.FullName == typeof(float).FullName)
                return "float";
            if (t.FullName == typeof(short).FullName)
                return "int16_t";
            if (t.FullName == typeof(ushort).FullName)
                return "uint16_t";
            if (t.FullName == typeof(char).FullName)
                return "uint8_t";
            if (t.FullName == typeof(byte).FullName)
                return "uint8_t";
            if (t.FullName == typeof(sbyte).FullName)
                return "int8_t";
            if (t.FullName == typeof(bool).FullName)
                return "int8_t";

            return "rt_obj_t*";
        }

        public string[] GetTypeRefName(params Type[] t)
        {
            return t.Select(a => GetTypeRefName(a)).ToArray();
        }
    }
}
