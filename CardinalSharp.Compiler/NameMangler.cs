using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace CardinalSharp.Compiler
{
    internal class NameMangler
    {
        public static string Mangle(Type t, bool isStatic = false)
        {
            return t.FullName.Replace(".", "____") + (isStatic ? "_static" : "");
        }

        public static string Mangle(Type returnType, Type[] parameterTypes, string name, bool isStatic, bool isConstr)
        {
            var s = Mangle(returnType) + "___";
            foreach (var type in parameterTypes)
                s += Mangle(type) + "__";
            s += "_" + name.Replace(".", "____") + (isStatic ? "_static" : "") + (isConstr ? "_constr" : "");
            return s;
        }

        public static string Mangle(MethodInfo mi)
        {
            return Mangle(mi.ReturnType, mi.GetParameters().Select(a => a.ParameterType).ToArray(), $"{mi.ReflectedType.FullName}.{mi.Name}".Replace(".", "____"), mi.IsStatic, false);
        }

        public static string Mangle(ConstructorInfo mi)
        {
            return Mangle(typeof(void), mi.GetParameters().Select(a => a.ParameterType).ToArray(), $"{mi.ReflectedType.FullName}.{mi.Name}".Replace(".", "____"), mi.IsStatic, true);
        }
    }
}
