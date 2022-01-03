using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace CardinalSharp.Compiler
{

    //Recursively parses methods and produces MethodRecords
    internal class MethodParser
    {
        private readonly TypeParser typeParser;
        private readonly TypeResolver typeResolver;

        public MethodParser(Records records, TypeParser typeParser, TypeResolver resolver)
        {
            Records = records;
            this.typeParser = typeParser;
            this.typeResolver = resolver;
        }

        public Records Records { get; }

        public void Parse(MethodInfo mthd)
        {
            var name = NameMangler.Mangle(mthd);
            if (Records.MethodNames.Contains(name))
                return;


            //Parse static members
            typeParser.ParseStatic(mthd.ReflectedType);

            //If method is not static, parse parent type
            if (!mthd.IsStatic) typeParser.Parse(mthd.ReflectedType);

            //Parse parameter types
            foreach (var param in mthd.GetParameters())
                typeParser.Parse(param.ParameterType);
            typeParser.Parse(mthd.ReturnType); //Parse return type

            //TODO: For generic types generate a specialized version of the type

            //Parse method
            var res_mthd = typeResolver.Resolve(mthd);
            var mb = res_mthd.GetMethodBody();
            if (mb == null)
                throw new NullReferenceException();
            var il = mb.GetILAsByteArray();
            if (il == null)
                throw new NullReferenceException();

            var mr = new MethodRecord(mb.MaxStackSize, mb.InitLocals, mthd.IsStatic, mthd.IsConstructor, name);
            var instructions = new ILStream(il);

            var mpi = new MethodParserInternal(this);
            mpi.Parse(instructions, res_mthd.Module);

            mr.Tokens = mpi.Tokens.ToArray();
            mr.Strings = mpi.StringTable.ToArray();
            Records.MethodRecords.Add(mr);
            Records.MethodNames.Add(name);
        }

        public void Parse(ConstructorInfo mthd)
        {
            var name = NameMangler.Mangle(mthd);
            if (Records.MethodNames.Contains(name))
                return;

            //Parse static members
            typeParser.ParseStatic(mthd.ReflectedType);

            //If method is not static, parse parent type
            if (!mthd.IsStatic) typeParser.Parse(mthd.ReflectedType);

            //Parse parameter types
            foreach (var param in mthd.GetParameters())
                typeParser.Parse(param.ParameterType);

            //TODO: For generic types generate a specialized version of the type

            //Parse method
            var res_mthd = typeResolver.Resolve(mthd);
            var mb = res_mthd.GetMethodBody();
            if (mb == null)
                throw new NullReferenceException();
            var il = mb.GetILAsByteArray();
            if (il == null)
                throw new NullReferenceException();

            var mr = new MethodRecord(mb.MaxStackSize, mb.InitLocals, mthd.IsStatic, mthd.IsConstructor, name);
            var instructions = new ILStream(il);

            var mpi = new MethodParserInternal(this);
            mpi.Parse(instructions, res_mthd.Module);

            mr.Tokens = mpi.Tokens.ToArray();
            mr.Strings = mpi.StringTable.ToArray();
            Records.MethodRecords.Add(mr);
            Records.MethodNames.Add(name);
        }

        class MethodParserInternal
        {
            private int TokenID = 0;
            private Stack<int> oStack = new Stack<int>();
            internal List<SSAToken> Tokens { get; set; } = new List<SSAToken>();
            internal List<string> StringTable = new List<string>();
            private ulong ConstrainedCallVirt = 0;
            private MethodParser parser;

            internal MethodParserInternal(MethodParser parent)
            {
                parser = parent;
            }

            internal void Parse(ILStream instructions, Module module)
            {
                do
                {
                    var opc = instructions.GetCurrentOpCode();
                    // Convert ilasm into easier to parse stream
                    // Recurse to methods called
                    if (new OpCode[] { OpCodes.Ldarg_0, OpCodes.Ldarg_1, OpCodes.Ldarg_2, OpCodes.Ldarg_3, OpCodes.Ldarg, OpCodes.Ldarg_S }.Contains(opc))
                    {
                        EncodedCountOpCode(instructions, opc, InstructionTypes.LdArg);
                    }
                    else if (new OpCode[] { OpCodes.Ldarga, OpCodes.Ldarga_S }.Contains(opc))
                    {
                        EncodedCountOpCode(instructions, opc, InstructionTypes.LdArga);
                    }
                    else if (new OpCode[] { OpCodes.Starg, OpCodes.Starg_S }.Contains(opc))
                    {
                        EncodedCountOpCode(instructions, opc, InstructionTypes.StArg);
                    }
                    else if (new OpCode[] { OpCodes.Conv_I, OpCodes.Conv_I1, OpCodes.Conv_I2, OpCodes.Conv_I4, OpCodes.Conv_I8, OpCodes.Conv_R4, OpCodes.Conv_R8, OpCodes.Conv_U1, OpCodes.Conv_U2, OpCodes.Conv_U4, OpCodes.Conv_U8, OpCodes.Conv_U, OpCodes.Conv_R_Un }.Contains(opc))
                    {
                        ConvOpcode(instructions, opc, InstructionTypes.Convert);
                    }
                    else if (new OpCode[] { OpCodes.Conv_Ovf_I, OpCodes.Conv_Ovf_I1, OpCodes.Conv_Ovf_I2, OpCodes.Conv_Ovf_I4, OpCodes.Conv_Ovf_I8, OpCodes.Conv_Ovf_U1, OpCodes.Conv_Ovf_U2, OpCodes.Conv_Ovf_U4, OpCodes.Conv_Ovf_U8, OpCodes.Conv_Ovf_U }.Contains(opc))
                    {
                        ConvOpcode(instructions, opc, InstructionTypes.ConvertCheckOverflow);
                    }
                    else if (opc == OpCodes.Nop)
                    {
                        NopOpCode(instructions, opc, InstructionTypes.Nop);
                    }
                    else if (opc == OpCodes.Dup)
                    {
                        DupOpCode(instructions, opc, InstructionTypes.Dup);
                    }
                    else if (opc == OpCodes.Pop)
                    {
                        PopOpCode(instructions, opc, InstructionTypes.Pop);
                    }
                    else if (opc == OpCodes.Throw)
                    {
                        PopOpCode(instructions, opc, InstructionTypes.Throw);
                    }
                    else if (opc == OpCodes.Mul)
                    {
                        DualParamMathOpCode(instructions, opc, InstructionTypes.Multiply);
                    }
                    else if (opc == OpCodes.Mul_Ovf)
                    {
                        DualParamMathOpCode(instructions, opc, InstructionTypes.MultiplyCheckOverflow);
                    }
                    else if (opc == OpCodes.Mul_Ovf_Un)
                    {
                        DualParamMathOpCode(instructions, opc, InstructionTypes.UMultiplyCheckOverflow);
                    }
                    else if (opc == OpCodes.Div)
                    {
                        DualParamMathOpCode(instructions, opc, InstructionTypes.Divide);
                    }
                    else if (opc == OpCodes.Div_Un)
                    {
                        DualParamMathOpCode(instructions, opc, InstructionTypes.UDivide);
                    }
                    else if (opc == OpCodes.Add)
                    {
                        DualParamMathOpCode(instructions, opc, InstructionTypes.Add);
                    }
                    else if (opc == OpCodes.Add_Ovf)
                    {
                        DualParamMathOpCode(instructions, opc, InstructionTypes.AddCheckOverflow);
                    }
                    else if (opc == OpCodes.Add_Ovf_Un)
                    {
                        DualParamMathOpCode(instructions, opc, InstructionTypes.UAddCheckOverflow);
                    }
                    else if (opc == OpCodes.Sub)
                    {
                        DualParamMathOpCode(instructions, opc, InstructionTypes.Subtract);
                    }
                    else if (opc == OpCodes.Sub_Ovf)
                    {
                        DualParamMathOpCode(instructions, opc, InstructionTypes.SubtractCheckOverflow);
                    }
                    else if (opc == OpCodes.Sub_Ovf_Un)
                    {
                        DualParamMathOpCode(instructions, opc, InstructionTypes.USubtractCheckOverflow);
                    }
                    else if (opc == OpCodes.Rem)
                    {
                        DualParamMathOpCode(instructions, opc, InstructionTypes.Rem);
                    }
                    else if (opc == OpCodes.Rem_Un)
                    {
                        DualParamMathOpCode(instructions, opc, InstructionTypes.URem);
                    }
                    else if (opc == OpCodes.And)
                    {
                        DualParamMathOpCode(instructions, opc, InstructionTypes.And);
                    }
                    else if (opc == OpCodes.Or)
                    {
                        DualParamMathOpCode(instructions, opc, InstructionTypes.Or);
                    }
                    else if (opc == OpCodes.Xor)
                    {
                        DualParamMathOpCode(instructions, opc, InstructionTypes.Xor);
                    }
                    else if (opc == OpCodes.Shl)
                    {
                        DualParamMathOpCode(instructions, opc, InstructionTypes.Shl);
                    }
                    else if (opc == OpCodes.Shr)
                    {
                        DualParamMathOpCode(instructions, opc, InstructionTypes.Shr);
                    }
                    else if (opc == OpCodes.Shr_Un)
                    {
                        DualParamMathOpCode(instructions, opc, InstructionTypes.ShrUn);
                    }
                    else if (opc == OpCodes.Neg)
                    {
                        DualParamMathOpCode(instructions, opc, InstructionTypes.Neg);
                    }
                    else if (opc == OpCodes.Not)
                    {
                        DualParamMathOpCode(instructions, opc, InstructionTypes.Not);
                    }
                    else if (opc == OpCodes.Ldstr)
                    {
                        LdStrOpCode(instructions, opc, InstructionTypes.LdStr, module);
                    }
                    else if (opc == OpCodes.Ldnull)
                    {
                        LdnullOpCode(instructions, opc, InstructionTypes.LdNull);
                    }
                    else if (new OpCode[] { OpCodes.Stloc_0, OpCodes.Stloc_1, OpCodes.Stloc_2, OpCodes.Stloc_3, OpCodes.Stloc_S, OpCodes.Stloc }.Contains(opc))
                    {
                        EncodedCountOpCode(instructions, opc, InstructionTypes.StLoc);
                    }
                    else if (new OpCode[] { OpCodes.Ldloc_0, OpCodes.Ldloc_1, OpCodes.Ldloc_2, OpCodes.Ldloc_3, OpCodes.Ldloc_S, OpCodes.Ldloc }.Contains(opc))
                    {
                        EncodedCountOpCode(instructions, opc, InstructionTypes.LdLoc);
                    }
                    else if (new OpCode[] { OpCodes.Ldc_R8, OpCodes.Ldc_R4, OpCodes.Ldc_I8, OpCodes.Ldc_I4, OpCodes.Ldc_I4_0, OpCodes.Ldc_I4_1, OpCodes.Ldc_I4_2, OpCodes.Ldc_I4_3, OpCodes.Ldc_I4_4, OpCodes.Ldc_I4_5, OpCodes.Ldc_I4_6, OpCodes.Ldc_I4_7, OpCodes.Ldc_I4_8, OpCodes.Ldc_I4_M1, OpCodes.Ldc_I4_S }.Contains(opc))
                    {
                        ExtnValOpcode(instructions, opc, InstructionTypes.Ldc);
                    }
                    else if (new OpCode[] { OpCodes.Brfalse, OpCodes.Brfalse_S }.Contains(opc))
                    {
                        ConditionalBranchOpCode(instructions, opc, InstructionTypes.BrFalse);
                    }
                    else if (new OpCode[] { OpCodes.Brtrue, OpCodes.Brtrue_S }.Contains(opc))
                    {
                        ConditionalBranchOpCode(instructions, opc, InstructionTypes.BrTrue);
                    }
                    else if (new OpCode[] { OpCodes.Br, OpCodes.Br_S }.Contains(opc))
                    {
                        BranchOpCode(instructions, opc, InstructionTypes.Br);
                    }
                    else if (opc == OpCodes.Ret)
                    {
                        RetOpCode(instructions, opc, InstructionTypes.Ret);
                    }
                    else if (opc == OpCodes.Call)
                    {
                        CallOpCode(instructions, opc, InstructionTypes.Call, module);
                    }
                    else if (opc == OpCodes.Callvirt)
                    {
                        CallOpCode(instructions, opc, InstructionTypes.CallVirt, module);
                    }
                    else if (opc == OpCodes.Newobj)
                    {
                        NewobjOpCode(instructions, opc, InstructionTypes.Newobj, module);
                    }
                    else if (opc == OpCodes.Newarr)
                    {
                        NewarrOpCode(instructions, opc, InstructionTypes.Newarr, module);
                    }
                    else if (opc == OpCodes.Stsfld)
                    {
                        StsfldOpCode(instructions, opc, InstructionTypes.Stsfld);
                    }
                    else if (opc == OpCodes.Stfld)
                    {
                        StfldOpCode(instructions, opc, InstructionTypes.Stfld);
                    }
                    else if (opc == OpCodes.Ldfld)
                    {
                        LdfldOpCode(instructions, opc, InstructionTypes.Ldfld);
                    }
                    else if (opc == OpCodes.Ldsfld)
                    {
                        LdsfldOpCode(instructions, opc, InstructionTypes.Ldsfld);
                    }
                    else if (opc == OpCodes.Ldflda)
                    {
                        LdfldOpCode(instructions, opc, InstructionTypes.Ldflda);
                    }
                    else if (opc == OpCodes.Ldsflda)
                    {
                        LdsfldOpCode(instructions, opc, InstructionTypes.Ldsflda);
                    }
                    else if (opc == OpCodes.Ldelema)
                    {
                        LdelemOpCode(instructions, opc, InstructionTypes.Ldelema);
                    }
                    else if (opc == OpCodes.Ldlen)
                    {
                        LdlenOpCode(instructions, opc, InstructionTypes.Ldlen);
                    }
                    else if (opc == OpCodes.Ldelem)
                    {
                        LdelemOpCode(instructions, opc, InstructionTypes.Ldelem);
                    }
                    else if (new OpCode[] { OpCodes.Ldelem_I, OpCodes.Ldelem_I1, OpCodes.Ldelem_I2, OpCodes.Ldelem_I4, OpCodes.Ldelem_I8, OpCodes.Ldelem_R4, OpCodes.Ldelem_R8, OpCodes.Ldelem_Ref, OpCodes.Ldelem_U1, OpCodes.Ldelem_U2, OpCodes.Ldelem_U4 }.Contains(opc))
                    {
                        LdelemOpCode(instructions, opc, InstructionTypes.Ldelem);
                    }
                    else if (opc == OpCodes.Stelem)
                    {
                        StelemOpCode(instructions, opc, InstructionTypes.Stelem);
                    }
                    else if (new OpCode[] { OpCodes.Stelem_I, OpCodes.Stelem_I1, OpCodes.Stelem_I2, OpCodes.Stelem_I4, OpCodes.Stelem_I8, OpCodes.Stelem_R4, OpCodes.Stelem_R8, OpCodes.Stelem_Ref }.Contains(opc))
                    {
                        StelemOpCode(instructions, opc, InstructionTypes.Stelem);
                    }
                    else if (new OpCode[] { OpCodes.Stind_I, OpCodes.Stind_I1, OpCodes.Stind_I2, OpCodes.Stind_I4, OpCodes.Stind_I8, OpCodes.Stind_R4, OpCodes.Stind_R8, OpCodes.Stind_Ref }.Contains(opc))
                    {
                        StindOpCode(instructions, opc, InstructionTypes.Stind);
                    }
                    else if (new OpCode[] { OpCodes.Ldind_I, OpCodes.Ldind_I1, OpCodes.Ldind_I2, OpCodes.Ldind_I4, OpCodes.Ldind_I8, OpCodes.Ldind_U1, OpCodes.Ldind_U2, OpCodes.Ldind_U4, OpCodes.Ldind_R4, OpCodes.Ldind_R8, OpCodes.Ldind_Ref }.Contains(opc))
                    {
                        LdindOpCode(instructions, opc, InstructionTypes.LdInd);
                    }
                    else if (opc == OpCodes.Ceq)
                    {
                        CompareOpCode(instructions, opc, InstructionTypes.Ceq);
                    }
                    else if (opc == OpCodes.Cgt)
                    {
                        CompareOpCode(instructions, opc, InstructionTypes.Cgt);
                    }
                    else if (opc == OpCodes.Cgt_Un)
                    {
                        CompareOpCode(instructions, opc, InstructionTypes.CgtUn);
                    }
                    else if (opc == OpCodes.Clt)
                    {
                        CompareOpCode(instructions, opc, InstructionTypes.Clt);
                    }
                    else if (opc == OpCodes.Clt_Un)
                    {
                        CompareOpCode(instructions, opc, InstructionTypes.CltUn);
                    }
                    else if (opc == OpCodes.Switch)
                    {
                        SwitchOpCode(instructions, opc, InstructionTypes.Switch);
                    }
                    else if (new OpCode[] { OpCodes.Beq, OpCodes.Beq_S }.Contains(opc))
                    {
                        CompConditionalBranchOpCode(instructions, opc, InstructionTypes.Beq);
                    }
                    else if (new OpCode[] { OpCodes.Bne_Un, OpCodes.Bne_Un_S }.Contains(opc))
                    {
                        CompConditionalBranchOpCode(instructions, opc, InstructionTypes.BneUn);
                    }
                    else if (new OpCode[] { OpCodes.Bgt, OpCodes.Bgt_S }.Contains(opc))
                    {
                        CompConditionalBranchOpCode(instructions, opc, InstructionTypes.Bgt);
                    }
                    else if (new OpCode[] { OpCodes.Blt, OpCodes.Blt_S }.Contains(opc))
                    {
                        CompConditionalBranchOpCode(instructions, opc, InstructionTypes.Blt);
                    }
                    else if (new OpCode[] { OpCodes.Bgt_Un, OpCodes.Bgt_Un_S }.Contains(opc))
                    {
                        CompConditionalBranchOpCode(instructions, opc, InstructionTypes.BgtUn);
                    }
                    else if (new OpCode[] { OpCodes.Blt_Un, OpCodes.Blt_Un_S }.Contains(opc))
                    {
                        CompConditionalBranchOpCode(instructions, opc, InstructionTypes.BltUn);
                    }
                    else if (new OpCode[] { OpCodes.Bge, OpCodes.Bge_S }.Contains(opc))
                    {
                        CompConditionalBranchOpCode(instructions, opc, InstructionTypes.Bge);
                    }
                    else if (new OpCode[] { OpCodes.Ble, OpCodes.Ble_S }.Contains(opc))
                    {
                        CompConditionalBranchOpCode(instructions, opc, InstructionTypes.Ble);
                    }
                    else if (new OpCode[] { OpCodes.Bge_Un, OpCodes.Bge_Un_S }.Contains(opc))
                    {
                        CompConditionalBranchOpCode(instructions, opc, InstructionTypes.BgeUn);
                    }
                    else if (new OpCode[] { OpCodes.Ble_Un, OpCodes.Ble_Un_S }.Contains(opc))
                    {
                        CompConditionalBranchOpCode(instructions, opc, InstructionTypes.BleUn);
                    }
                    else if (new OpCode[] { OpCodes.Leave, OpCodes.Leave_S }.Contains(opc))
                    {
                        LeaveOpCode(instructions, opc, InstructionTypes.Leave);
                    }
                    else if (opc == OpCodes.Endfinally)
                    {
                        EndFinallyOpCode(instructions, opc, InstructionTypes.EndFinally);
                    }
                    else if (opc == OpCodes.Ldtoken)
                    {
                        LdTokenOpCode(instructions, opc, InstructionTypes.Ldtoken);
                    }
                    else if (new OpCode[] { OpCodes.Ldloca, OpCodes.Ldloca_S }.Contains(opc))
                    {
                        LdLocaOpCode(instructions, opc, InstructionTypes.LdLoca);
                    }
                    else if (opc == OpCodes.Localloc)
                    {
                        LocallocOpCode(instructions, opc, InstructionTypes.Localloc);
                    }
                    else if (opc == OpCodes.Ldftn)
                    {
                        LdFtnOpCode(instructions, opc, InstructionTypes.Ldftn, module);
                    }
                    else if (opc == OpCodes.Isinst)
                    {
                        IsInstOpCode(instructions, opc, InstructionTypes.IsInst);
                    }
                    else if (opc == OpCodes.Constrained)
                    {
                        //Store this token for the next instruction, guaranteed to be a callvirt, which will be updated appropriately
                        ConstrainedCallVirt = instructions.GetParameter(0);
                    }
                    else
                        throw new Exception(opc.Name);

                } while (instructions.NextInstruction());
            }

            #region OpCode Parsers
            private void ConvOpcode(ILStream instructions, OpCode opc, InstructionTypes t)
            {
                SSAToken tkn = new SSAToken()
                {
                    Parameters = new int[] { oStack.Pop() },
                    Operation = t,
                    InstructionOffset = instructions.CurrentOffset,
                    ID = TokenID++,
                };

                var parts = opc.Name.Split('.');
                if (parts.Length == 2)
                {
                    switch (parts[1])
                    {
                        case "i":
                            tkn.Constants = new ulong[] { (ulong)OperandTypes.I };
                            break;
                        case "i1":
                            tkn.Constants = new ulong[] { (ulong)OperandTypes.I1 };
                            break;
                        case "i2":
                            tkn.Constants = new ulong[] { (ulong)OperandTypes.I2 };
                            break;
                        case "i4":
                            tkn.Constants = new ulong[] { (ulong)OperandTypes.I4 };
                            break;
                        case "i8":
                            tkn.Constants = new ulong[] { (ulong)OperandTypes.I8 };
                            break;
                        case "u":
                            tkn.Constants = new ulong[] { (ulong)OperandTypes.U };
                            break;
                        case "u1":
                            tkn.Constants = new ulong[] { (ulong)OperandTypes.U1 };
                            break;
                        case "u2":
                            tkn.Constants = new ulong[] { (ulong)OperandTypes.U2 };
                            break;
                        case "u4":
                            tkn.Constants = new ulong[] { (ulong)OperandTypes.U4 };
                            break;
                        case "u8":
                            tkn.Constants = new ulong[] { (ulong)OperandTypes.U8 };
                            break;
                        case "r4":
                            tkn.Constants = new ulong[] { (ulong)OperandTypes.R4 };
                            break;
                        case "r8":
                            tkn.Constants = new ulong[] { (ulong)OperandTypes.R8 };
                            break;
                    }
                }
                else
                {
                    //conv.r.un
                    tkn.Constants = new ulong[] { (ulong)OperandTypes.R_U };
                }

                Tokens.Add(tkn);
                oStack.Push(tkn.ID);
            }

            private void ExtnValOpcode(ILStream instructions, OpCode opc, InstructionTypes t)
            {
                var tkn = new SSAToken()
                {
                    Operation = t,
                    ID = TokenID++,
                    InstructionOffset = instructions.CurrentOffset
                };

                var parts = opc.Name.Split('.');
                if (parts.Length == 2)
                {
                    switch (parts[1])
                    {
                        case "i4":
                            tkn.Constants = new ulong[] { (ulong)OperandTypes.I4, instructions.GetParameter(0) };
                            break;
                        case "i8":
                            tkn.Constants = new ulong[] { (ulong)OperandTypes.I8, instructions.GetParameter(0) };
                            break;
                        case "r4":
                            tkn.Constants = new ulong[] { (ulong)OperandTypes.R4, instructions.GetParameter(0) };
                            break;
                        case "r8":
                            tkn.Constants = new ulong[] { (ulong)OperandTypes.R8, instructions.GetParameter(0) };
                            break;
                    }
                }
                else if (parts.Length == 3)
                {
                    if (ulong.TryParse(parts[2], out ulong val))
                    {
                        tkn.Constants = new ulong[] { (ulong)OperandTypes.I4, val };
                    }
                    else if (parts[2] == "m1")
                    {
                        tkn.Constants = new ulong[] { (ulong)OperandTypes.I4, unchecked((uint)-1) };
                    }
                    else if (parts[2] == "s")
                    {
                        tkn.Constants = new ulong[] { (ulong)OperandTypes.I4, unchecked((ulong)(sbyte)instructions.GetParameter(0)) };
                    }
                }
                Tokens.Add(tkn);
                oStack.Push(tkn.ID);
            }

            private void EncodedCountOpCode(ILStream instructions, OpCode opc, InstructionTypes t)
            {
                var p = opc.Name.Split('.')[1];
                SSAToken tkn = new SSAToken()
                {
                    Parameters = null,
                    Operation = t,
                    ID = TokenID++,
                    InstructionOffset = instructions.CurrentOffset
                };

                if (ulong.TryParse(p, out ulong constant))
                {
                    tkn.Constants = new ulong[] { constant };
                }
                else
                {
                    tkn.Constants = new ulong[] { instructions.GetParameter(0) };
                }

                if (t == InstructionTypes.StLoc | t == InstructionTypes.StArg)
                {
                    tkn.Parameters = new int[] { oStack.Pop() };
                }
                else
                {
                    oStack.Push(tkn.ID);
                }
                Tokens.Add(tkn);
            }

            private void DualParamMathOpCode(ILStream instructions, OpCode opc, InstructionTypes t)
            {
                SSAToken tkn = new SSAToken()
                {
                    Parameters = new int[] { oStack.Pop(), oStack.Pop() },
                    Operation = t,
                    ID = TokenID++,
                    InstructionOffset = instructions.CurrentOffset
                };

                Tokens.Add(tkn);
                oStack.Push(tkn.ID);
            }

            private void ConditionalBranchOpCode(ILStream instructions, OpCode opc, InstructionTypes t)
            {
                long off = unchecked((long)instructions.GetParameter(0));
                if (instructions.GetParameterSize(0) == 1)
                {
                    off = unchecked((sbyte)instructions.GetParameter(0));
                }
                if (instructions.GetParameterSize(0) == 2)
                {
                    off = unchecked((short)instructions.GetParameter(0));
                }
                if (instructions.GetParameterSize(0) == 4)
                {
                    off = unchecked((int)instructions.GetParameter(0));
                }

                var tkn = new SSAToken()
                {
                    Operation = t,
                    Parameters = new int[] { oStack.Pop() },
                    Constants = new ulong[] { (ulong)unchecked(off + instructions.CurrentOffset + opc.Size + instructions.GetParameterSize(0)) },
                    ID = TokenID++,
                    InstructionOffset = instructions.CurrentOffset
                };
                Tokens.Add(tkn);
            }

            private void CompConditionalBranchOpCode(ILStream instructions, OpCode opc, InstructionTypes t)
            {
                long off = unchecked((long)instructions.GetParameter(0));
                if (instructions.GetParameterSize(0) == 1)
                {
                    off = unchecked((sbyte)instructions.GetParameter(0));
                }
                if (instructions.GetParameterSize(0) == 2)
                {
                    off = unchecked((short)instructions.GetParameter(0));
                }
                if (instructions.GetParameterSize(0) == 4)
                {
                    off = unchecked((int)instructions.GetParameter(0));
                }

                var tkn = new SSAToken()
                {
                    Operation = t,
                    Parameters = new int[] { oStack.Pop(), oStack.Pop() },
                    Constants = new ulong[] { (ulong)unchecked(off + instructions.CurrentOffset + opc.Size + instructions.GetParameterSize(0)) },
                    ID = TokenID++,
                    InstructionOffset = instructions.CurrentOffset
                };
                Tokens.Add(tkn);
            }

            private void BranchOpCode(ILStream instructions, OpCode opc, InstructionTypes t)
            {
                var tkn = new SSAToken()
                {
                    Operation = t,
                    Parameters = null,
                    InstructionOffset = instructions.CurrentOffset,
                    ID = TokenID++,
                    Constants = new ulong[] { (ulong)unchecked((long)instructions.GetParameter(0) + instructions.GetParameterSize(0) + instructions.CurrentOffset + opc.Size) }
                };
                Tokens.Add(tkn);
            }

            private void LeaveOpCode(ILStream instructions, OpCode opc, InstructionTypes t)
            {
                var tkn = new SSAToken()
                {
                    Operation = t,
                    Parameters = null,
                    InstructionOffset = instructions.CurrentOffset,
                    ID = TokenID++,
                    Constants = new ulong[] { (ulong)unchecked((long)instructions.GetParameter(0) + instructions.GetParameterSize(0) + instructions.CurrentOffset + opc.Size) }
                };
                oStack.Clear();
                Tokens.Add(tkn);
            }

            private void EndFinallyOpCode(ILStream instructions, OpCode opc, InstructionTypes t)
            {
                var tkn = new SSAToken()
                {
                    Operation = t,
                    Parameters = null,
                    Constants = null,
                    InstructionOffset = instructions.CurrentOffset,
                    ID = TokenID++,
                };
                oStack.Clear();
                Tokens.Add(tkn);
            }

            private void RetOpCode(ILStream instructions, OpCode opc, InstructionTypes t)
            {
                var tkn = new SSAToken()
                {
                    Operation = t,
                    Parameters = null,
                    Constants = null,
                    InstructionOffset = instructions.CurrentOffset,
                    ID = TokenID++,
                };

                if (oStack.Count > 0)
                {
                    tkn.Parameters = new int[] { oStack.Pop() };
                }

                if (oStack.Count != 0)
                    throw new Exception("Incorrect CIL! Evaluation stack should be empty on ret instruction.");
                Tokens.Add(tkn);
            }

            private void LdStrOpCode(ILStream instructions, OpCode opc, InstructionTypes t, Module module)
            {
                var str = module.ResolveString((int)instructions.GetParameter(0));

                var tkn = new SSAToken()
                {
                    Operation = t,
                    Parameters = null,
                    Constants = new ulong[] { (ulong)StringTable.Count },
                    InstructionOffset = instructions.CurrentOffset,
                    String = str,
                    ID = TokenID++,
                };
                oStack.Push(tkn.ID);
                Tokens.Add(tkn);
            }

            private void CallOpCode(ILStream instructions, OpCode opc, InstructionTypes call, Module module)
            {
                Type retType = null;
                ParameterInfo[] @params = null;

                var mbase = module.ResolveMethod((int)instructions.GetParameter(0));
                @params = mbase.GetParameters();

                if (mbase is MethodInfo)
                {
                    var mbase_mi = (MethodInfo)mbase;
                    retType = mbase_mi.ReturnType;
                    if (parser.typeResolver.IsNative(mbase_mi))
                        parser.Records.NativeMethods[NameMangler.Mangle(mbase_mi)] = parser.typeResolver.GetNativeName(mbase_mi);
                    else
                        parser.Parse(mbase_mi);
                }
                else if (mbase is ConstructorInfo)
                {
                    var mbase_ci = (ConstructorInfo)mbase;
                    retType = typeof(void);
                    parser.Parse(mbase_ci);
                }

                var intP = new int[@params.Length + (mbase.IsStatic ? 0 : 1)];
                for (int i = 0; i < intP.Length; i++)
                {
                    intP[intP.Length - 1 - i] = oStack.Pop();
                }

                if (call == InstructionTypes.CallVirt && ConstrainedCallVirt != 0)
                {
                    call = InstructionTypes.CallVirtConstrained;
                }

                var tkn = new SSAToken()
                {
                    Operation = call,
                    Parameters = intP,
                    InstructionOffset = instructions.CurrentOffset,
                    String = (mbase is MethodInfo) ? NameMangler.Mangle((MethodInfo)mbase) : NameMangler.Mangle((ConstructorInfo)mbase),
                    ID = TokenID++,
                };

                if (call == InstructionTypes.CallVirtConstrained)
                {
                    tkn.Constants = new ulong[] { instructions.GetParameter(0), ConstrainedCallVirt };
                    ConstrainedCallVirt = 0;
                }
                else
                {
                    tkn.Constants = new ulong[] { instructions.GetParameter(0) };
                }

                if (retType != typeof(void))
                {
                    oStack.Push(tkn.ID);
                }
                Tokens.Add(tkn);
            }

            private void LdLocaOpCode(ILStream instructions, OpCode opc, InstructionTypes call)
            {
                var tkn = new SSAToken()
                {
                    Operation = call,
                    Parameters = null,
                    InstructionOffset = instructions.CurrentOffset,
                    Constants = new ulong[] { instructions.GetParameter(0) },
                    ID = TokenID++,
                };
                oStack.Push(tkn.ID);
                Tokens.Add(tkn);
            }

            private void NewobjOpCode(ILStream instructions, OpCode opc, InstructionTypes newobj, Module module)
            {
                //TODO: Detect generic type creations and pass them back for resolution

                //allocate the object, call the constructor
                var mthd = (ConstructorInfo)module.ResolveMethod((int)instructions.GetParameter(0));
                var @params = mthd.GetParameters();

                parser.Parse(mthd);

                var intP = new int[@params.Length];
                for (int i = 0; i < intP.Length; i++)
                {
                    intP[intP.Length - 1 - i] = oStack.Pop();
                }

                var tkn = new SSAToken()
                {
                    Operation = newobj,
                    Parameters = intP,
                    InstructionOffset = instructions.CurrentOffset,
                    String = NameMangler.Mangle(mthd),
                    ID = TokenID++,
                };

                oStack.Push(tkn.ID);
                Tokens.Add(tkn);
            }

            private void NewarrOpCode(ILStream instructions, OpCode opc, InstructionTypes newobj, Module module)
            {
                //allocate the array
                var type = module.ResolveType((int)instructions.GetParameter(0));
                parser.typeParser.Parse(type);

                var tkn = new SSAToken()
                {
                    Operation = newobj,
                    Parameters = new int[] { oStack.Pop() },
                    InstructionOffset = instructions.CurrentOffset,
                    String = NameMangler.Mangle(type),
                    ID = TokenID++,
                };

                oStack.Push(tkn.ID);
                Tokens.Add(tkn);
            }

            private void StsfldOpCode(ILStream instructions, OpCode opc, InstructionTypes newobj)
            {
                var tkn = new SSAToken()
                {
                    Operation = newobj,
                    Parameters = new int[] { oStack.Pop() },
                    InstructionOffset = instructions.CurrentOffset,
                    Constants = new ulong[] { instructions.GetParameter(0) },
                    ID = TokenID++,
                };
                Tokens.Add(tkn);
            }

            private void StfldOpCode(ILStream instructions, OpCode opc, InstructionTypes newobj)
            {
                var tkn = new SSAToken()
                {
                    Operation = newobj,
                    Parameters = new int[] { oStack.Pop(), oStack.Pop() },
                    InstructionOffset = instructions.CurrentOffset,
                    Constants = new ulong[] { instructions.GetParameter(0) },
                    ID = TokenID++,
                };
                Tokens.Add(tkn);
            }

            private void StelemOpCode(ILStream instructions, OpCode opc, InstructionTypes stelem)
            {
                var tkn = new SSAToken()
                {
                    Operation = stelem,
                    Parameters = new int[] { oStack.Pop(), oStack.Pop(), oStack.Pop() },
                    InstructionOffset = instructions.CurrentOffset,
                    ID = TokenID++,
                    Constants = null
                };

                var parts = opc.Name.Split('.');
                if (parts.Length == 1)
                {
                    tkn.Constants = new ulong[] { (ulong)OperandTypes.Object, instructions.GetParameter(0) };
                }
                else if (parts.Length == 2)
                {
                    switch (parts[1])
                    {
                        case "i":
                            tkn.Constants = new ulong[] { (ulong)OperandTypes.I };
                            break;
                        case "i1":
                            tkn.Constants = new ulong[] { (ulong)OperandTypes.I1 };
                            break;
                        case "i2":
                            tkn.Constants = new ulong[] { (ulong)OperandTypes.I2 };
                            break;
                        case "i4":
                            tkn.Constants = new ulong[] { (ulong)OperandTypes.I4 };
                            break;
                        case "i8":
                            tkn.Constants = new ulong[] { (ulong)OperandTypes.I8 };
                            break;
                        case "u":
                            tkn.Constants = new ulong[] { (ulong)OperandTypes.U };
                            break;
                        case "u1":
                            tkn.Constants = new ulong[] { (ulong)OperandTypes.U1 };
                            break;
                        case "u2":
                            tkn.Constants = new ulong[] { (ulong)OperandTypes.U2 };
                            break;
                        case "u4":
                            tkn.Constants = new ulong[] { (ulong)OperandTypes.U4 };
                            break;
                        case "u8":
                            tkn.Constants = new ulong[] { (ulong)OperandTypes.U8 };
                            break;
                        case "r4":
                            tkn.Constants = new ulong[] { (ulong)OperandTypes.R4 };
                            break;
                        case "r8":
                            tkn.Constants = new ulong[] { (ulong)OperandTypes.R8 };
                            break;
                        case "ref":
                            tkn.Constants = new ulong[] { (ulong)OperandTypes.Object };
                            break;
                    }
                }
                Tokens.Add(tkn);
            }

            private void StindOpCode(ILStream instructions, OpCode opc, InstructionTypes stelem)
            {
                var tkn = new SSAToken()
                {
                    Operation = stelem,
                    Parameters = new int[] { oStack.Pop(), oStack.Pop() },
                    InstructionOffset = instructions.CurrentOffset,
                    ID = TokenID++,
                    Constants = null
                };

                var parts = opc.Name.Split('.');

                switch (parts[1])
                {
                    case "i":
                        tkn.Constants = new ulong[] { (ulong)OperandTypes.I };
                        break;
                    case "i1":
                        tkn.Constants = new ulong[] { (ulong)OperandTypes.I1 };
                        break;
                    case "i2":
                        tkn.Constants = new ulong[] { (ulong)OperandTypes.I2 };
                        break;
                    case "i4":
                        tkn.Constants = new ulong[] { (ulong)OperandTypes.I4 };
                        break;
                    case "i8":
                        tkn.Constants = new ulong[] { (ulong)OperandTypes.I8 };
                        break;
                    case "r4":
                        tkn.Constants = new ulong[] { (ulong)OperandTypes.R4 };
                        break;
                    case "r8":
                        tkn.Constants = new ulong[] { (ulong)OperandTypes.R8 };
                        break;
                    case "ref":
                        tkn.Constants = new ulong[] { (ulong)OperandTypes.Object };
                        break;
                }
                Tokens.Add(tkn);
            }

            private void LdindOpCode(ILStream instructions, OpCode opc, InstructionTypes stelem)
            {
                var tkn = new SSAToken()
                {
                    Operation = stelem,
                    Parameters = new int[] { oStack.Pop() },
                    InstructionOffset = instructions.CurrentOffset,
                    ID = TokenID++,
                    Constants = null
                };

                var parts = opc.Name.Split('.');

                switch (parts[1])
                {
                    case "i":
                        tkn.Constants = new ulong[] { (ulong)OperandTypes.I };
                        break;
                    case "i1":
                        tkn.Constants = new ulong[] { (ulong)OperandTypes.I1 };
                        break;
                    case "i2":
                        tkn.Constants = new ulong[] { (ulong)OperandTypes.I2 };
                        break;
                    case "i4":
                        tkn.Constants = new ulong[] { (ulong)OperandTypes.I4 };
                        break;
                    case "u1":
                        tkn.Constants = new ulong[] { (ulong)OperandTypes.U1 };
                        break;
                    case "u2":
                        tkn.Constants = new ulong[] { (ulong)OperandTypes.U2 };
                        break;
                    case "u4":
                        tkn.Constants = new ulong[] { (ulong)OperandTypes.U4 };
                        break;
                    case "i8":
                        tkn.Constants = new ulong[] { (ulong)OperandTypes.I8 };
                        break;
                    case "r4":
                        tkn.Constants = new ulong[] { (ulong)OperandTypes.R4 };
                        break;
                    case "r8":
                        tkn.Constants = new ulong[] { (ulong)OperandTypes.R8 };
                        break;
                    case "ref":
                        tkn.Constants = new ulong[] { (ulong)OperandTypes.Object };
                        break;
                }

                oStack.Push(tkn.ID);
                Tokens.Add(tkn);
            }

            private void LdfldOpCode(ILStream instructions, OpCode opc, InstructionTypes newobj)
            {
                var tkn = new SSAToken()
                {
                    Operation = newobj,
                    Parameters = new int[] { oStack.Pop() },
                    InstructionOffset = instructions.CurrentOffset,
                    Constants = new ulong[] { instructions.GetParameter(0) },
                    ID = TokenID++,
                };
                oStack.Push(tkn.ID);
                Tokens.Add(tkn);
            }

            private void LdsfldOpCode(ILStream instructions, OpCode opc, InstructionTypes newobj)
            {
                var tkn = new SSAToken()
                {
                    Operation = newobj,
                    Parameters = null,
                    InstructionOffset = instructions.CurrentOffset,
                    Constants = new ulong[] { instructions.GetParameter(0) },
                    ID = TokenID++,
                };
                oStack.Push(tkn.ID);
                Tokens.Add(tkn);
            }

            private void LdnullOpCode(ILStream instructions, OpCode opc, InstructionTypes newobj)
            {
                var tkn = new SSAToken()
                {
                    Operation = newobj,
                    Parameters = null,
                    InstructionOffset = instructions.CurrentOffset,
                    ID = TokenID++,
                    Constants = null,
                };
                oStack.Push(tkn.ID);
                Tokens.Add(tkn);
            }

            private void LdelemOpCode(ILStream instructions, OpCode opc, InstructionTypes op)
            {
                var tkn = new SSAToken()
                {
                    Operation = op,
                    Parameters = new int[] { oStack.Pop(), oStack.Pop() },
                    InstructionOffset = instructions.CurrentOffset,
                    ID = TokenID++,
                    Constants = null
                };

                var parts = opc.Name.Split('.');
                if (parts.Length == 1)
                {
                    tkn.Constants = new ulong[] { (ulong)OperandTypes.Object, instructions.GetParameter(0) };
                }
                else if (parts.Length == 2)
                {
                    switch (parts[1])
                    {
                        case "i":
                            tkn.Constants = new ulong[] { (ulong)OperandTypes.I };
                            break;
                        case "i1":
                            tkn.Constants = new ulong[] { (ulong)OperandTypes.I1 };
                            break;
                        case "i2":
                            tkn.Constants = new ulong[] { (ulong)OperandTypes.I2 };
                            break;
                        case "i4":
                            tkn.Constants = new ulong[] { (ulong)OperandTypes.I4 };
                            break;
                        case "i8":
                            tkn.Constants = new ulong[] { (ulong)OperandTypes.I8 };
                            break;
                        case "u":
                            tkn.Constants = new ulong[] { (ulong)OperandTypes.U };
                            break;
                        case "u1":
                            tkn.Constants = new ulong[] { (ulong)OperandTypes.U1 };
                            break;
                        case "u2":
                            tkn.Constants = new ulong[] { (ulong)OperandTypes.U2 };
                            break;
                        case "u4":
                            tkn.Constants = new ulong[] { (ulong)OperandTypes.U4 };
                            break;
                        case "u8":
                            tkn.Constants = new ulong[] { (ulong)OperandTypes.U8 };
                            break;
                        case "r4":
                            tkn.Constants = new ulong[] { (ulong)OperandTypes.R4 };
                            break;
                        case "r8":
                            tkn.Constants = new ulong[] { (ulong)OperandTypes.R8 };
                            break;
                        case "ref":
                            tkn.Constants = new ulong[] { (ulong)OperandTypes.Object };
                            break;
                    }
                }

                oStack.Push(tkn.ID);
                Tokens.Add(tkn);
            }

            private void CompareOpCode(ILStream instructions, OpCode opc, InstructionTypes op)
            {
                var tkn = new SSAToken()
                {
                    Operation = op,
                    Parameters = new int[] { oStack.Pop(), oStack.Pop() },
                    InstructionOffset = instructions.CurrentOffset,
                    ID = TokenID++,
                    Constants = null
                };
                oStack.Push(tkn.ID);
                Tokens.Add(tkn);
            }

            private void PopOpCode(ILStream instructions, OpCode opc, InstructionTypes op)
            {
                var tkn = new SSAToken()
                {
                    Operation = op,
                    Parameters = new int[] { oStack.Pop() },
                    InstructionOffset = instructions.CurrentOffset,
                    ID = TokenID++,
                    Constants = null
                };
                Tokens.Add(tkn);
            }

            private void DupOpCode(ILStream instructions, OpCode opc, InstructionTypes op)
            {
                var tkn = new SSAToken()
                {
                    Operation = op,
                    Parameters = new int[] { oStack.Pop() },
                    InstructionOffset = instructions.CurrentOffset,
                    ID = TokenID++,
                    Constants = null
                };
                oStack.Push(tkn.ID);
                oStack.Push(tkn.ID);
                Tokens.Add(tkn);
            }

            private void NopOpCode(ILStream instructions, OpCode opc, InstructionTypes op)
            {
                var tkn = new SSAToken()
                {
                    Operation = op,
                    Parameters = null,
                    InstructionOffset = instructions.CurrentOffset,
                    ID = TokenID++,
                    Constants = null
                };
                Tokens.Add(tkn);
            }

            private void LdlenOpCode(ILStream instructions, OpCode opc, InstructionTypes ldlen)
            {
                var tkn = new SSAToken()
                {
                    Operation = ldlen,
                    Parameters = new int[] { oStack.Pop() },
                    InstructionOffset = instructions.CurrentOffset,
                    ID = TokenID++,
                    Constants = null
                };
                oStack.Push(tkn.ID);
                Tokens.Add(tkn);
            }

            private void SwitchOpCode(ILStream instructions, OpCode opc, InstructionTypes @switch)
            {
                var tkn = new SSAToken()
                {
                    Operation = @switch,
                    InstructionOffset = instructions.CurrentOffset,
                    Parameters = new int[] { oStack.Pop() },
                    ID = TokenID++,
                };

                ulong cnt = instructions.GetParameter(0);
                ulong[] parts = new ulong[cnt];
                for (uint i = 0; i < parts.Length; i++)
                {
                    parts[i] = instructions.GetParameter(1 + i);
                }
                tkn.Constants = parts;
                Tokens.Add(tkn);

            }

            private void LdTokenOpCode(ILStream instructions, OpCode opc, InstructionTypes types)
            {
                var tkn = new SSAToken()
                {
                    Operation = types,
                    Parameters = null,
                    InstructionOffset = instructions.CurrentOffset,
                    Constants = new ulong[] { instructions.GetParameter(0) },
                    ID = TokenID++,
                };
                oStack.Push(tkn.ID);
                Tokens.Add(tkn);
            }

            private void CpBlkOpCode(ILStream instructions, OpCode opc, InstructionTypes types)
            {
                var tkn = new SSAToken()
                {
                    Operation = types,
                    Parameters = new int[] { oStack.Pop(), oStack.Pop(), oStack.Pop() },
                    InstructionOffset = instructions.CurrentOffset,
                    ID = TokenID++,
                };
                Tokens.Add(tkn);
            }

            private void EndFilterOpCode(ILStream instructions, OpCode opc, InstructionTypes types)
            {
                var tkn = new SSAToken()
                {
                    Operation = types,
                    Parameters = new int[] { oStack.Pop() },
                    InstructionOffset = instructions.CurrentOffset,
                    ID = TokenID++,
                };
                Tokens.Add(tkn);
            }

            private void InitBlkOpCode(ILStream instructions, OpCode opc, InstructionTypes types)
            {
                var tkn = new SSAToken()
                {
                    Operation = types,
                    Parameters = new int[] { oStack.Pop(), oStack.Pop(), oStack.Pop() },
                    InstructionOffset = instructions.CurrentOffset,
                    ID = TokenID++,
                };
                Tokens.Add(tkn);
            }

            private void JmpOpCode(ILStream instructions, OpCode opc, InstructionTypes types)
            {
                var tkn = new SSAToken()
                {
                    Operation = types,
                    Parameters = null,
                    InstructionOffset = instructions.CurrentOffset,
                    Constants = new ulong[] { instructions.GetParameter(0) },
                    ID = TokenID++,
                };
                Tokens.Add(tkn);
            }

            private void LocallocOpCode(ILStream instructions, OpCode opc, InstructionTypes types)
            {
                var tkn = new SSAToken()
                {
                    Operation = types,
                    Parameters = new int[] { oStack.Pop() },
                    InstructionOffset = instructions.CurrentOffset,
                    ID = TokenID++,
                };
                oStack.Push(tkn.ID);
                Tokens.Add(tkn);
            }

            private void LdFtnOpCode(ILStream instructions, OpCode opc, InstructionTypes types, Module module)
            {
                var mbase = module.ResolveMethod((int)instructions.GetParameter(0));
                var name = "";

                if (mbase is MethodInfo)
                {
                    var mbase_mi = (MethodInfo)mbase;
                    if (parser.typeResolver.IsNative(mbase_mi))
                        parser.Records.NativeMethods[NameMangler.Mangle(mbase_mi)] = parser.typeResolver.GetNativeName(mbase_mi);
                    else
                        parser.Parse(mbase_mi);
                    name = NameMangler.Mangle(mbase_mi);
                }
                else if (mbase is ConstructorInfo)
                {
                    var mbase_ci = (ConstructorInfo)mbase;
                    parser.Parse(mbase_ci);
                    name = NameMangler.Mangle(mbase_ci);
                }

                var tkn = new SSAToken()
                {
                    Operation = types,
                    Parameters = null,
                    InstructionOffset = instructions.CurrentOffset,
                    String = name,
                    ID = TokenID++,
                };
                oStack.Push(tkn.ID);
                Tokens.Add(tkn);
            }

            private void IsInstOpCode(ILStream instructions, OpCode opc, InstructionTypes types)
            {
                var tkn = new SSAToken()
                {
                    Operation = types,
                    Parameters = new int[] { oStack.Pop() },
                    InstructionOffset = instructions.CurrentOffset,
                    ID = TokenID++,
                };
                oStack.Push(tkn.ID);
                Tokens.Add(tkn);
            }
            #endregion
        }
    }
}
