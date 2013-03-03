using System.Diagnostics.Contracts;
namespace ExpressOS.Kernel
{
    internal class Credential
    {
        public readonly int Uid;
        public Process GhostOwner { get; private set; }
        internal byte[] SFSEncryptKey { get; private set; }

        internal Credential(Process owner, int uid, byte[] encryptKey)
        {
            Contract.Ensures(GhostOwner == owner);
            Contract.Ensures(SFSEncryptKey == encryptKey);

            this.Uid = uid;
            this.GhostOwner = owner;
            this.SFSEncryptKey = encryptKey;
        }
    }
}
