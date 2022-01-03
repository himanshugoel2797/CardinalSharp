// See https://aka.ms/new-console-template for more information
using CardinalSharp.Runtime;

ExecutableMemory exec_mem = new ExecutableMemory();
ProgramLoader loader = new ProgramLoader(@"C:\Users\hgoel\source\repos\CardinalSharp\CardinalSharp.Compiler\bin\Debug\net6.0\kernel.cself");
var rec = loader.Read();
//Call the bootstrap compiler to convert this into assembly
AssemblyGenerator assemblyGenerator = new AssemblyGenerator();
assemblyGenerator.Compile(rec);


Console.WriteLine("Hello, World!");
