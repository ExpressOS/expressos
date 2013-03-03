using System;
using System.Diagnostics.Contracts;
using System.Runtime.InteropServices;

namespace ExpressOS.Kernel
{
    internal sealed partial class SecureFSInode : Arch.ArchINode
    {
        private static class NativeMethods
        {
            [DllImport("glue")]
            internal static extern int sfs_initialize_and_verify_metadata(Pointer header, int header_length, int size_on_disk,
                out int data_pgoffset, out uint real_file_size, out byte[] signatures);
        }

        internal struct WriteBackEntry
        {
            internal Pointer page;
            internal int pgoffset;
        }

        private uint OnDiskFileSize;
        private uint OnDiskBlock;
        internal uint FileSize { get; private set; }
        private readonly CachePageHolder Pages;
      
        private const ulong HeaderMagic = 0x56414e44524f4944UL;
        private const int DEFAULT_DATA_PGOFFSET = 1;
        private int DataPageOffset;
        private byte[] Signatures;


        // 160-bits signature for SHA-1
        internal const int HMACSize = (int)SHA1Managed.SHA1HashSize;
        private const int MinimumHeaderSize = Arch.ArchDefinition.PageSize * DEFAULT_DATA_PGOFFSET;
        private const int HMACOffsetInFile = sizeof(ulong);
        private const int DataPageOffsetInFile = HMACOffsetInFile + HMACSize;
        private const int FileSizeOffsetInFile = DataPageOffsetInFile + sizeof(int);
      
        internal static SecureFSInode Create(Thread current, ByteBufferRef header, int linux_fd, int size_on_disk, ref int p_ret)
        {
            var proc = current.Parent;

            int data_pgoffset;
            byte[] page_signatures;
            uint real_file_size;

            var ret = NativeMethods.sfs_initialize_and_verify_metadata(new Pointer(header.Location), header.Length, size_on_disk,
                out data_pgoffset, out real_file_size, out page_signatures);

            var metadata_verified = ret == 0;
            if (!metadata_verified)
            {
                Arch.Console.WriteLine("SFSINode::Failed to verify metadata");
                p_ret = ret;
                return null;
            }

            var access_permission_checked = Globals.SecurityManager.CanAccessFile(current, header);
            if (!access_permission_checked)
                return null;

            var arch_inode = new Arch.ArchINode(linux_fd, (uint)size_on_disk, proc.helperPid);

            Contract.Assert(metadata_verified && access_permission_checked);

            var inode = new SecureFSInode(linux_fd, (uint)size_on_disk, proc.helperPid, real_file_size);
            inode.DataPageOffset = data_pgoffset;
            inode.Signatures = page_signatures;
            return inode;
        }

        public static unsafe void CalculateHMAC(int pg_offset, uint filesize, byte[] page_signatures, byte *result)
        {
            var b = CalculateHMAC(pg_offset, filesize, page_signatures);
            var buf = new ByteBufferRef(new IntPtr(result), (int)SHA1Managed.SHA1HashSize);
            buf.CopyFrom(0, b);
        }

        private static unsafe byte[] CalculateHMAC(int pg_offset, uint filesize, byte[] page_signatures)
        {
            var sha1 = new SHA1Managed();
            var b1 = new ByteBufferRef(new IntPtr(&pg_offset), sizeof(int));
            var b2 = new ByteBufferRef(new IntPtr(&filesize), sizeof(uint));
            sha1.Input(SecureFS.HMACSecretKey);
            sha1.Input(b1);
            sha1.Input(b2);

            sha1.Input(page_signatures);

            var r = sha1.GetResult();
            sha1.Reset();
            sha1.Input(SecureFS.HMACSecretKey);
            sha1.Input(r);

            return sha1.GetResult();
        }

        internal bool VerifyPage(CachePage page)
        {
            var sha1 = new SHA1Managed();
            sha1.Input(page.Buffer);
            var r = sha1.GetResult();

            var l = page.Location * HMACSize;
            for (var i = 0; i < HMACSize; ++i)
            {
                if (Signatures[l + i] != r[i])
                {
                    Arch.Console.WriteLine("SFSINode::Failed to verify page");
                    return false;
                }
            }
            return true;
        }

        private int PrepareBuffer(ByteBufferRef buf, CachePage[] sealed_page)
        {
            var cursor = 0;

            // Metadata
            for (var i = 0; i < DataPageOffset; ++i)
            {
                Deserializer.WriteInt(i, buf, cursor);
                cursor += sizeof(int);
            }

            for (var i = 0; i < sealed_page.Length; ++i)
            {
                var pgoffset = sealed_page[i].Location;
                Deserializer.WriteInt(pgoffset + DataPageOffset, buf, cursor);
                cursor += sizeof(int);
                if (pgoffset > MaximumPageOffset())
                {
                    Arch.Console.WriteLine("SecureFSINode::FlushAndCloseAsync, file too big");
                    Utils.Panic();
                    return -1;
                }
            }

            var metadata_cursor = cursor;
            cursor += DataPageOffset * Arch.ArchDefinition.PageSize;

            // Copy data
            var sha1 = new SHA1Managed();
            for (var i = 0; i < sealed_page.Length; ++i)
            {
                var page = sealed_page[i];
                buf.CopyFrom(cursor, page.Buffer);
                sha1.Reset();
                sha1.Input(page.Buffer);
                var r = sha1.GetResult();

                for (var j = 0; j < r.Length; ++j)
                    Signatures[page.Location * HMACSize + j] = r[j];

                page.Dispose();
                cursor += Arch.ArchDefinition.PageSize;
            }

            SerializeMetadata(buf, metadata_cursor);
            return 0;
        }

        private int MaximumPageOffset()
        {
            return Signatures.Length / HMACSize;
        }

        private void SerializeMetadata(ByteBufferRef buf, int cursor)
        {
            Deserializer.WriteULong(HeaderMagic, buf, cursor);
            cursor += sizeof(ulong);
            var hmac = CalculateHMAC(DataPageOffset, FileSize, Signatures);
            buf.CopyFrom(cursor, hmac);
            cursor += HMACSize;
            Deserializer.WriteInt(DataPageOffset, buf, cursor);
            cursor += sizeof(int);
            Deserializer.WriteUInt(FileSize, buf, cursor);
            cursor += sizeof(uint);
            Deserializer.WriteInt(Signatures.Length, buf, cursor);
            cursor += sizeof(int);
            buf.CopyFrom(cursor, Signatures);
        }

        private SecureFSInode(int linux_fd, uint size_on_disk, int helperPid, uint real_file_size)
            : base(linux_fd, size_on_disk, helperPid, INodeKind.SecureFSINodeKind)
        {
            this.OnDiskFileSize = size_on_disk;
            this.OnDiskBlock = Arch.ArchDefinition.PageAlign(size_on_disk) >> Arch.ArchDefinition.PageShift;
            this.FileSize = real_file_size;
            this.Pages = new CachePageHolder();
        }

        internal int SFSClose()
        {
            var metadata_pages = DataPageOffset;
            var cached_pages = Pages.Length;
            var buf = AllocateWriteBackBuffer(metadata_pages, cached_pages);

            Utils.Assert(buf.isValid);
            if (!buf.isValid)
            {
                Arch.Console.WriteLine("SFSClose:Out of memory");
                Utils.Panic();
            }

            CachePage[] sealed_pages = Pages.Seal();

            var ret = PrepareBuffer(buf, sealed_pages);
            if (ret != 0)
                return ret;

            var completion = new SFSFlushCompletion(Fd, buf);
            Globals.CompletionQueue.Enqueue(completion);
            
            ret = Arch.IPCStubs.ScatterWritePageAsync(helperPid, completion.handle, new Pointer(buf.Location), Fd, sealed_pages.Length + DataPageOffset);

            if (ret != 0)
                return ret;

            Arch.IPCStubs.Close(helperPid, Fd);
            return 0;
        }

        private static ByteBufferRef AllocateWriteBackBuffer(int metadata_pages, int cached_pages)
        {
            // Info for pgoffset
            var info_pages = (int)(Arch.ArchDefinition.PageAlign((uint)((metadata_pages + cached_pages) * sizeof(int))) / Arch.ArchDefinition.PageSize);
            var buf = Globals.AllocateAlignedCompletionBuffer((info_pages + metadata_pages + cached_pages) * Arch.ArchDefinition.PageSize);
            return buf;
        }

        internal int SFSWrite(Thread current, ref Arch.ExceptionRegisters regs, ByteBufferRef buf, int len, uint pos, File file)
        {
            var writtenBytes = Write(current, buf, len, pos);
            if (writtenBytes <= 0)
                return writtenBytes;

            file.position = (uint)(pos + writtenBytes);
            return writtenBytes;
        }

        internal void LoadPageRaw(CachePage cachedPage)
        {
            Contract.Requires(cachedPage.CurrentState == CachePage.State.Empty);
            Contract.Ensures(cachedPage.CurrentState == CachePage.State.Encrypted);

            var pos = (uint)DataPageIndex(cachedPage.Location) * Arch.ArchDefinition.PageSize;
            int ret = Arch.IPCStubs.Read(helperPid, Fd, new Pointer(cachedPage.Buffer.Location), Arch.ArchDefinition.PageSize, ref pos);
            
            cachedPage.CurrentState = CachePage.State.Encrypted;
        }

        private int DataPageIndex(int p)
        {
            return DataPageOffset + p;
        }

        internal int SFSRead(Thread current, ref Arch.ExceptionRegisters regs, UserPtr userBuf, int len, uint pos, File file)
        {
            var readBytes = Read(current, userBuf, len, pos);
            if (readBytes <= 0)
                return readBytes;
            
            if (file != null)
                file.position = (uint)(pos + readBytes);
            
            return readBytes;
        }

        internal int SFSFStat64(Thread current, UserPtr buf)
        {
            var ret = Arch.IPCStubs.linux_sys_fstat64(helperPid, Fd);
            if (ret < 0)
                return ret;

            FileSystem.SetSizeFromStat64(Globals.LinuxIPCBuffer, FileSize);

            if (buf.Write(current, new Pointer(Globals.LinuxIPCBuffer.Location), GenericINode.SIZE_OF_STAT64) != 0)
                return -ErrorCode.EFAULT;

            return 0;
        }

        internal int ftruncate(int length)
        {
            if (length < 0)
                return -ErrorCode.EINVAL;

            var pages = Arch.ArchDefinition.PageAlign((uint)length) / Arch.ArchDefinition.PageSize;

            var ret = Arch.IPCStubs.Ftruncate(helperPid, Fd, DataPageIndex((int)pages) * Arch.ArchDefinition.PageSize);

            if (ret < 0)
                return ret;

            TruncateUnusedPages(length);
            FileSize = (uint)length;

            return 0;
        }

        private void TruncateUnusedPages(int length)
        {
            if (length >= FileSize)
                return;

            var loc = (int)Arch.ArchDefinition.PageIndex((uint)length) / Arch.ArchDefinition.PageSize;

            var page = Pages.Truncate(loc);

            if (page != null)
            {
                var cursor = Arch.ArchDefinition.PageOffset(length);
                page.Buffer.ClearAfter(cursor);
            }
        }

        private static int WriteUserBuffer(Thread current, UserPtr ptr, int buf_offset, CachePage page, int page_offset, int len)
        {
            return (ptr + buf_offset).Write(current, new Pointer(page.Buffer.Location + page_offset), len);
        }

        private static int ReadUserBuffer(Thread current, ByteBufferRef src, int buf_offset, CachePage page, int page_offset, int len)
        {
            var dst = new ByteBufferRef(page.Buffer.Location + page_offset, len);
            dst.CopyFrom(buf_offset, src);
            return 0;
        }
    }

    public sealed class SFSFlushCompletion : GenericCompletionEntry
    {
        internal readonly int archfd;
        internal readonly ByteBufferRef buf;
     
        internal SFSFlushCompletion(int archfd, ByteBufferRef buf)
            : base(Kind.SFSFlushCompletionKind, Globals.CompletionQueue.NextFreeHandle())
        {
            this.archfd = archfd;
            this.buf = buf;
        }

        public void Dispose()
        {
            Globals.CompletionQueueAllocator.FreePages(new Pointer(buf.Location), buf.Length >> Arch.ArchDefinition.PageShift);
        }
    }
}
