using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CardinalSharp.Compiler;

namespace CardinalSharp.Runtime
{
    internal class ProgramLoader : IDisposable
    {
        Stream s;
        BinaryReader br;
        private bool disposedValue;

        public ProgramLoader(string file)
        {
            s = File.OpenRead(file);
            br = new BinaryReader(s);
        }

        public Records Read()
        {
            Records records = new Records();
            if (br.ReadUInt32() != 0xDEADBEEF)
                throw new Exception("Unknown file format!");
            var tr_count = br.ReadInt32();
            var mr_count = br.ReadInt32();
            var nm_count = br.ReadInt32();

            for (int i = 0; i < tr_count; i++)
            {
                Read(out TypeRecord tr);
                records.TypeRecords.Add(tr);
            }

            for (int i = 0; i < mr_count; i++)
            {
                Read(out MethodRecord mr);
                records.MethodRecords.Add(mr);
            }

            for (int i = 0; i < nm_count; i++)
            {
                var nm_key = br.ReadString();
                var nm_value = br.ReadString();
                records.NativeMethods[nm_key] = nm_value;
            }

            return records;
        }

        public void Read(out TypeRecord tr)
        {
            var name = br.ReadString();
            var isStatic = br.ReadBoolean();
            tr = new TypeRecord(name, isStatic);
            int f_count = br.ReadInt32();
            for (int i = 0; i < f_count; i++)
            {
                var f_key = br.ReadString();
                var f_value = br.ReadString();
                tr.AddField(f_key, f_value);
            }
        }

        public void Read(out MethodRecord mr)
        {
            var name = br.ReadString();
            var isStatic = br.ReadBoolean();
            var isConstr = br.ReadBoolean();
            var initLocals = br.ReadBoolean();
            var maxStackSize = br.ReadInt32();
            mr = new MethodRecord(maxStackSize, initLocals, isStatic, isConstr, name);

            var str_count = br.ReadInt32();
            mr.Strings = new string[str_count];
            for(int i = 0; i < str_count; i++)
                mr.Strings[i] = br.ReadString();

            var tkn_count = br.ReadInt32();
            mr.Tokens = new SSAToken[tkn_count];
            for (int i = 0; i < tkn_count; i++)
            {
                Read(out SSAToken tkn);
                mr.Tokens[i] = tkn;
            }
        }

        public void Read(out SSAToken st)
        {
            st = new SSAToken();
            st.ID = br.ReadInt32();
            st.InstructionOffset = br.ReadInt32();
            st.Operation = (InstructionTypes)br.ReadInt32();
            st.String = br.ReadString();

            var param_count = br.ReadInt32();
            st.Parameters = new int[param_count];
            for (int i = 0; i < param_count; i++)
                st.Parameters[i] = br.ReadInt32();

            var const_count = br.ReadInt32();
            st.Constants = new ulong[const_count];
            for (int i = 0; i <const_count; i++)
                st.Constants[i] = br.ReadUInt64();
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects)
                    br.Dispose();
                    s.Dispose();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                disposedValue = true;
            }
        }

        // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        // ~ProgramLoader()
        // {
        //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        //     Dispose(disposing: false);
        // }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
