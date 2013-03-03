using System.Diagnostics.Contracts;

namespace ExpressOS.Kernel
{
    internal partial class SecureFSInode
    {
        private int Read(Thread current, UserPtr userBuf, int len, uint pos)
        {
            int readBytes = 0;
            int remainedBytes = len;
            if (FileSize - pos < remainedBytes)
            {
                remainedBytes = (int)(FileSize - pos);
            }
            int currentPageIndex = (int)Arch.ArchDefinition.PageIndex(pos);
            var page = Pages.Lookup(currentPageIndex / Arch.ArchDefinition.PageSize);

            while (remainedBytes > 0)
            {
                if (page == null)
                {
                    page = CachePage.Allocate(current.Parent, currentPageIndex / Arch.ArchDefinition.PageSize);
                    var succeed = page.Load(this);
                    if (!succeed)
                    {
                        return -ErrorCode.EIO;
                    }

                    Contract.Assert(page != null && (page.CurrentState == CachePage.State.Empty || page.CurrentState == CachePage.State.Decrypted));
                    Pages.Add(page);
                }

                var pageCursor = (int)((pos + readBytes) % Arch.ArchDefinition.PageSize);
                var chunkLen = Arch.ArchDefinition.PageSize - pageCursor < remainedBytes ? Arch.ArchDefinition.PageSize - pageCursor : remainedBytes;
                var left = WriteUserBuffer(current, userBuf, readBytes, page, pageCursor, chunkLen);

                readBytes += chunkLen - left;
                if (left != 0)
                {
                    return -ErrorCode.EFAULT;
                }

                remainedBytes = remainedBytes - chunkLen;
                currentPageIndex = currentPageIndex + Arch.ArchDefinition.PageSize;
                page = Pages.Lookup(currentPageIndex / Arch.ArchDefinition.PageSize);
            }
            return readBytes;
        }

        private int Write(Thread current, ByteBufferRef buf, int len, uint pos)
        {
            var writtenBytes = 0;
            var remainedBytes = len;
            var currentPageIndex = (int)Arch.ArchDefinition.PageIndex(pos);
            CachePage page = null;
            while (remainedBytes > 0)
            {
                page = Pages.Lookup(currentPageIndex / Arch.ArchDefinition.PageSize);
                if (page == null)
                {
                    page = CachePage.Allocate(current.Parent, currentPageIndex / Arch.ArchDefinition.PageSize);

                    int currentBlockId = currentPageIndex / Arch.ArchDefinition.PageSize;

                    if (currentBlockId < OnDiskBlock)
                    {
                        // Case (1)
                        var succeed = page.Load(this);
                        Contract.Assert(page.Next == null);
                        if (!succeed)
                        {
                            return -ErrorCode.EIO;
                        }
                    }
                    else
                    {
                        // Case (2) / (3)
                        // assert cachedPage.Empty();
                    }
                    Pages.Add(page);
                }

                // Copying
                int pageCursor = Arch.ArchDefinition.PageOffset((int)(pos + writtenBytes));
                int chunkLen = Arch.ArchDefinition.PageSize - pageCursor < remainedBytes ? Arch.ArchDefinition.PageSize - pageCursor : remainedBytes;

                var left = ReadUserBuffer(current, buf, writtenBytes, page, pageCursor, chunkLen);

                writtenBytes += chunkLen - left;
                if (left != 0)
                {
                    return -ErrorCode.EFAULT;
                }

                remainedBytes -= chunkLen;
                // Update FileSize
                if (pos + writtenBytes > FileSize)
                {
                    FileSize = (uint)(pos + writtenBytes);
                }

                currentPageIndex += Arch.ArchDefinition.PageSize;
            }
            return writtenBytes;
        }

        private WriteBackEntry[] PrepareFlush(out CachePage[] pages)
        {
            pages = Pages.Seal();
            var ret = new WriteBackEntry[pages.Length];
            for (var i = 0; i < pages.Length; ++i)
            {
                ret[i].page = new Pointer(pages[i].Buffer.Location);
                ret[i].pgoffset = pages[i].Location;
            }
            return ret;
        }
    }
}
