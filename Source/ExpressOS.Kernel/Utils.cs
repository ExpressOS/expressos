namespace ExpressOS.Kernel
{
    public static class Utils
    {
        public static void Assert(bool condition)
        {
            Arch.ArchDefinition.Assert(condition);
        }

        public static void Panic()
        {
            ExpressOS.Kernel.Arch.Console.Flush();
            Arch.ArchDefinition.Panic();
        }
    }
}
