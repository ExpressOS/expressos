using System.Diagnostics.Contracts;
namespace ExpressOS.Kernel
{
    internal class BinderIPCMarshaler
    {
        readonly ByteBufferRef buf;
        int ReadCursor;
        internal int WriteCursor { get; private set; }
        Thread current;

        public const int kPatchTableSize = 16;
        int[] patchTable;
        internal int CurrentPatchEntry { get; private set; }
        internal int PatchTableOffset { get; private set; }

        [ContractInvariantMethod]
        private void ObjectInvariant()
        {
            Contract.Invariant(ReadCursor >= 0);
            Contract.Invariant(WriteCursor >= 0);
        }

        internal BinderIPCMarshaler(Thread current, ByteBufferRef buf)
        {
            Contract.Requires(buf.Length >= kPatchTableSize * sizeof(int));

            this.buf = buf;
            this.ReadCursor = 0;
            this.WriteCursor = 0;
            this.current = current;
            this.patchTable = new int[kPatchTableSize];
            this.CurrentPatchEntry = 0;
        }

        internal int Marshal(UserPtr writeBuf, int size)
        {
            if (size < 0 || size > buf.Length)
                return -ErrorCode.EINVAL;

            ReadCursor = 0;

            // Copy the full data into the buffer, but don't increment the read pointer yet.
            var r = writeBuf.Read(current, buf, size);
            if (r != 0)
                return -1;

            // Advance the cursor
            WriteCursor = size;
            // parse the command
            while (ReadCursor < size)
            {
                if (ReadCursor + sizeof(uint) > buf.Length)
                    return -ErrorCode.ENOMEM;

                var cmd = Deserializer.ReadUInt(buf, ReadCursor);
                ReadCursor += sizeof(uint);

                switch (cmd)
                {
                    case BinderINode.BC_INCREFS:
                    case BinderINode.BC_ACQUIRE:
                    case BinderINode.BC_RELEASE:
                    case BinderINode.BC_DECREFS:
                        ReadCursor += sizeof(int);
                        break;

                    case BinderINode.BC_INCREFS_DONE:
                    case BinderINode.BC_ACQUIRE_DONE:
                    case BinderINode.BC_REQUEST_DEATH_NOTIFICATION:
                    case BinderINode.BC_CLEAR_DEATH_NOTIFICATION:
                        ReadCursor += Pointer.Size * 2;
                        break;

                    case BinderINode.BC_ATTEMPT_ACQUIRE:
                    case BinderINode.BC_ACQUIRE_RESULT:
                        // Unimplemented in Android IPC
                        return -ErrorCode.EINVAL;

                    case BinderINode.BC_FREE_BUFFER:
                        {
                            if (ReadCursor + sizeof(uint) > buf.Length)
                                return -ErrorCode.ENOMEM;

                            var addr = Deserializer.ReadUInt(buf, ReadCursor);
                            var new_val = addr - current.Parent.binderVMStart.Value.ToUInt32() + current.Parent.ShadowBinderVMStart;
                            Deserializer.WriteUInt(new_val, buf, ReadCursor);
                            ReadCursor += Pointer.Size;
                            break;
                        }

                    case BinderINode.BC_TRANSACTION:
                    case BinderINode.BC_REPLY:
                        {
                            var ret = MarshalTransaction();
                            if (ret < 0)
                                return ret;

                            break;
                        }

                    case BinderINode.BC_REGISTER_LOOPER:
                    case BinderINode.BC_ENTER_LOOPER:
                    case BinderINode.BC_EXIT_LOOPER:
                        break;

                    default:
                        Arch.Console.Write("binder: unsupported IPC primitive ");
                        Arch.Console.Write(cmd);
                        Arch.Console.WriteLine();
                        return -1;
                }
            }

            r = AppendPatchTable();
            if (r != 0)
                return -1;

            //Arch.Console.Write("Dump write buffer ");
            //Arch.Console.Write(current.Tid);
            //Arch.Console.WriteLine();
            //DumpBuf(new Pointer(buf.Location), size);
            return 0;
        }

        private static void DumpUserBuf(Thread current, UserPtr writeBuf, int size)
        {
            var buf = new byte[(size + 3) / 4];
            writeBuf.Read(current, buf, size);

            var buf_ref = new ByteBufferRef(buf);
            DumpBuf(new Pointer(buf_ref.Location), size);
        }

        public static unsafe void DumpBuf(Pointer buf, int size)
        {
            Arch.Console.Write("Dump: size=");
            Arch.Console.Write(size);
            Arch.Console.WriteLine();

            for (var i = 1; i <= (size + 3) / 4; ++i)
            {
                int* p = (int*)buf.ToPointer() + i - 1;
                Arch.Console.Write(*p);
                Arch.Console.Write(' ');
                if (i % 8 == 0)
                    Arch.Console.WriteLine();
            }

            Arch.Console.WriteLine();
            Arch.Console.WriteLine();
        }

        private int AppendPatchTable()
        {
            // Skip the field storing the number of the patch
            PatchTableOffset = WriteCursor + sizeof(int);
            if (WriteCursor + (CurrentPatchEntry + 1) * sizeof(int) > buf.Length)
                return -1;

            Deserializer.WriteInt(CurrentPatchEntry, buf, WriteCursor);
            WriteCursor += sizeof(int);
            for (var i = 0; i < CurrentPatchEntry; i++)
            {
                Deserializer.WriteInt(patchTable[i], buf, WriteCursor);
                WriteCursor += sizeof(int);
            }

            return 0;
        }

        private int MarshalDataSegments(int size, ref UserPtr data, int offset)
        {
            Contract.Requires(size >= 0);
            Contract.Ensures(ReadCursor == Contract.OldValue(ReadCursor));

            if (size == 0)
                return 0;

            if (size > buf.Length - WriteCursor)
                return -ErrorCode.ENOMEM;

            var b = buf.Slice(WriteCursor, size);
            
            // Post condition for ByteBufferRef
            Contract.Assume(b.Length == size);
            var r = data.Read(current, b, size);
            if (r != 0)
                return -ErrorCode.EFAULT;

            data = new UserPtr((uint)WriteCursor);
            WriteCursor += size;

            // Add patch entry
            if (CurrentPatchEntry >= kPatchTableSize)
                return -ErrorCode.ENOMEM;

            patchTable[CurrentPatchEntry++] = ReadCursor + offset;

            return 0;
        }

        private int MarshalTransaction()
        {
            if (ReadCursor + binder_transaction_data.Size > buf.Length)
                return -ErrorCode.ENOMEM;

            var tr = binder_transaction_data.Deserialize(buf, ReadCursor);

            var r = MarshalDataSegments((int)tr.data_size, ref tr.data_buffer, binder_transaction_data.DATA_BUFFER_OFFSET);
            if (r != 0)
                return r;

            r = MarshalDataSegments((int)tr.offsets_size, ref tr.data_offsets, binder_transaction_data.DATA_OFFSETS_OFFSET);
            if (r != 0)
                return r;

            tr.Write(buf, ReadCursor);
            ReadCursor += binder_transaction_data.Size;
            return 0;
        }
    }
}
