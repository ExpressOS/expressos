using System.Diagnostics.Contracts;
namespace ExpressOS.Kernel
{
    internal class Parcel
    {
        internal byte[] Buffer { get; private set; }
        internal int cursor { get; private set; }

        [ContractInvariantMethod]
        private void ObjectInvariant()
        {
            Contract.Invariant(cursor >= 0);
        }

        internal Parcel()
        {
            Contract.Ensures(Buffer == null && cursor == 0);
        }

        internal void WriteString16(string s)
        {
            var r = new ByteBufferRef(Buffer);

            if (cursor + sizeof(int) > Buffer.Length)
                return;

            WriteInt32(s.Length);

            foreach (var c in s)
            {
                if (cursor + sizeof(short) > r.Length)
                    return;
                Deserializer.WriteShort((short)c, r, cursor);
                cursor += sizeof(short);
            }

            if (cursor + sizeof(short) > r.Length)
                return;

            Deserializer.WriteShort(0, r, cursor);
            cursor += sizeof(short);
            Pad();
        }

        internal void WriteInt32(int v)
        {
            // Fail sliently
            if (cursor + sizeof(int) > Buffer.Length)
                return;

            var r = new ByteBufferRef(Buffer);
            Deserializer.WriteInt(v, r, cursor);
            cursor += sizeof(int);
        }

        internal void AddLengthInt32(int v)
        {
            cursor += sizeof(int);
         }

        internal void AddLengthString16(string s)
        {
            cursor += sizeof(int) + (s.Length + 1) * sizeof(char);
            Pad();
        }

        static int Padding(int cursor)
        {
            int p = 0;
            switch (cursor % 4)
            {
                case 1:
                    p = 3;
                    break;
                case 2:
                    p = 2;
                    break;
                case 3:
                    p = 1;
                    break;
                default:
                    break;
            }
            return p;
        }

        internal void AllocateBuffer()
        {
            Buffer = new byte[cursor];
            cursor = 0;
        }

        void Pad()
        {
            Contract.Ensures(cursor >= Contract.OldValue(cursor));

            var p = Padding(cursor);
            if (Buffer != null && cursor + p >= Buffer.Length)
                return;

            cursor += p;
        }
    }
}
