
namespace ExpressOS.Kernel.Arch
{
    public static class Console
    {
        public static void Flush()
        {
            NativeMethods.console_flush();
        }

        public static void Write(string s)
        {
            foreach (var c in s)
                Write(c);
        }

        public static void Write(ASCIIString s)
        {
            foreach (var c in s.GetByteString())
                Write(c);
        }

        public static void WriteLine(string s)
        {
            Write(s);
            WriteLine();
        }

        public static void Write(char c)
        {
            NativeMethods.console_putchar(c);
        }

        public static void Write(byte v)
        {
            Write("0x");
            WriteByte(v);
        }

        public static void Write(int v)
        {
            Write("0x");
            WriteByte(v >> 24);
            WriteByte((v & 0xff0000) >> 16);
            WriteByte((v & 0xff00) >> 8);
            WriteByte(v & 0xff);
        }

        public static void Write(bool v)
        {
            Write(v ? "true" : "false");
        }

        public static void Write(ulong v)
        {
            Write("0x");
            WriteIntNoPrefix((uint)(v >> 32));
            WriteIntNoPrefix((uint)(v & 0xffffffff));
        }

        public static void Write(uint v)
        {
            Write("0x");
            WriteIntNoPrefix(v);
        }

        private static void WriteIntNoPrefix(uint v)
        {
            WriteByte((int)(v >> 24));
            WriteByte((int)((v & 0xff0000) >> 16));
            WriteByte((int)((v & 0xff00) >> 8));
            WriteByte((int)(v & 0xff));
        }


        private static void WriteByte(int b)
        {
            WriteHex((b & 0xff) >> 4);
            WriteHex(b & 0xf);
        }

        private static void WriteHex(int b)
        {
            if (0 <= b && b <= 9)
            {
                NativeMethods.console_putchar('0' + b);
            }
            else if (10 <= b && b <= 15)
            {
                NativeMethods.console_putchar('a' + b - 10);
            }
            else
            {
                NativeMethods.console_putchar('?');
            }
        }

        public static void WriteLine()
        {
            Write('\n');
            Flush();
        }

        public static void Write(long p)
        {
            ulong v = (ulong)p;
            Write("0x");
            WriteByte((int)(v >> 56));
            WriteByte((int)((v >> 48) & 0xff));
            WriteByte((int)((v >> 40) & 0xff));
            WriteByte((int)((v >> 32) & 0xff));
            WriteByte((int)((v >> 24) & 0xff));
            WriteByte((int)((v >> 16) & 0xff));
            WriteByte((int)((v >> 8) & 0xff));
            WriteByte((int)(v & 0xff));
        }
    }
}

