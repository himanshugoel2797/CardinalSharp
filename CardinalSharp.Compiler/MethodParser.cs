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
        string all_code = "";

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
            var param_types = mthd.GetParameters().Select(p => p.ParameterType).ToArray();
            if (!mthd.IsStatic)
                param_types = param_types.Prepend(mthd.ReflectedType).ToArray();
            var local_types = mb.LocalVariables.Select(p => p.LocalType).ToArray();
            var code = mpi.Parse(instructions, res_mthd.Module, typeResolver.GetTypeRefSize(param_types), typeResolver.GetTypeRefName(param_types), typeResolver.GetTypeRefSize(mthd.ReturnType), typeResolver.GetTypeRefName(mthd.ReturnType), typeResolver.GetTypeRefSize(local_types), typeResolver.GetTypeRefName(local_types), name, mb.MaxStackSize);
            all_code += code;

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

            //TODO: Generate conversion methods for every type this type can convert to (including interfaces)
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
            var param_types = mthd.GetParameters().Select(p => p.ParameterType).ToArray();
            if (!mthd.IsStatic)
                param_types = param_types.Prepend(mthd.ReflectedType).ToArray();
            var local_types = mb.LocalVariables.Select(p => p.LocalType).ToArray();
            var code = mpi.Parse(instructions, res_mthd.Module, typeResolver.GetTypeRefSize(param_types), typeResolver.GetTypeRefName(param_types), typeResolver.GetTypeRefSize(typeof(void)), typeResolver.GetTypeRefName(typeof(void)), typeResolver.GetTypeRefSize(local_types), typeResolver.GetTypeRefName(local_types), name, mb.MaxStackSize);
            all_code += code;

            mr.Tokens = mpi.Tokens.ToArray();
            mr.Strings = mpi.StringTable.ToArray();
            Records.MethodRecords.Add(mr);
            Records.MethodNames.Add(name);
        }

        class MethodParserInternal
        {
            class StackEntry
            {
                public int size;
                public string type_name;
            }

            private Stack<StackEntry> oStack = new Stack<StackEntry>();
            internal List<SSAToken> Tokens { get; set; } = new List<SSAToken>();
            internal List<string> StringTable = new List<string>();
            private MethodParser parser;

            internal MethodParserInternal(MethodParser parent)
            {
                parser = parent;
            }

            internal string Parse(ILStream instructions, Module module, int[] param_sizes, string[] param_type_names, int ret_size, string ret_type, int[] local_sizes, string[] local_type_names, string name, int eval_stack_size)
            {
                string code = "";
                code += $"{ret_type} {name}(";
                for (int i = 0; i < param_sizes.Length; i++)
                    code += $"{param_type_names[i]} arg_{i}" + (i < param_sizes.Length - 1 ? "," : "");
                code += "){ \r\n";
                code += $"evalstack_init({eval_stack_size});\r\n";

                StackEntry[] locals = new StackEntry[local_sizes.Length];
                for (int i = 0; i < local_sizes.Length; i++)
                {
                    code += $"{local_type_names[i]} lcl_{i};\r\n";
                    locals[i] = new StackEntry()
                    {
                        size = local_sizes[i],
                        type_name = local_type_names[i],
                    };
                }
                code += "\r\n";

                StackEntry pop(int n)
                {
                    var v = oStack.Pop();
                    code += $"{v.type_name} tmp_{n} = evalstack_pop({v.type_name});\r\n";
                    return v;
                }
                void push(int n, StackEntry v)
                {
                    oStack.Push(v);
                    code += $"evalstack_push(tmp_{n});\r\n";
                }
                void start_ctxt(int offset, OpCode opc)
                {
                    code += $"INST_{offset}: //{opc.Name}\r\n";
                    code += "{\r\n";
                }
                void end_ctxt()
                {
                    code += "}\r\n";
                }

                do
                {
                    var opc = instructions.GetCurrentOpCode();
                    start_ctxt(instructions.CurrentOffset, opc);

                    if (opc == OpCodes.Add)
                    {
                        var t0 = pop(0);
                        var t1 = pop(1);

                        var tn = new StackEntry();
                        if (t0.size >= t1.size && !t1.type_name.EndsWith('*'))
                        {
                            tn.size = t0.size;
                            tn.type_name = t0.type_name;
                        }
                        else
                        {
                            tn.size = t1.size;
                            tn.type_name = t1.type_name;
                        }

                        code += $"{tn.type_name} tmp_2 = tmp_0 + tmp_1;\r\n";
                        push(2, tn);
                    }
                    else if (opc == OpCodes.And)
                    {
                        var t0 = pop(0);
                        var t1 = pop(1);

                        var tn = new StackEntry();
                        tn.size = t0.size;
                        tn.type_name = t0.type_name;

                        code += $"{tn.type_name} tmp_2 = tmp_0 & tmp_1;\r\n";
                        push(2, tn);
                    }
                    else if (opc == OpCodes.Nop)
                    {
                        code += "\r\n";
                    }
                    else if (new OpCode[] { OpCodes.Beq, OpCodes.Beq_S }.Contains(opc))
                    {
                        var t0 = pop(0);
                        var t1 = pop(1);

                        code += $"if (tmp_0 == tmp_1) goto INST_{instructions.CurrentOffset + instructions.GetCurrentInstructionSize() + (int)instructions.GetParameter(0)};";
                        code += "\r\n";
                    }
                    else if (new OpCode[] { OpCodes.Bne_Un, OpCodes.Bne_Un_S }.Contains(opc))
                    {
                        var t0 = pop(0);
                        var t1 = pop(1);

                        code += $"if (tmp_0 != tmp_1) goto INST_{instructions.CurrentOffset + instructions.GetCurrentInstructionSize() + (int)instructions.GetParameter(0)};";
                        code += "\r\n";
                    }
                    else if (new OpCode[] { OpCodes.Bge, OpCodes.Bge_S }.Contains(opc))
                    {
                        var t0 = pop(0);
                        var t1 = pop(1);

                        code += $"if ((int64_t)tmp_0 >= (int64_t)tmp_1) goto INST_{instructions.CurrentOffset + instructions.GetCurrentInstructionSize() + (int)instructions.GetParameter(0)};";
                        code += "\r\n";
                    }
                    else if (new OpCode[] { OpCodes.Bge_Un, OpCodes.Bge_Un_S }.Contains(opc))
                    {
                        var t0 = pop(0);
                        var t1 = pop(1);

                        code += $"if ((uint64_t)tmp_0 >= (uint64_t)tmp_1) goto INST_{instructions.CurrentOffset + instructions.GetCurrentInstructionSize() + (int)instructions.GetParameter(0)};";
                        code += "\r\n";
                    }
                    else if (new OpCode[] { OpCodes.Bgt, OpCodes.Bgt_S }.Contains(opc))
                    {
                        var t0 = pop(0);
                        var t1 = pop(1);

                        code += $"if ((int64_t)tmp_0 > (int64_t)tmp_1) goto INST_{instructions.CurrentOffset + instructions.GetCurrentInstructionSize() + (int)instructions.GetParameter(0)};";
                        code += "\r\n";
                    }
                    else if (new OpCode[] { OpCodes.Bgt_Un, OpCodes.Bgt_Un_S }.Contains(opc))
                    {
                        var t0 = pop(0);
                        var t1 = pop(1);

                        code += $"if ((uint64_t)tmp_0 > (uint64_t)tmp_1) goto INST_{instructions.CurrentOffset + instructions.GetCurrentInstructionSize() + (int)instructions.GetParameter(0)};";
                        code += "\r\n";
                    }
                    else if (new OpCode[] { OpCodes.Ble, OpCodes.Ble_S }.Contains(opc))
                    {
                        var t0 = pop(0);
                        var t1 = pop(1);

                        code += $"if ((int64_t)tmp_0 <= (int64_t)tmp_1) goto INST_{instructions.CurrentOffset + instructions.GetCurrentInstructionSize() + (int)instructions.GetParameter(0)};";
                        code += "\r\n";
                    }
                    else if (new OpCode[] { OpCodes.Ble_Un, OpCodes.Ble_Un_S }.Contains(opc))
                    {
                        var t0 = pop(0);
                        var t1 = pop(1);

                        code += $"if ((uint64_t)tmp_0 <= (uint64_t)tmp_1) goto INST_{instructions.CurrentOffset + instructions.GetCurrentInstructionSize() + (int)instructions.GetParameter(0)};";
                        code += "\r\n";
                    }
                    else if (new OpCode[] { OpCodes.Blt, OpCodes.Blt_S }.Contains(opc))
                    {
                        var t0 = pop(0);
                        var t1 = pop(1);

                        code += $"if ((int64_t)tmp_0 < (int64_t)tmp_1) goto INST_{instructions.CurrentOffset + instructions.GetCurrentInstructionSize() + (int)instructions.GetParameter(0)};";
                        code += "\r\n";
                    }
                    else if (new OpCode[] { OpCodes.Blt_Un, OpCodes.Blt_Un_S }.Contains(opc))
                    {
                        var t0 = pop(0);
                        var t1 = pop(1);

                        code += $"if ((uint64_t)tmp_0 < (uint64_t)tmp_1) goto INST_{instructions.CurrentOffset + instructions.GetCurrentInstructionSize() + (int)instructions.GetParameter(0)};";
                        code += "\r\n";
                    }
                    else if (new OpCode[] { OpCodes.Br, OpCodes.Br_S }.Contains(opc))
                    {
                        code += $"goto INST_{instructions.CurrentOffset + instructions.GetCurrentInstructionSize() + (int)instructions.GetParameter(0)};";
                        code += "\r\n";
                    }
                    else if (new OpCode[] { OpCodes.Brfalse, OpCodes.Brfalse_S }.Contains(opc))
                    {
                        var t0 = pop(0);

                        code += $"if (!tmp_0) goto INST_{instructions.CurrentOffset + instructions.GetCurrentInstructionSize() + (int)instructions.GetParameter(0)};";
                        code += "\r\n";
                    }
                    else if (new OpCode[] { OpCodes.Brtrue, OpCodes.Brtrue_S }.Contains(opc))
                    {
                        var t0 = pop(0);

                        code += $"if (tmp_0) goto INST_{instructions.CurrentOffset + instructions.GetCurrentInstructionSize() + (int)instructions.GetParameter(0)};";
                        code += "\r\n";
                    }
                    else if (opc == OpCodes.Ceq)
                    {
                        var t0 = pop(0);
                        var t1 = pop(1);

                        code += $"int32_t tmp_2 = (tmp_0 == tmp_1);\r\n";
                        push(2, new StackEntry() { type_name = "int32_t", size = 4 });
                    }
                    else if (opc == OpCodes.Cgt)
                    {
                        var t0 = pop(0);
                        var t1 = pop(1);

                        code += $"int32_t tmp_2 = (tmp_0 > tmp_1);\r\n";
                        push(2, new StackEntry() { type_name = "int32_t", size = 4 });
                    }
                    else if (opc == OpCodes.Cgt_Un)
                    {
                        var t0 = pop(0);
                        var t1 = pop(1);

                        code += $"int32_t tmp_2 = ((uint64_t)tmp_0 > (uint64_t)tmp_1);\r\n";
                        push(2, new StackEntry() { type_name = "int32_t", size = 4 });
                    }
                    else if (opc == OpCodes.Clt)
                    {
                        var t0 = pop(0);
                        var t1 = pop(1);

                        code += $"int32_t tmp_2 = (tmp_0 < tmp_1);\r\n";
                        push(2, new StackEntry() { type_name = "int32_t", size = 4 });
                    }
                    else if (opc == OpCodes.Clt_Un)
                    {
                        var t0 = pop(0);
                        var t1 = pop(1);

                        code += $"int32_t tmp_2 = ((uint64_t)tmp_0 < (uint64_t)tmp_1);\r\n";
                        push(2, new StackEntry() { type_name = "int32_t", size = 4 });
                    }
                    else if (new OpCode[] { OpCodes.Ldc_I4, OpCodes.Ldc_I4_S, }.Contains(opc))
                    {
                        var tn = new StackEntry()
                        {
                            size = 4,
                            type_name = "int32_t",
                        };

                        var v = instructions.GetParameter(0);
                        code += $"{tn.type_name} tmp_{0} = {v};\r\n";
                        push(0, tn);
                    }
                    else if (new OpCode[] { OpCodes.Ldc_I4_0, OpCodes.Ldc_I4_1, OpCodes.Ldc_I4_2, OpCodes.Ldc_I4_3, OpCodes.Ldc_I4_4, OpCodes.Ldc_I4_5, OpCodes.Ldc_I4_6, OpCodes.Ldc_I4_7, OpCodes.Ldc_I4_8, OpCodes.Ldc_I4_M1, }.Contains(opc))
                    {
                        var tn = new StackEntry()
                        {
                            size = 4,
                            type_name = "int32_t",
                        };

                        var v = opc.Name.Replace("ldc.i4.", "");
                        if (v == "m1") v = "-1";

                        code += $"{tn.type_name} tmp_{0} = {v};\r\n";
                        push(0, tn);
                    }
                    else if (new OpCode[] { OpCodes.Stloc_0, OpCodes.Stloc_1, OpCodes.Stloc_2, OpCodes.Stloc_3 }.Contains(opc))
                    {
                        var v = int.Parse(opc.Name.Replace("stloc.", ""));
                        locals[v] = pop(0);
                        code += $"lcl_{v} = tmp_{0};\r\n";
                    }
                    else if (new OpCode[] { OpCodes.Ldloc_0, OpCodes.Ldloc_1, OpCodes.Ldloc_2, OpCodes.Ldloc_3 }.Contains(opc))
                    {
                        var v = int.Parse(opc.Name.Replace("ldloc.", ""));
                        code += $"{locals[v].type_name} tmp_{0} = lcl_{v};\r\n";
                        push(0, locals[v]);
                    }
                    else if (new OpCode[] { OpCodes.Ldarg_0, OpCodes.Ldarg_1, OpCodes.Ldarg_2, OpCodes.Ldarg_3 }.Contains(opc))
                    {
                        var v = int.Parse(opc.Name.Replace("ldarg.", ""));
                        var tn = new StackEntry() { type_name = param_type_names[v], size = param_sizes[v] };
                        code += $"{tn.type_name} tmp_{0} = arg_{v};\r\n";
                        push(0, tn);
                    }
                    else if (opc == OpCodes.Ret)
                    {
                        if (oStack.Count > 0)
                        {
                            var t0 = pop(0);
                            code += $"return tmp_{0};\r\n";
                        }
                        else
                            code += $"return;\r\n";
                    }
                    else if (opc == OpCodes.Call)
                    {
                        var mbase = module.ResolveMethod((int)instructions.GetParameter(0));
                        var @params = mbase.GetParameters();
                        var tgt_mthd_name = "";
                        Type retType = null;

                        if (mbase is MethodInfo)
                        {
                            var mbase_mi = (MethodInfo)mbase;
                            retType = mbase_mi.ReturnType;
                            if (parser.typeResolver.IsNative(mbase_mi))
                            {
                                tgt_mthd_name = parser.typeResolver.GetNativeName(mbase_mi);
                                parser.Records.NativeMethods[NameMangler.Mangle(mbase_mi)] = parser.typeResolver.GetNativeName(mbase_mi);
                            }
                            else
                            {
                                tgt_mthd_name = NameMangler.Mangle(mbase_mi);
                                parser.Parse(mbase_mi);
                            }
                        }
                        else if (mbase is ConstructorInfo)
                        {
                            var mbase_ci = (ConstructorInfo)mbase;
                            retType = typeof(void);
                            parser.Parse(mbase_ci);
                            tgt_mthd_name = NameMangler.Mangle(mbase_ci);
                        }

                        string param_part = "";
                        for (int i = 0; i < @params.Length; i++)
                        {
                            var ni = pop(i);
                            param_part = $"tmp_{i}, " + param_part;
                        }
                        if (!mbase.IsStatic)
                        {
                            var ni_inst = pop(@params.Length);
                            param_part = $"tmp_{@params.Length}, ";
                        }
                        if (param_part.Length > 0)
                            param_part = param_part.Substring(0, param_part.Length - 2);
                        if (retType == typeof(void))
                            code += $"{tgt_mthd_name}({param_part});\r\n";
                        else
                        {
                            var rEnt = new StackEntry()
                            {
                                type_name = parser.typeResolver.GetTypeRefName(retType),
                                size = parser.typeResolver.GetTypeRefSize(retType),
                            };
                            code += $"{rEnt.type_name} tmp_{@params.Length + 1} = {tgt_mthd_name}({param_part});\r\n";
                            push(@params.Length + 1, rEnt);
                        }
                    }
                    else
                    {
                        throw new NotImplementedException(opc.ToString());
                    }

                    end_ctxt();
                } while (instructions.NextInstruction());

                code += "}\r\n";
                return code;
            }
        }
    }
}
