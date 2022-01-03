using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace CardinalSharp.Compiler
{
    public class ILStream
    {
        private OpCode[] opcodes;
        private OpCode[] secondary_opcodes;

        const byte SecondaryOpcodeLo = 0xFE;

        public byte[] IL { get; }

        public int CurrentOffset { get; set; }

        public ILStream(byte[] il)
        {
            this.IL = (byte[])il.Clone();

            opcodes = new OpCode[byte.MaxValue + 1];
            secondary_opcodes = new OpCode[byte.MaxValue + 1];

            var fields = typeof(OpCodes).GetFields();
            for (int i = 0; i < fields.Length; i++)
                if (fields[i].FieldType == typeof(OpCode))
                {
                    OpCode opc = (OpCode)fields[i].GetValue(null)!;
                    if ((byte)(opc.Value >> 8) != SecondaryOpcodeLo)
                        opcodes[(byte)opc.Value] = opc;
                    else
                        secondary_opcodes[(byte)(opc.Value & 0xff)] = opc;
                }
        }

        public void Reset()
        {
            CurrentOffset = 0;
        }

        public bool NextInstruction()
        {
            if (CurrentOffset >= IL.Length)
                return false;

            int opcode_size = 0;

            OpCode opc = GetCurrentOpCode();
            for (uint i = 0; i < GetParameterCount(); i++)
                opcode_size += GetParameterSize(i);

            CurrentOffset += opc.Size + opcode_size;

            return CurrentOffset < IL.Length;
        }

        public OpCode GetCurrentOpCode()
        {
            OpCode opc = opcodes[IL[CurrentOffset]];
            if (IL[CurrentOffset] == SecondaryOpcodeLo)
            {
                opc = secondary_opcodes[IL[CurrentOffset + 1]];
            }
            return opc;
        }

        public uint GetParameterCount()
        {
            if (GetCurrentOpCode().OperandType == OperandType.InlineNone)
                return 0;

            if (GetCurrentOpCode().Value == OpCodes.Switch.Value)
                return BitConverter.ToUInt32(IL, CurrentOffset + 1) + 1;

            return 1;
        }

        public OperandType GetParameterType(uint i)
        {
            return GetCurrentOpCode().OperandType;
        }

        public int GetParameterSize(uint index)
        {
            if (CurrentOffset >= IL.Length)
                throw new Exception();

            int sz = 0;

            if (GetCurrentOpCode().Value == OpCodes.Switch.Value)
                return 4;

            switch (GetCurrentOpCode().OperandType)
            {
                case OperandType.InlineNone:
                    sz = 0;
                    break;
                case OperandType.InlineI:
                    sz = 4;
                    break;
                case OperandType.InlineBrTarget:
                    sz = 4;
                    break;
                case OperandType.InlineField:
                    sz = 4;
                    break;
                case OperandType.InlineI8:
                    sz = 8;
                    break;
                case OperandType.InlineMethod:
                    sz = 4;
                    break;
                case OperandType.InlineR:
                    sz = 8;
                    break;
                case OperandType.InlineSig:
                    sz = 4;
                    break;
                case OperandType.InlineString:
                    sz = 4;
                    break;
                case OperandType.ShortInlineR:
                    sz = 4;
                    break;
                case OperandType.ShortInlineI:
                    sz = 1;
                    break;
                case OperandType.ShortInlineVar:
                    sz = 1;
                    break;
                case OperandType.ShortInlineBrTarget:
                    sz = 1;
                    break;
                case OperandType.InlineVar:
                    sz = 2;
                    break;
                case OperandType.InlineType:
                    sz = 4;
                    break;
                case OperandType.InlineTok:
                    sz = 4;
                    break;
            }

            return sz;
        }

        public ulong GetParameter(uint index)
        {
            ulong retVal = 0;

            if (CurrentOffset >= IL.Length)
                throw new Exception();

            //Find the parameter offset
            int pos = CurrentOffset + 1;
            for (uint i = 0; i < index; i++)
                pos += GetParameterSize(i);

            switch (GetParameterSize(index))
            {
                case 0:
                    throw new Exception();
                case 1:
                    retVal = IL[pos];
                    if (IL[CurrentOffset] == SecondaryOpcodeLo)
                        retVal = IL[pos + 1];
                    break;
                case 2:
                    retVal = BitConverter.ToUInt16(IL, pos);
                    if (IL[CurrentOffset] == SecondaryOpcodeLo)
                        retVal = BitConverter.ToUInt16(IL, pos + 1);
                    break;
                case 4:
                    retVal = BitConverter.ToUInt32(IL, pos);
                    if (IL[CurrentOffset] == SecondaryOpcodeLo)
                        retVal = BitConverter.ToUInt32(IL, pos + 1);
                    break;
                case 8:
                    retVal = BitConverter.ToUInt64(IL, pos);
                    if (IL[CurrentOffset] == SecondaryOpcodeLo)
                        retVal = BitConverter.ToUInt64(IL, pos + 1);
                    break;
            }

            return retVal;
        }

    }
}
