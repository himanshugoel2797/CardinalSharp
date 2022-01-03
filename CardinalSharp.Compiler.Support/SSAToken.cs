namespace CardinalSharp.Compiler
{
    public class SSAToken
    {
        public int ID;
        public int InstructionOffset;
        public int[] Parameters;
        public ulong[] Constants;
        public string String;
        public InstructionTypes Operation;

        static SSAToken()
        {
        }

        public override string ToString()
        {
            return ID.ToString() + " " + Operation.ToString();
        }
    }
}
