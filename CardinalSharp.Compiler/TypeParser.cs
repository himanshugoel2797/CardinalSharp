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
        private readonly TypeCache cache;
        public Type CurrentType { get; private set; }
        public Type BaseType { get; private set; }
        public bool IsStatic { get; private set; }
        public Dictionary<string, string> Fields { get; private set; } = new();

        public TypeParser(TypeCache cache)
        {
            this.cache = cache;
        }

        public void Build(Type t, TypeResolver resolver, bool _static)
        {
            var name = NameMangler.Mangle(t);
            CurrentType = t;
            BaseType = t.BaseType;
            IsStatic = _static;
            if (t.BaseType != null) cache.Add(t.BaseType);

            //Parse fields
            foreach (var fi in (resolver.Resolve(t) as TypeInfo).DeclaredFields)
                if (fi.IsStatic == _static)
                    Fields[NameMangler.Mangle(fi.FieldType)] = fi.Name;
        }

        public string Emit()
        {
            string r = "typedef struct {\r\n";
            if (BaseType != null && !IsStatic)
                r += $"{NameMangler.Mangle(BaseType, IsStatic)} *base_obj;\r\n";
            foreach (var fi in Fields)
                r += $"{fi.Key} {fi.Value};\r\n";
            r += $"}} {NameMangler.Mangle(CurrentType, IsStatic)};\r\n\r\n";
            return r;
        }
    }
}
