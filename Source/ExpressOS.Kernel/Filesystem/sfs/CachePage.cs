using System;
using System.Diagnostics.Contracts;

namespace ExpressOS.Kernel
{
    internal class CachePage
    {
        private readonly Process Owner;
        internal readonly int Location;
        internal readonly ByteBufferRef Buffer;
        internal CachePage Next;

        internal enum State
        {
            Empty,
            Loaded,
            Verified,
            Decrypted,
            Encrypted,
        }

        internal State CurrentState;

        private CachePage(Process owner, int location, ByteBufferRef buf)
        {
            Contract.Requires(location >= 0);
            Contract.Ensures(Owner == owner);
            Contract.Ensures(Location == location);
            Contract.Ensures(CurrentState == State.Empty);
            Contract.Ensures(Next == null);

            this.Owner = owner;
            this.Location = location;
            this.Buffer = buf;
            this.Next = null;
            this.CurrentState = State.Empty;
        }

        internal static CachePage Allocate(Process owner, int loc)
        {
            Contract.Requires(loc >= 0);
            Contract.Ensures(Contract.Result<CachePage>().CurrentState == State.Empty);
            Contract.Ensures(Contract.Result<CachePage>().Next == null);

            var buf = AllocFreeBuffer();
            var p = new CachePage(owner, loc, buf);
            return p;
        }

        private static ByteBufferRef AllocFreeBuffer()
        {
            var ret = Globals.PageAllocator.AllocPage();
            ret.Clear();
            return ret;
        }

        internal bool Load(SecureFSInode inode)
        {
            Contract.Requires(CurrentState == State.Empty);
            Contract.Ensures(Next == Contract.OldValue(Next));
            Contract.Ensures(!Contract.Result<bool>() || CurrentState == State.Decrypted);

            inode.LoadPageRaw(this);
            
            if (!Verify(inode))
                return false;

            Decrypt();
            return true;
        }

        internal bool Writable()
        {
            return CurrentState == State.Empty || CurrentState == State.Verified;
        }

        internal void Dispose()
        {
            Globals.PageAllocator.FreePage(new Pointer(Buffer.Location));
        }

        internal bool Verify(SecureFSInode inode)
        {
            Contract.Requires(CurrentState == State.Encrypted);
            Contract.Ensures(!Contract.Result<bool>() || CurrentState == State.Verified);
            Contract.Ensures(Contract.Result<bool>() || CurrentState == Contract.OldValue(CurrentState));
            Contract.Ensures(Next == Contract.OldValue(Next));

            var r = inode.VerifyPage(this);
            if (r)
            {
                CurrentState = State.Verified;
            }
            return r;
        }

        internal void Encrypt()
        {
            Contract.Requires(CurrentState == State.Empty || CurrentState == State.Decrypted);
            Contract.Ensures(CurrentState == State.Encrypted);

            var aes = new AESManaged();
            aes.SetEncryptKey(Owner.Credential.SFSEncryptKey, 128);
            Contract.Assert(Arch.ArchDefinition.PageSize % AESManaged.AES_BLOCK_SIZE == 0);

            for (int i = 0; i < Arch.ArchDefinition.PageSize / AESManaged.AES_BLOCK_SIZE; ++i)
            {
                var block = Buffer.Slice(i * AESManaged.AES_BLOCK_SIZE, AESManaged.AES_BLOCK_SIZE);
                aes.Encrypt(block, block);
            }

            CurrentState = State.Encrypted;
        }

        internal void Decrypt()
        {
            Contract.Requires(CurrentState == State.Verified);
            Contract.Ensures(CurrentState == State.Decrypted);
            Contract.Ensures(Next == Contract.OldValue(Next));

            var aes = new AESManaged();
            aes.SetDecryptKey(Owner.Credential.SFSEncryptKey, 128);
            Contract.Assert(Arch.ArchDefinition.PageSize % AESManaged.AES_BLOCK_SIZE == 0);

            for (int i = 0; i < Arch.ArchDefinition.PageSize / AESManaged.AES_BLOCK_SIZE; ++i)
            {
                var block = Buffer.Slice(i * AESManaged.AES_BLOCK_SIZE, AESManaged.AES_BLOCK_SIZE);
                aes.Decrypt(block, block);
            }

            CurrentState = State.Decrypted;
        }
    }
}

