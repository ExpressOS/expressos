using ExpressOS.Kernel;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;

namespace ExpressOS.Kernel
{
    public class SecurityManager
    {
        public Process ActiveProcess { get; private set; }

        internal SecurityManager()
        {
            ActiveProcess = null;
        }

        [ContractInvariantMethod]
        private void ObjectInvariantMethod()
        {
            Contract.Invariant(ActiveProcess == null || ActiveProcess.ScreenEnabled);
        }

        public void OnActiveProcessChanged(Process newActiveProcess)
        {
            Contract.Requires(newActiveProcess != null);
            Contract.Ensures(ActiveProcess == newActiveProcess);

            if (ActiveProcess == newActiveProcess)
                return;

            if (ActiveProcess != null)
            {
                if (ActiveProcess.ScreenEnabled)
                    DisableScreen();

                ActiveProcess = null;
            }

            EnableScreen(newActiveProcess);
        }

        private void DisableScreen()
        {
            Contract.Requires(ActiveProcess != null);
            Contract.Requires(ActiveProcess.ScreenEnabled);
            Contract.Ensures(ActiveProcess == null);
            Contract.Ensures(!Contract.OldValue(ActiveProcess).ScreenEnabled);
           
            for (var r = ActiveProcess.Space.Head; r != null; r = r.Next)
            {
                if (r.BackingFile == null
                    || r.BackingFile.inode.kind != GenericINode.INodeKind.ScreenBufferINodeKind)
                    continue;

                r.UpdateAccessRights(ActiveProcess.Space, MemoryRegion.FAULT_READ);
            }
            ActiveProcess.ScreenEnabled = false;
            ActiveProcess = null;
        }

        private void EnableScreen(Process proc)
        {
            Contract.Requires(proc != null);
            Contract.Requires(ActiveProcess == null);
            Contract.Ensures(ActiveProcess == proc);
            Contract.Ensures(ActiveProcess.ScreenEnabled);

            for (var r = proc.Space.Head; r != null; r = r.Next)
            {
                if (r.BackingFile == null
                    || r.BackingFile.inode.kind != GenericINode.INodeKind.ScreenBufferINodeKind)
                    continue;

                r.UpdateAccessRights(proc.Space, MemoryRegion.FAULT_WRITE | MemoryRegion.FAULT_READ);
            }
            ActiveProcess = proc;
            ActiveProcess.ScreenEnabled = true;
        }

        internal bool CanAccessFile(Thread current, ByteBufferRef header)
        {
            return true;
        }

        internal bool CanCreateVBinderChannel(Thread current, int label, int permission)
        {
            return true;
        }

        /*
         * Load the credential for an application.
         * 
         * Currently ExpressOS does not deal with key management, etc.
         * It uses a static key for all application.
         */
        internal static Credential GetCredential(ASCIIString name, Process process)
        {
            Contract.Ensures(Contract.Result<Credential>().GhostOwner == process);
            var key = new byte[] { 0xc8, 0x43, 0xae, 0x85, 0x9b, 0x63, 0x4b, 0x72, 0x1b, 0x14, 0xcf, 0x6d, 0xa8, 0xf9, 0x6f, 0x1d };

            // 1003 == AID_GRAPHICS
            return new Credential(process, 1003, key);
        }
    }
}
