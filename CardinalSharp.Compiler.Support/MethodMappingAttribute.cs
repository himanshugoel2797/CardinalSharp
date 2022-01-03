using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CardinalSharp.Compiler.Support
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public class MethodMappingAttribute : Attribute
    {
        public MethodMappingAttribute(string mthd_name)
        {
            MethodName = mthd_name;
        }

        public string MethodName { get; }
    }
}
