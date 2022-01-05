using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace CardinalSharp.Compiler
{
    internal class VTableBuilder
    {
        private VTableCache cache;
        private Type t;
        private List<MethodInfo> non_vtable_members = new List<MethodInfo>();
        private List<MethodInfo> members = new List<MethodInfo>();
        private List<MethodInfo> abstract_members = new List<MethodInfo>();
        private VTable base_vtable;
        private Dictionary<Type, VTable> interfaces = new Dictionary<Type, VTable>();

        public VTableBuilder(VTableCache cache)
        {
            this.cache = cache;
        }
        private void AddMethod(MethodInfo mthd)
        {
            non_vtable_members.Add(mthd);
        }
        private void AddVirtualMethod(MethodInfo mthd)
        {
            members.Add(mthd);
        }
        private void AddAbstractMethod(MethodInfo mthd) { abstract_members.Add(mthd); }
        private void AddInheritedMethod(MethodInfo mthd, TypeResolver resolver)
        {
            base_vtable?.UpdateMethod(mthd, resolver);
            if (!mthd.IsFinal)
                AddVirtualMethod(mthd);
            else
                AddMethod(mthd);
        }
        private void AddInterface(Type i)
        {
            if (!interfaces.ContainsKey(i)) interfaces[i] = cache.Get(i);
        }

        private void AddInterfaceMethod(MethodInfo mthd, TypeResolver resolver)
        {
            interfaces[mthd.DeclaringType].UpdateMethod(mthd, resolver);
        }

        public void Build(Type t, TypeResolver resolver)
        {
            this.t = t;
            //vtable only includes non-virtual methods
            /*
             * vtable structure:
             *    - main vtable pointer
             *    - type conversion table pointer
             *    - virtual/abstract methods defined in this type
             *    - vtable for base class
             *    - vtables for all interfaces
             */
            if (t.BaseType != null)
                base_vtable = cache.Get(t.BaseType);

            var t_res = resolver.Resolve(t);
            var interfaces = t_res.GetInterfaces();
            foreach (var i in interfaces)
            {
                AddInterface(i);
                var map = t_res.GetInterfaceMap(i);
                for (int j = 0; j < map.InterfaceMethods.Length; j++)
                {
                    this.interfaces[i].UpdateMethodInterface(map.InterfaceMethods[j], map.TargetMethods[j]);
                }
            }

            var mthds = t_res.GetMethods(BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            foreach (var mth in mthds)
            {
                var base_mth = mth.GetBaseDefinition();
                if (mth.IsAbstract)
                    AddAbstractMethod(mth);
                //else if (mth != base_mth && base_mth.DeclaringType.IsInterface)
                //    AddInterfaceMethod(mth, resolver);
                else if (mth != base_mth && mth.IsVirtual)
                    AddInheritedMethod(mth, resolver);
                else if (mth.IsVirtual && !mth.IsFinal)
                    AddVirtualMethod(mth);
                else if (mth.DeclaringType == t_res)
                    AddMethod(mth);
                else
                    Console.WriteLine($"Ignoring method: {mth.DeclaringType.Name}.{mth.Name}");
            }
        }

        //Emit a populated default for this vtable
        public VTable Emit()
        {
            VTable v = new VTable(t, members.Concat(abstract_members).ToArray(), base_vtable?.Clone(), interfaces.Values.Select(a => a.Clone()).ToArray());
            return v;
        }
    }
}
