namespace ExpressOS.Kernel
{
    internal sealed class BinderINode : GenericINode
    {
        static BinderINode inst;
        internal static BinderINode Instance
        {
            get
            {
                if (inst == null)
                    inst = new BinderINode();

                return inst;
            }
        }

        BinderINode()
            : base(INodeKind.BinderINodeKind)
        { }

        // 8K of marshaling / unmarhsaling buffer
        internal const int MARSHAL_BUF_PAGES = 2;

        internal int Ioctl(Thread current, ref Arch.ExceptionRegisters pt_regs, uint cmd, UserPtr userBuf)
        {
            switch (cmd)
            {
                case BINDER_WRITE_READ:
                    return HandleWriteRead(current, userBuf, ref pt_regs);

                case BINDER_VERSION:
                    if (userBuf.Write(current, BINDER_CURRENT_PROTOCOL_VERSION) != 0)
                        return -ErrorCode.EINVAL;

                    return 0;

                case BINDER_SET_IDLE_TIMEOUT:
                case BINDER_SET_MAX_THREADS:
                case BINDER_SET_IDLE_PRIORITY:
                case BINDER_SET_CONTEXT_MGR:
                case BINDER_THREAD_EXIT:
                    // skipping
                    return 0;

                default:
                    return -ErrorCode.EINVAL;
            }
        }

        private int HandleWriteRead(Thread current, UserPtr userBwr, ref Arch.ExceptionRegisters pt_regs)
        {
            var bwr = new binder_write_read();

            if (userBwr.Read(current, out bwr) != 0)
                return -ErrorCode.EFAULT;

            if (bwr.write_size > 0 || bwr.read_size > 0)
            {
                var ret = HandleWriteRead(current, ref pt_regs, userBwr, bwr);
                if (ret < 0)
                {
                    bwr.read_consumed = 0;
                    if (userBwr.Write(current, ref bwr) != 0)
                        return -ErrorCode.EFAULT;
                }
            }

            if (userBwr.Write(current, ref bwr) != 0)
                return -ErrorCode.EFAULT;

            return 0;
        }

        private int HandleWriteRead(Thread current, ref Arch.ExceptionRegisters regs, UserPtr userBwr, binder_write_read bwr)
        {
            var writeBuf = bwr.write_buffer;
            var writeSize = bwr.write_size;
            var buf = Globals.AllocateAlignedCompletionBuffer(MARSHAL_BUF_PAGES * Arch.ArchDefinition.PageSize);
            if (!buf.isValid)
                return -ErrorCode.ENOMEM;
           
            var marshaler = new BinderIPCMarshaler(current, buf);

            if (bwr.write_consumed != 0)
            {
                Arch.Console.WriteLine("BinderINode::HandleWriteRead: write_consumed != 0");
                Utils.Panic();
            }

            var r = marshaler.Marshal(writeBuf, writeSize);
            if (r < 0)
            {
                Arch.Console.WriteLine("Marshaling error");
                Globals.CompletionQueueAllocator.FreePages(new Pointer(buf.Location), MARSHAL_BUF_PAGES);
                return -1;
            }

            sys_binder_write_desc desc;
            desc.buffer_size = marshaler.WriteCursor;
            desc.bwr_write_size = writeSize;
            desc.write_buffer = new Pointer(buf.Location);
            desc.patch_table_entries = marshaler.CurrentPatchEntry;
            desc.patch_table_offset = marshaler.PatchTableOffset;
            desc.read_consumed = 0;
            desc.write_consumed = 0;
            
            var binder_cp = new BinderCompletion(current, userBwr, desc, buf);
            
            r = Arch.IPCStubs.linux_sys_binder_write_read_async(current.Parent.helperPid, current.impl._value.thread._value, desc);

            if (r < 0)
            {
                binder_cp.Dispose();
                return r;
            }

            Globals.CompletionQueue.Enqueue(binder_cp);
            current.SaveState(ref regs);
            current.AsyncReturn = true;
            return 0;
        }

        internal void HandleAsyncCall(BinderCompletion entry, int retval, int write_consumed, int read_consumed, int buffer_size, int data_entries)
        {
            var current = entry.thr;

            int ret = retval;

            if (retval < 0)
            {
                entry.Dispose();
                current.ReturnFromCompletion(retval);
                return;
            }

            var bwr = new binder_write_read();
            entry.userBwrBuf.Read(current, out bwr);

            var desc = entry.desc;

            bwr.write_consumed = write_consumed;
            bwr.read_consumed = read_consumed;
            desc.read_consumed = read_consumed;
            desc.patch_table_entries = data_entries;
            desc.buffer_size = buffer_size;

            int r = 0;
            r = ReadBufferUnmarshaler.UnmarshalReadBuffer(current, entry.buf, ref desc, bwr.read_buffer, bwr.read_consumed);
            
            if (r < 0)
            {
                Arch.Console.WriteLine("UnmarshalReadBuffer failed");
                ret = r;
            }

            if (entry.userBwrBuf.Write(current, ref bwr) != 0)
            {
                Arch.Console.WriteLine("entry.userBwrBuf.Write failed");
                ret = -ErrorCode.EFAULT;
            }

            entry.Dispose();
            current.ReturnFromCompletion(ret);
            return;
        }


        #region Constants

        internal const int BINDER_VERSION_SIZE = 4;

        internal const int BINDER_CURRENT_PROTOCOL_VERSION = 7;

        internal const uint BINDER_WRITE_READ = 0xc0186201;
        internal const uint BINDER_SET_IDLE_TIMEOUT = 0x40086203;
        internal const uint BINDER_SET_MAX_THREADS = 0x40046205;
        internal const uint BINDER_SET_IDLE_PRIORITY = 0x40046206;
        internal const uint BINDER_SET_CONTEXT_MGR = 0x40046207;
        internal const uint BINDER_THREAD_EXIT = 0x40046208;
        internal const uint BINDER_VERSION = 0xc0046209;
        internal const uint BR_ERROR = 0x80047200;
        internal const uint BR_OK = 0x00007201;
        internal const uint BR_TRANSACTION = 0x80287202;
        internal const uint BR_REPLY = 0x80287203;
        internal const uint BR_ACQUIRE_RESULT = 0x80047204;
        internal const uint BR_DEAD_REPLY = 0x00007205;
        internal const uint BR_TRANSACTION_COMPLETE = 0x00007206;
        internal const uint BR_INCREFS = 0x80087207;
        internal const uint BR_ACQUIRE = 0x80087208;
        internal const uint BR_RELEASE = 0x80087209;
        internal const uint BR_DECREFS = 0x8008720a;
        internal const uint BR_ATTEMPT_ACQUIRE = 0x800c720b;
        internal const uint BR_NOOP = 0x0000720c;
        internal const uint BR_SPAWN_LOOPER = 0x0000720d;
        internal const uint BR_FINISHED = 0x0000720e;
        internal const uint BR_DEAD_BINDER = 0x8004720f;
        internal const uint BR_CLEAR_DEATH_NOTIFICATION_DONE = 0x80047210;
        internal const uint BR_FAILED_REPLY = 0x00007211;
        internal const uint BC_TRANSACTION = 0x40286300;
        internal const uint BC_REPLY = 0x40286301;
        internal const uint BC_ACQUIRE_RESULT = 0x40046302;
        internal const uint BC_FREE_BUFFER = 0x40046303;
        internal const uint BC_INCREFS = 0x40046304;
        internal const uint BC_ACQUIRE = 0x40046305;
        internal const uint BC_RELEASE = 0x40046306;
        internal const uint BC_DECREFS = 0x40046307;
        internal const uint BC_INCREFS_DONE = 0x40086308;
        internal const uint BC_ACQUIRE_DONE = 0x40086309;
        internal const uint BC_ATTEMPT_ACQUIRE = 0x4008630a;
        internal const uint BC_REGISTER_LOOPER = 0x0000630b;
        internal const uint BC_ENTER_LOOPER = 0x0000630c;
        internal const uint BC_EXIT_LOOPER = 0x0000630d;
        internal const uint BC_REQUEST_DEATH_NOTIFICATION = 0x4008630e;
        internal const uint BC_CLEAR_DEATH_NOTIFICATION = 0x4008630f;
        internal const uint BC_DEAD_BINDER_DONE = 0x40046310;
        internal const uint BINDER_TYPE_FD = 0x66642a85;
        internal const uint BINDER_TYPE_HANDLE = 0x73682a85;
        #endregion


    }

   

    
}
