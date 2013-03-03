using System.Diagnostics.Contracts;

namespace ExpressOS.Kernel
{
    public struct IOVector
    {
        public UserPtr iov_base;
        public int iov_len;

        public const int Size = 8;
        public static IOVector Deserialize(byte[] buf, int offset)
        {
            Contract.Requires(offset >= 0);
            Contract.Requires(offset + Size <= buf.Length);

            IOVector r;
            r.iov_base = new UserPtr(Deserializer.ReadUInt(buf, offset)); offset += sizeof(uint);
            r.iov_len = Deserializer.ReadInt(buf, offset);
            return r;
        }
    }

    #region Binder IPC
    struct binder_write_read
    {
        public readonly int write_size;     /* bytes to write */
        public int write_consumed; /* bytes consumed by driver */
        public readonly UserPtr write_buffer;
        public readonly int read_size;      /* bytes to read */
        public int read_consumed;  /* bytes consumed by driver */
        public readonly UserPtr read_buffer;
    };

    /*                                                                                                                                                                                        
     * This is the flattened representation of a Binder object for transfer                                                                                                                   
     * between processes.  The 'offsets' supplied as part of a binder transaction                                                                                                             
     * contains offsets into the data where these structures occur.  The Binder                                                                                                               
     * driver takes care of re-writing the structure type and data as it moves                                                                                                                
     * between processes.                                                                                                                                                                     
     */
    public struct flat_binder_object
    {
        /* 8 bytes for large_flat_header. */
        public uint type;
        public uint flags;

        public UserPtr binderOrHandle;

        /* extra data associated with local object */
        public UserPtr cookie;
        public const int OFFSET_OF_HANDLE = 8;
    };


    public struct binder_transaction_data
    {
        /* The first two are only used for bcTRANSACTION and brTRANSACTION,                                                                                                 
         * identifying the target and contents of the transaction.                                                                                                          
         */
        /* target descriptor of command transaction (handle) */
        /* target descriptor of return transaction (ptr) */
        public UserPtr HandleOrPtr;
        public UserPtr cookie;        /* target object cookie */
        public uint code;           /* transaction command */

        /* General information about the transaction. */
        public uint flags;
        public uint sender_pid;
        public uint sender_euid;
        public uint data_size;      /* number of bytes of data */
        public uint offsets_size;   /* number of bytes of offsets */

        /* If this transaction is inline, the data immediately                                                                                                              
         * follows here; otherwise, it ends with a pointer to                                                                                                               
         * the data buffer.                                                                                                                                                 
         */
        /* transaction data */
        public UserPtr data_buffer;
        public UserPtr data_offsets;

        /* Alternatively, buffer + offset could be an inlined buf*/
        /* uint8_t buf[8]; */
        #region Constants
        public const int DATA_BUFFER_OFFSET = 32;
        public const int DATA_OFFSETS_OFFSET = 36;
        public const int Size = 40;
        #endregion

        public static binder_transaction_data Deserialize(ByteBufferRef buf, int offset)
        {
            Contract.Requires(offset >= 0);
            Contract.Requires(offset + Size <= buf.Length);

            binder_transaction_data r;
            r.HandleOrPtr = new UserPtr(Deserializer.ReadUInt(buf, offset)); offset += sizeof(uint);
            r.cookie = new UserPtr(Deserializer.ReadUInt(buf, offset)); offset += sizeof(uint);
            r.code = Deserializer.ReadUInt(buf, offset); offset += sizeof(uint);
            r.flags = Deserializer.ReadUInt(buf, offset); offset += sizeof(uint);
            r.sender_pid = Deserializer.ReadUInt(buf, offset); offset += sizeof(uint);
            r.sender_euid = Deserializer.ReadUInt(buf, offset); offset += sizeof(uint);
            r.data_size = Deserializer.ReadUInt(buf, offset); offset += sizeof(uint);
            r.offsets_size = Deserializer.ReadUInt(buf, offset); offset += sizeof(uint);
            r.data_buffer = new UserPtr(Deserializer.ReadUInt(buf, offset)); offset += sizeof(uint);
            r.data_offsets = new UserPtr(Deserializer.ReadUInt(buf, offset)); offset += sizeof(uint);
            return r;
        }

        public void Write(ByteBufferRef buf, int offset)
        {
            Contract.Requires(offset >= 0);
            Contract.Requires(offset + Size <= buf.Length);

            Deserializer.WriteUInt(HandleOrPtr.Value.ToUInt32(), buf, offset); offset += sizeof(uint);
            Deserializer.WriteUInt(cookie.Value.ToUInt32(), buf, offset); offset += sizeof(uint);
            Deserializer.WriteUInt(code, buf, offset); offset += sizeof(uint);
            Deserializer.WriteUInt(flags, buf, offset); offset += sizeof(uint);
            Deserializer.WriteUInt(sender_pid, buf, offset); offset += sizeof(uint);
            Deserializer.WriteUInt(sender_euid, buf, offset); offset += sizeof(uint);
            Deserializer.WriteUInt(data_size, buf, offset); offset += sizeof(uint);
            Deserializer.WriteUInt(offsets_size, buf, offset); offset += sizeof(uint);
            Deserializer.WriteUInt(data_buffer.Value.ToUInt32(), buf, offset); offset += sizeof(uint);
            Deserializer.WriteUInt(data_offsets.Value.ToUInt32(), buf, offset); offset += sizeof(uint);
        }
    }
    #endregion
}
