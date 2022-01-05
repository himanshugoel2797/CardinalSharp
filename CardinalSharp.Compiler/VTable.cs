using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace CardinalSharp.Compiler
{
    internal class VTable
    {
        public int ParentOffset; //Offset to top level type (ie the 'true' type of the object)
        public int TypeConversionTableOffset; //Offset to table of offsets for inherited type vtables, only needs to be populated at the 'true' type level
        public Type CurrentType;
        public Type ResolvedCurrentType;
        public MethodInfo[] Members;
        public VTable BaseClass;
        public VTable[] Interfaces;
        public Dictionary<string, int> TypeConversionTable;

        public VTable(Type curType, MethodInfo[] members, VTable baseClass, VTable[] interfaces)
        {
            CurrentType = curType;
            Members = members;
            BaseClass = baseClass;
            Interfaces = interfaces;
        }

        private bool UpdateMethodInternal(MethodInfo mthd, TypeResolver typeResolver)
        {
            Type baseDeclaringType = typeResolver.Resolve(mthd.GetBaseDefinition().DeclaringType);
            bool update_made = false;
            for (int i = 0; i < Members.Length; i++)
            {
                var cm = Members[i];
                if (typeResolver.Resolve(cm.GetBaseDefinition().DeclaringType) != baseDeclaringType) continue;

                bool match = (cm.Name == mthd.Name) && (cm.ReturnType == mthd.ReturnType);

                var p0 = cm.GetParameters();
                var p1 = mthd.GetParameters();
                match = match && p0.Length == p1.Length;
                if (!match) continue;
                for (int j = 0; j < p0.Length; j++)
                    match = match && (p0[j].ParameterType == p1[j].ParameterType) && (p0[j].IsIn == p1[j].IsIn) && (p0[j].IsOut == p1[j].IsOut) && (p0[j].IsOptional == p1[j].IsOptional);

                if (match)
                {
                    Members[i] = mthd;
                    update_made = true;
                    break;
                }
            }

            if (baseDeclaringType != typeResolver.Resolve(CurrentType))
            {
                if (BaseClass != null && BaseClass.UpdateMethodInternal(mthd, typeResolver)) update_made = true;
            }

            return update_made;
        }

        public void UpdateMethodInterface(MethodInfo matchTarget, MethodInfo replacement)
        {
            for (int i = 0; i < Members.Length; i++)
                if (Members[i] == matchTarget)
                {
                    Members[i] = replacement;
                    return;
                }
        }

        private int UpdateOffsetsInternal(bool isTop)
        {
            int offset = ParentOffset + (Members.Length + 1) * 8;

            if (BaseClass != null)
            {
                BaseClass.ParentOffset = offset;
                offset = BaseClass.UpdateOffsetsInternal(false);
            }
            if (isTop)
                for (int i = 0; i < Interfaces.Length; i++)
                {
                    Interfaces[i].ParentOffset = offset;
                    offset = Interfaces[i].UpdateOffsetsInternal(false);
                }
            else
                Interfaces = new VTable[0];
            TypeConversionTableOffset = offset; //Type conversion table is placed at the end of the table
            return offset;
        }

        public void UpdateOffsets()
        {
            UpdateOffsetsInternal(true);
        }

        private void GetSubtables(Dictionary<string, int> table)
        {
            var name = NameMangler.Mangle(CurrentType);
            if (!table.ContainsKey(name))
                table[name] = ParentOffset;

            if (BaseClass != null)
                BaseClass.GetSubtables(table);

            for (int i = 0; i < Interfaces.Length; i++)
                table[NameMangler.Mangle(Interfaces[i].CurrentType)] = Interfaces[i].ParentOffset;
        }

        public void BuildTypeConversionTable()
        {
            //Gather type conversions and offsets from all child vtables
            Dictionary<string, int> table = new Dictionary<string, int>();
            GetSubtables(table);
            TypeConversionTable = table;
        }

        public void UpdateMethod(MethodInfo mthd, TypeResolver typeResolver, bool expect_match = true)
        {
            if (!UpdateMethodInternal(mthd, typeResolver) && expect_match) throw new Exception("Could not find associated method to update!");
        }

        public VTable Clone()
        {
            var n_members = new MethodInfo[Members.Length];
            Array.Copy(Members, 0, n_members, 0, Members.Length);
            return new VTable(CurrentType, n_members, BaseClass?.Clone(), Interfaces.Select(a => a.Clone()).ToArray());
        }
    }
}
