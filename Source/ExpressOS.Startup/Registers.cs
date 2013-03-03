using System;
using System.Runtime.InteropServices;

namespace ExpressOS.Kernel.Arch
{
    [StructLayout(LayoutKind.Sequential)]
    public struct Registers
    {
        public int r0;
        public int r1;
        public int r2;
        public int r3;
        public int r4;
        public int r5;
        public int r6;
        public int r7;
        public int r8;
        public int r9;
        public int r10;
        public int fp;
        public int ip;
        public int sp;
        public int lr;
        public int pc;
        public int cpsr;
        public int Orignal_r0;
    }
}

