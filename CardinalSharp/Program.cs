namespace CardinalSharp
{
    public interface IA
    {
        void Foo(int a);
    }

    public class A : IA
    {
        public virtual void Foo(int a)
        {
            Console.WriteLine("A");
        }
    }

    public class B : A
    {
        public override void Foo(int a)
        {
            Console.WriteLine("B");
        }

        public void Foo(ref int a)
        {
            a = 0;
        }
    }

    public class C : B
    {
        public override void Foo(int a)
        {
            Console.WriteLine("B");
        }
    }

    public class Program
    {
        static void Tester(int bootloaderID)
        {
            if(bootloaderID == BootServices.Multiboot2Magic)
            {
                Substitutes.Console.WriteLine(0, 0);
            }
            else
            {
                Substitutes.Console.WriteLine(1, 0);
            }
        }

        public static void Main()
        {
            var bootloaderID = BootServices.GetBootloaderID();
            Tester(bootloaderID);
        }
    }
}
