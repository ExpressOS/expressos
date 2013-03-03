using System.Diagnostics.Contracts;

namespace ExpressOS.Kernel
{
    public struct ELF32Header
    {
        internal long ident1;
        internal long ident2;
        internal ushort type;
        internal ushort machine;
        internal uint version;
        // Program start address
        internal uint EntryPoint;
        // File offset of program header table
        internal uint ProgramHeaderOffest;
        internal uint SectionHeaderOffset;
        internal uint flags;
        internal ushort ELFHeaderSize;
        internal ushort ProgramHeaderSize;
        internal ushort NumOfProgramHeader;
        internal ushort SectionHeaderSize;
        internal ushort NumOfSectionHeader;
        internal ushort shstrndx;

        public const uint Size = 8 * 2 + 2 * 2 + 4 * 5 + 2 * 6;

        public const ushort ELF_TYPE_EXECUTABLE = 2;
        private const int LengthOfAuxVector = 8;
        // Aux vector
        private const uint AT_PHNUM = 5;
        private const uint AT_ENTRY = 9;
        private const uint AT_PHDR = 3;

        public static int Parse(int helperPid, File file, Process proc, ref UserPtr stackTop)
        {
            Contract.Requires(file.GhostOwner == proc);
            Contract.Requires(proc.Space.GhostOwner == proc);

            var buf = new byte[Size];
            uint pos = 0;
            if (file.Read(buf, ref pos) != buf.Length)
                return -ErrorCode.EINVAL;

            var eh = Read(buf);
            if (eh.type != ELF32Header.ELF_TYPE_EXECUTABLE || eh.ProgramHeaderOffest == 0)
                return -ErrorCode.ENOEXEC;

            proc.EntryPoint = eh.EntryPoint;

            ELF32ProgramHeader ph = new ELF32ProgramHeader();
            var ret = FindInterpreter(file, eh, ref ph);
            if (ret == -ErrorCode.EINVAL)
            {
                Arch.Console.WriteLine("Malformed ELF file");
                return ret;
            }
            else if (ret == 0)
            {
                var interpreterBuf = new byte[ph.FileSize];
                pos = ph.offset;
                if (file.Read(interpreterBuf, ref pos) != interpreterBuf.Length)
                    return -ErrorCode.EINVAL;

                var interpreterName = new ASCIIString(interpreterBuf);
                ErrorCode ec;
                var interpreter_inode = Arch.ArchFS.Open(helperPid, interpreterName, 0, 0, out ec);
                if (interpreter_inode == null)
                    return -ErrorCode.ENOENT;

                var interpreter = new File(proc, interpreter_inode, FileFlags.ReadOnly, 0);

                /*
                 * Parse the information of linker.
                 *
                 * This function will also override the entry point.
                 */
                if (Parse(helperPid, interpreter, proc, ref stackTop) != 0)
                    return -ErrorCode.EINVAL;

                // So now let's copy the program header to the top of the stack, and push auxlirary vectors
                PushProgramHeaderAndAuxliraryVectors(proc, file, eh, ref stackTop);
            }

            return MapInSegments(file, proc, eh);
        }

        private static ELF32Header Read(byte[] buf)
        {
            var r = new ELF32Header();
            int off = 0;
            r.ident1 = Deserializer.ReadLong(buf, off); off += sizeof(long);
            r.ident2 = Deserializer.ReadLong(buf, off); off += sizeof(long);
            r.type = Deserializer.ReadUShort(buf, off); off += sizeof(ushort);
            r.machine = Deserializer.ReadUShort(buf, off); off += sizeof(ushort);
            r.version = Deserializer.ReadUInt(buf, off); off += sizeof(uint);
            r.EntryPoint = Deserializer.ReadUInt(buf, off); off += sizeof(uint);
            r.ProgramHeaderOffest = Deserializer.ReadUInt(buf, off); off += sizeof(uint);
            r.SectionHeaderOffset = Deserializer.ReadUInt(buf, off); off += sizeof(uint);
            r.flags = Deserializer.ReadUInt(buf, off); off += sizeof(uint);
            r.ELFHeaderSize = Deserializer.ReadUShort(buf, off); off += sizeof(ushort);
            r.ProgramHeaderSize = Deserializer.ReadUShort(buf, off); off += sizeof(ushort);
            r.NumOfProgramHeader = Deserializer.ReadUShort(buf, off); off += sizeof(ushort);
            r.SectionHeaderSize = Deserializer.ReadUShort(buf, off); off += sizeof(ushort);
            r.NumOfSectionHeader = Deserializer.ReadUShort(buf, off); off += sizeof(ushort);
            r.shstrndx = Deserializer.ReadUShort(buf, off); off += sizeof(ushort);
            return r;
        }

        private static int FindInterpreter(File file, ELF32Header eh, ref ELF32ProgramHeader ret)
        {
            var buf = new byte[ELF32ProgramHeader.Size];

            uint pos = 0;
            ushort i = 0;

            while (i < eh.NumOfProgramHeader)
            {
                pos = (uint)(eh.ProgramHeaderOffest + eh.ProgramHeaderSize * i);
                if (file.Read(buf, ref pos) != ELF32ProgramHeader.Size)
                    return -ErrorCode.EINVAL;

                ret = ELF32ProgramHeader.Read(buf);

                if (ret.type == ELF32ProgramHeader.PT_INTERP)
                    break;

                ++i;
            }
            return i == eh.NumOfProgramHeader ? -ErrorCode.ENOENT : 0;
        }

        private static int MapInSegments(File file, Process proc, ELF32Header eh)
        {
            Contract.Requires(file != null && file.GhostOwner == proc);
            Contract.Requires(proc.Space.GhostOwner == proc);

            var buf = new byte[ELF32ProgramHeader.Size];

            var ph = new ELF32ProgramHeader();
            // At this point we need to map in all stuff in PT_LOAD
            for (var i = 0; i < eh.NumOfProgramHeader; ++i)
            {
                var pos = (uint)(eh.ProgramHeaderOffest + eh.ProgramHeaderSize * i);
                if (file.Read(buf, ref pos) != buf.Length)
                    return -ErrorCode.EINVAL;

                ph = ELF32ProgramHeader.Read(buf);

                var size = ph.FileSize > ph.MemorySize ? ph.FileSize : ph.MemorySize;

                if (ph.type != ELF32ProgramHeader.PT_LOAD)
                    continue;

                // Round address to page boundary

                var diff = Arch.ArchDefinition.PageOffset(ph.vaddr);
                var vaddr = new Pointer(ph.vaddr);
                var offset = ph.offset;
                var memSize = (int)Arch.ArchDefinition.PageAlign((uint)ph.MemorySize);
                var fileSize = ph.FileSize;

                if (diff < 0 || ph.offset < diff || fileSize + diff > file.inode.Size || fileSize <= 0 || memSize <= 0)
                    return -ErrorCode.EINVAL;

                vaddr -= diff;
                offset -= (uint)diff;
                fileSize += diff;

                if (fileSize > memSize)
                    fileSize = memSize;

                if (proc.Space.AddMapping(ph.ExpressOSAccessFlag, 0, file, offset, fileSize, vaddr, memSize) != 0)
                    return -ErrorCode.ENOMEM;

                // Update brk

                var segmentEnd = (vaddr + memSize).ToUInt32();
                if (segmentEnd > proc.Space.Brk)
                {
                    proc.Space.Brk = segmentEnd;
                    proc.Space.StartBrk = segmentEnd;
                }
            }
            return 0;
        }

        private static int PushProgramHeaderAndAuxliraryVectors(Process proc, File file, ELF32Header eh, ref UserPtr stackTop)
        {
            var programHeaderLength = eh.NumOfProgramHeader * eh.ProgramHeaderSize;
            var buf = new byte[programHeaderLength];
            uint pos = eh.ProgramHeaderOffest;
            
            if (file.Read(buf, ref pos) != programHeaderLength)
                return -ErrorCode.ENOMEM;

            stackTop -= programHeaderLength;
            UserPtr ph_ptr = stackTop;

            if (ph_ptr.Write(proc, buf) != 0)
                return -ErrorCode.ENOMEM;

            // align
            stackTop = UserPtr.RoundDown(stackTop);

            var aux_vector = new uint[LengthOfAuxVector];
            aux_vector[0] = AT_PHDR;
            aux_vector[1] = ph_ptr.Value.ToUInt32();
            aux_vector[2] = AT_ENTRY;
            aux_vector[3] = eh.EntryPoint;
            aux_vector[4] = AT_PHNUM;
            aux_vector[5] = eh.NumOfProgramHeader;
            aux_vector[6] = 0;
            aux_vector[7] = 0;

            var auxVectorSize = sizeof(uint) * LengthOfAuxVector;
            stackTop -= auxVectorSize;

            if (stackTop.Write(proc, aux_vector) != 0)
                return -ErrorCode.ENOMEM;

            return 0;
        }
    }

    internal struct ELF32ProgramHeader
    {
        public const uint PT_LOAD = 1;
        public const uint PT_DYNAMIC = 2;
        public const uint PT_INTERP = 3;
        public const uint PT_PHDR = 6;
        /* Access Control bits */
        public const uint PF_X = 1;
        public const uint PF_W = 2;
        public const uint PF_R = 4;

        internal uint type;
        internal uint offset;
        internal uint vaddr;
        internal uint paddr;
        internal int FileSize;
        internal int MemorySize;
        internal uint flags;
        internal uint align;

        public const uint Size = 4 * 8;

        public static ELF32ProgramHeader Read(byte[] buf)
        {
            Contract.Requires(buf.Length == Size);
            var r = new ELF32ProgramHeader();
            r.type = Deserializer.ReadUInt(buf, 0);
            r.offset = Deserializer.ReadUInt(buf, sizeof(uint));
            r.vaddr = Deserializer.ReadUInt(buf, sizeof(uint) * 2);
            r.paddr = Deserializer.ReadUInt(buf, sizeof(uint) * 3);
            r.FileSize = Deserializer.ReadInt(buf, sizeof(uint) * 4);
            r.MemorySize = Deserializer.ReadInt(buf, sizeof(uint) * 5);
            r.flags = Deserializer.ReadUInt(buf, sizeof(uint) * 6);
            r.align = Deserializer.ReadUInt(buf, sizeof(uint) * 7);
            return r;
        }

        public uint ExpressOSAccessFlag
        {
            get
            {
                uint res = 0;
                if ((flags & PF_X) == PF_X)
                    res |= MemoryRegion.FAULT_EXEC;

                if ((flags & PF_W) == PF_W)
                    res |= MemoryRegion.FAULT_WRITE;

                if ((flags & PF_R) == PF_R)
                    res |= MemoryRegion.FAULT_READ;

                return res;
            }
        }
    }
}
