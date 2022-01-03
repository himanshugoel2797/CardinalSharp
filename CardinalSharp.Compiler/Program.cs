// See https://aka.ms/new-console-template for more information
// Read replacement type info from CardinalSharp.RuntimeLib
// Read CardinalSharp, convert it and loaded libraries into easier to parse bytecode for a runtime to execute
using System;
using CardinalSharp.Compiler;

TypeResolver typeResolver = new TypeResolver();
Records records = new Records();
TypeParser typeParser = new TypeParser(records, typeResolver);
MethodParser methodParser = new MethodParser(records, typeParser, typeResolver);

Console.WriteLine("Parsing CardinalSharp...");
var main_mthd = typeof(CardinalSharp.Program).GetMethod("Main");
if (main_mthd == null)
    throw new NullReferenceException("OS Entry Point not found!");
methodParser.Parse(main_mthd);
Console.WriteLine("Done!");

Console.WriteLine("Writing Records...");
ProgramSaver saver = new ProgramSaver("kernel.cself");
saver.Write(records);
Console.WriteLine("Done!");