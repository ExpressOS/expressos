using System.Diagnostics.Contracts;

namespace ExpressOS.Kernel
{
    public static class ReadBufferUnmarshaler
    {
        private static byte[] InspectionBuffer;
        private static byte[] WindowFocusChangedHeader;
        private const uint WindowFocusChangedIPCSize = 0x3c;

        public static void Initialize()
        {
            InspectionBuffer = new byte[WindowFocusChangedIPCSize];
            WindowFocusChangedHeader = new byte[] { 0x00, 0x01, 0x00, 0x00, 0x14, 0x00, 0x00, 0x00,
                /* little-endian representation of the UTF16string "android.view.IWindow" */
                0x61, 0x00, 0x6e, 0x00, 0x64, 0x00, 0x72, 0x00, 0x6f, 0x00, 0x69, 0x00, 0x64, 0x00,
                0x2e, 0x00, 0x76, 0x00, 0x69, 0x00, 0x65, 0x00, 0x77, 0x00, 0x2e, 0x00, 0x49, 0x00,
                0x57, 0x00, 0x69, 0x00, 0x6e, 0x00, 0x64, 0x00, 0x6f, 0x00, 0x77, 0x00,
                0x00, 0x00, 0x00, 0x00,
            };
        }

        public static int UnmarshalReadBuffer(Thread thr, ByteBufferRef completionBuf, ref sys_binder_write_desc desc, UserPtr readBuffer, int readBufferSize)
        {
            var proc = thr.Parent;
            var marshaledPtr = new Pointer(completionBuf.Location);

            //Arch.Console.Write("read_consumed:");
            //Arch.Console.Write(desc.read_consumed);
            //BinderIPCMarshaler.DumpBuf(new Pointer(completionBuf.Location), (int)desc.read_consumed); 

            if (proc.binderVMStart == UserPtr.Zero)
            {
                Arch.Console.WriteLine("proc.binderVMStart == UserPtr.Zero");
                return -ErrorCode.EFAULT;
            }

            if (UnmarshalDataEntries(thr, completionBuf, ref desc) != 0)
            {
                Arch.Console.WriteLine("UnmarshalDataEntries failed");
                return -ErrorCode.ENOMEM;
            }

            if (desc.read_consumed > completionBuf.Length)
            {
                Arch.Console.WriteLine("UnmarshalReadBuffer: bad input");
                return -ErrorCode.ENOMEM;
            }

            // Patch pointers and convert file descriptors
            var b = completionBuf.Slice(0, desc.read_consumed);
            if (PatchReadBuffer(thr, b) != 0)
            {
                Arch.Console.WriteLine("Failed to patch read buffer");
                return -ErrorCode.EINVAL;
            }

            if (readBuffer.Write(thr, marshaledPtr, desc.read_consumed) != 0)
            {
                Arch.Console.WriteLine("readBuffer.Write failed");
                return -ErrorCode.ENOMEM;
            }

            return 0;
        }

        private static int UnmarshalDataEntries(Thread current, ByteBufferRef buf, ref sys_binder_write_desc desc)
        {
            var cursor = desc.read_consumed;
            var i = 0;
            while (i < desc.patch_table_entries && cursor < buf.Length)
            {
                if (cursor + 2 * sizeof(uint) > buf.Length)
                    return -1;

                var offset = Deserializer.ReadUInt(buf, cursor);
                cursor += sizeof(uint);
                var length = (int)Deserializer.ReadUInt(buf, cursor);
                cursor += sizeof(int);

                if (cursor + length > buf.Length)
                    return -1;

                //Arch.Console.Write("UnmarshalDataEntries: offset=");
                //Arch.Console.Write(offset);
                //Arch.Console.Write(" length=");
                //Arch.Console.Write(length);
                //Arch.Console.WriteLine();

                //BinderIPCMarshaler.DumpBuf(marshaledPtr, length); 
                var b = buf.Slice(cursor, length);

                if ((current.Parent.binderVMStart + offset).Write(current, b) != 0)
                    return -1;

                cursor += length;
                i++;
            }
            return 0;
        }

        // Handling unpatch buffer(i.e. offset relative to the start of the buffer

        private static int PatchReadBuffer(Thread current, ByteBufferRef marshaledBuffer)
        {
            var cursor = 0;
            while (cursor < marshaledBuffer.Length)
            {
                if (cursor + sizeof(uint) > marshaledBuffer.Length)
                    return -ErrorCode.ENOMEM;

                /*
                 * Here the code extend the value from uint to ulong to work around a bug
                 * in code contract.
                 * 
                 * See http://social.msdn.microsoft.com/Forums/en-US/codecontracts/thread/ca123a3e-6e0d-4045-9caf-a95b49df223e
                 * for more details.
                 */
                var cmd = (ulong)Deserializer.ReadUInt(marshaledBuffer, cursor);
                cursor += sizeof(uint);

                switch (cmd)
                {
                    case BinderINode.BR_INCREFS:
                    case BinderINode.BR_ACQUIRE:
                    case BinderINode.BR_RELEASE:
                    case BinderINode.BR_DECREFS:
                        cursor += 2 * sizeof(uint);
                        break;

                    case BinderINode.BR_NOOP:
                    case BinderINode.BR_TRANSACTION_COMPLETE:
                    case BinderINode.BR_SPAWN_LOOPER:
                        break;

                    case BinderINode.BR_TRANSACTION:
                    case BinderINode.BR_REPLY:
                        {
                            if (cursor + binder_transaction_data.Size > marshaledBuffer.Length)
                                return -ErrorCode.ENOMEM;

                            var tr = binder_transaction_data.Deserialize(marshaledBuffer, cursor);

                            var ret = PatchReadTransaction(current, new Pointer(marshaledBuffer.Location), ref tr);
                            if (ret < 0)
                                return ret;

                            tr.Write(marshaledBuffer, cursor);
                            cursor += binder_transaction_data.Size;
                            break;
                        }
                    default:
                        Arch.Console.Write("binder::patchReadBuffer: unsupported IPC primitive ");
                        Arch.Console.Write(cmd);
                        Arch.Console.WriteLine();
                        return -1;
                }
            }
            return 0;
        }

        private static int PatchReadTransaction(Thread current, Pointer marshaledBufferStart, ref binder_transaction_data tr)
        {
            if (tr.data_size != 0)
                tr.data_buffer += current.Parent.binderVMStart.Value.ToUInt32();

            if (tr.offsets_size != 0)
                tr.data_offsets += current.Parent.binderVMStart.Value.ToUInt32();

            if (GainingWindowFocus(current, tr))
                Globals.SecurityManager.OnActiveProcessChanged(current.Parent);

            for (var off_ptr = tr.data_offsets; off_ptr < tr.data_offsets + tr.offsets_size; off_ptr += 4)
            {
                var fp = new flat_binder_object();
                int off;
                if (off_ptr.Read(current, out off) != 0)
                {
                    Arch.Console.Write("Can't get offset");
                    return -1;
                }

                var fp_ptr = tr.data_buffer + off;
                if (fp_ptr.Read(current, out fp) != 0)
                {
                    Arch.Console.Write("Read fp failed, ptr:");
                    Arch.Console.Write(fp_ptr.Value.ToInt32());
                    Arch.Console.WriteLine();
                    return -1;
                }

                //Arch.Console.Write("off_ptr:");
                //Arch.Console.Write(tr->data_offsets.Value.ToUInt32());
                //Arch.Console.Write(" Off end:");
                //Arch.Console.Write((tr->data_offsets + tr->offsets_size).Value.ToUInt32());
                //Arch.Console.WriteLine();

                switch (fp.type)
                {
                    case BinderINode.BINDER_TYPE_FD:
                        {
                            var proc = current.Parent;
                            var linux_fd = fp.binderOrHandle.Value.ToInt32();
                            
                            GenericINode inode;
                            if (IsScreenSharingTransaction(current, ref tr))
                                inode = new ScreenBufferINode(linux_fd, proc.helperPid);
                            else
                                inode = new BinderSharedINode(linux_fd, proc.helperPid);

                            int fd = proc.GetUnusedFd();
                            proc.InstallFd(fd, new File(proc, inode, FileSystem.O_RDWR, 0));
                            // Patch the data structure
                            (fp_ptr + flat_binder_object.OFFSET_OF_HANDLE).Write(current, fd);
                        }
                        break;

                    case BinderINode.BINDER_TYPE_HANDLE:
                        // sliently ignore it (it seems safe)
                        break;

                    default:
                        Arch.Console.Write("BinderINode::PatchReadTransaction ignoring ");
                        Arch.Console.Write(fp.type);
                        Arch.Console.WriteLine();
                        break;
                }
            }

            return 0;
        }

        /*
         * Heuristic for determing whether a transaction is mapping the screen buffer
         * into the process.
         * 
         * See frameworks/base/libs/ui/GraphicBuffer.cpp for more details.
         */
        private static bool IsScreenSharingTransaction(Thread current, ref binder_transaction_data tr)
        {
            int a0, a1, a2;
            const uint SystemEuid = 1000;
            if (tr.sender_euid != SystemEuid || tr.data_size != 0x54 || tr.offsets_size != 4)
                return false;

            tr.data_buffer.Read(current, out a0);
            (tr.data_buffer + sizeof(int)).Read(current, out a1);
            (tr.data_buffer + sizeof(int) * 2).Read(current, out a2);

            return a0 == 0x3c && a1 == 0x1 && a2 == 0x47424652;
        }

        /*
         * Testing whether the current process is getting the focus.
         * 
         * See frameworks/base/core/java/android/view/IWindow.aidl and
         * frameworks/base/services/java/com/android/server/WindowManagerService.java
         * for more details.
         */
        private static bool GainingWindowFocus(Thread current, binder_transaction_data tr)
        {
            const int OP_windowFocusChanged = 5;
            if (tr.code != OP_windowFocusChanged || tr.data_size != 0x3c)
                return false;

            var buf = InspectionBuffer;
            tr.data_buffer.Read(current, buf, buf.Length);

            bool header_matched = true;
            for (var i = 0; i < WindowFocusChangedHeader.Length && header_matched; ++i)
            {
                if (buf[i] != WindowFocusChangedHeader[i])
                    header_matched = false;
            }

            if (!header_matched)
                return false;

            int get_focus;
            (tr.data_buffer + WindowFocusChangedHeader.Length).Read(current, out get_focus);
            return get_focus == 1;
        }
    };
}
