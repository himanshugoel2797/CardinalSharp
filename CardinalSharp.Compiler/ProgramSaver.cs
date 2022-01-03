using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CardinalSharp.Compiler
{
    internal class ProgramSaver : IDisposable
    {
        Stream s;
        BinaryWriter sw;
        private bool disposedValue;

        public ProgramSaver(string file)
        {
            s = File.OpenWrite(file);
            sw = new BinaryWriter(s, Encoding.UTF8);
        }

        public void Write(Records r)
        {
            //Write the header
            sw.Write(0xDEADBEEF);
            sw.Write(r.TypeRecords.Count);
            sw.Write(r.MethodRecords.Count);
            sw.Write(r.NativeMethods.Count);

            foreach (TypeRecord tr in r.TypeRecords)
                Write(tr);
            foreach (MethodRecord mr in r.MethodRecords)
                Write(mr);
            foreach (var nm in r.NativeMethods)
            {
                sw.Write(nm.Key);
                sw.Write(nm.Value);
            }

            sw.Flush();
        }

        public void Write(TypeRecord r)
        {
            sw.Write(r.Name);
            sw.Write(r.IsStatic);
            sw.Write(r.Fields.Count);
            foreach (var f in r.Fields)
            {
                sw.Write(f.Key);
                sw.Write(f.Value);
            }
        }

        public void Write(MethodRecord r)
        {
            sw.Write(r.Name);
            sw.Write(r.IsStatic);
            sw.Write(r.IsConstructor);
            sw.Write(r.InitLocals);
            sw.Write(r.MaxStackSize);
            sw.Write(r.Strings.Length);
            foreach (var s in r.Strings)
                sw.Write(s);
            sw.Write(r.Tokens.Length);
            foreach (var t in r.Tokens)
                Write(t);
        }

        public void Write(SSAToken tkn)
        {
            if (tkn.String == null) tkn.String = "";

            sw.Write(tkn.ID);
            sw.Write(tkn.InstructionOffset);
            sw.Write((int)tkn.Operation);
            sw.Write(tkn.String);
            if (tkn.Parameters != null)
            {
                sw.Write(tkn.Parameters.Length);
                foreach (var p in tkn.Parameters)
                    sw.Write(p);
            }
            else
                sw.Write(0);

            if (tkn.Constants != null)
            {
                sw.Write(tkn.Constants.Length);
                foreach (var p in tkn.Constants)
                    sw.Write(p);
            }
            else
                sw.Write(0);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects)
                    sw.Dispose();
                    s.Dispose();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                disposedValue = true;
            }
        }

        // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        // ~ProgramSaver()
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
