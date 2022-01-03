namespace CardinalSharp.Compiler.Support
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class TypeMappingAttribute : Attribute
    {
        public TypeMappingAttribute(Type target)
        {
            Target = target;
        }

        public Type Target { get; }
    }
}