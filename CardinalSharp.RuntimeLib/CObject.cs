using CardinalSharp.Compiler.Support;
using System;
using System.Collections.Generic;
using System.Linq;
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
}
