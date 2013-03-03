
namespace ExpressOS.Kernel.Arch
{
    public struct ArchAPI
    {
        public static unsafe void ReturnFromSyscall(L4Handle target, ref ExceptionRegisters regs, int retval)
        {
            var p_exc = NativeMethods.l4api_utcb_exc();
            *p_exc = regs;
            
            // Skip the int $0x80 instruction
            p_exc->ip += 2;
            p_exc->eax = retval;

            var tag = new Msgtag(0, ExceptionRegisters.L4_UTCB_EXCEPTION_REGS_SIZE, 0, 0);
            NativeMethods.l4api_ipc_send(target, NativeMethods.l4api_utcb(), tag, Timeout.Never);
        }

        public static void GetSyscallParameters(ExceptionRegisters regs, out int scno, out int arg0, out int arg1, out int arg2, out int arg3, out int arg4, out int arg5)
        {
            scno = regs.eax;
            arg0 = regs.ebx;
            arg1 = regs.ecx;
            arg2 = regs.edx;
            arg3 = regs.esi;
            arg4 = regs.edi;
            arg5 = regs.ebp;
        }

        public static void GetPageFaultInfo(ref MessageRegisters mr, out uint pfa, out uint pc, out uint faultType)
        {
            pfa = (uint)mr.mr0;
            pc = (uint)mr.mr1;
            const uint WRITE_BIT = 2;
            faultType = ((pfa & WRITE_BIT) != 0) ? L4FPage.L4_FPAGE_FAULT_WRITE : L4FPage.L4_FPAGE_FAULT_READ;
        }

        public static void ReturnFromPageFault(L4Handle target, out Msgtag tag, ref MessageRegisters mr, uint pfa, Pointer physicalPage, uint permssion)
        {
            var virt_page_addr = ArchDefinition.PageIndex(physicalPage.ToUInt32());
            var fpage = new L4FPage(virt_page_addr, ArchDefinition.PageShift, (int)permssion);
            tag = new Msgtag(0, 0, 1, 0);
            mr.mr0 = (int)(ArchDefinition.PageIndex(pfa) | Msgtag.L4_ITEM_MAP);
            mr.mr1 = (int)fpage.raw;
            NativeMethods.l4api_ipc_send(target, NativeMethods.l4api_utcb(), tag, Timeout.Never);
        }


    }
}
