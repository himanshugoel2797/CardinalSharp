using CardinalSharp.Compiler.Support;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace CardinalSharp.RuntimeLib
{
    [TypeMapping(typeof(object))]
    internal class CObject
    {
        public CObject() { }
        public new virtual bool Equals(object? b){ return false; }
        public new static bool Equals(object? a, object? b) { return a.Equals(b); }
        protected new virtual void Finalize() { }
        public new virtual int GetHashCode() { return 0; }
        public new Type GetType() { return null; }
        public new object MemberwiseClone() { return null; }
        public new static bool ReferenceEquals(object? a, object? b) { return false; }
        public new virtual string? ToString() { return "System.Object"; }
    }

    [TypeMapping(typeof(ValueType))]
    internal class CValueType
    {

    }

    [TypeMapping(typeof(bool))]
    internal class CBoolean
    {
        private bool m_value;
    }

    [TypeMapping(typeof(char))]
    internal class CChar
    {
        private char m_value;
    }

    [TypeMapping(typeof(string))]
    internal class CString
    {
        private char[] m_value;
    }

    [TypeMapping(typeof(int))]
    internal class CInt32
    {
        private int m_value;
    }

    [TypeMapping(typeof(MemberInfo))]
    internal class CMemberInfo
    {

    }

    [TypeMapping(typeof(Type))]
    internal class CType
    {

    }

    [TypeMapping(typeof(Attribute))]
    internal class CAttribute
    {
        public virtual object TypeId { get; }

        public virtual bool Match (object? obj)
        {
            if (obj == null) return false;
            return true;
        }

        public virtual bool IsDefaultAttribute()
        {
            return (this == default(CAttribute));
        }
    }
}
