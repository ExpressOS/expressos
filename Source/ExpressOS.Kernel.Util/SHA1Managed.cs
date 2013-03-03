/*
 *  sha1.c, from http://www.ietf.org/rfc/rfc3174.txt
 *
 *  Description:
 *      This file implements the Secure Hashing Algorithm 1 as
 *      defined in FIPS PUB 180-1 published April 17, 1995.
 *
 *      The SHA-1, produces a 160-bit message digest for a given
 *      data stream.  It should take about 2**n steps to find a
 *      message with the same digest as a given message and
 *      2**(n/2) to find any two messages with the same digest,
 *      when n is the digest size in bits.  Therefore, this
 *      algorithm can serve as a means of providing a
 *      "fingerprint" for a message.
 *
 *  Portability Issues:
 *      SHA-1 is defined in terms of 32-bit "words".  This code
 *      uses <stdint.h> (included via "sha1.h" to define 32 and 8
 *      bit unsigned integer types.  If your C compiler does not
 *      support 32 bit unsigned integers, this code is not
 *      appropriate.
 *
 *  Caveats:
 *      SHA-1 is designed to work with messages less than 2^64 bits
 *      long.  Although SHA-1 allows a message digest to be generated
 *      for messages of any number of bits less than 2^64, this
 *      implementation only works with messages with a length that is
 *      a multiple of the size of an 8-bit character.
 *
 */
using System.Diagnostics.Contracts;

namespace ExpressOS.Kernel
{
    public sealed class SHA1Managed
    {
        public const uint SHA1HashSize = 20;
        private uint[] Intermediate_Hash; /* Message Digest  */
        /* Constants defined in SHA-1   */
        private static uint[] K;

        uint Length_Low;            /* Message length in bits      */
        uint Length_High;           /* Message length in bits      */

        /* Index into message block array   */
        short Message_Block_Index;
        byte[] Message_Block;      /* 512-bit message blocks      */

        bool Computed;               /* Is the digest computed?         */
        bool Corrupted;             /* Is the message digest corrupted? */

        public byte[] GetResult()
        {
            return SHA1Result();
        }

        public static void Initialize()
        {
            K = new uint[] { 0x5A827999, 0x6ED9EBA1, 0x8F1BBCDC, 0xCA62C1D6 };
        }

        /*
        *  SHA1Reset
        *
        *  Description:
        *      This function will initialize the SHA1Context in preparation
        *      for computing a new SHA1 message digest.
        *
        *  Parameters:
        *      context: [in/out]
        *          The context to reset.
        *
        *  Returns:
        *      sha Error Code.
        *
        */
        public void Reset()
        {
            Intermediate_Hash = new uint[] { 0x67452301, 0xEFCDAB89, 0x98BADCFE, 0x10325476, 0xC3D2E1F0 };
            Message_Block = new byte[64];
            Length_Low = 0;
            Length_High = 0;
            Message_Block_Index = 0;
            Computed = false;
            Corrupted = false;
        }

        public SHA1Managed()
        {
            Reset();
        }

        /*
         *  Define the SHA1 circular left shift macro
         */
        private static uint SHA1CircularShift(ushort bits, uint word)
        {
            return (((word) << (bits)) | ((word) >> (32 - (bits))));
        }


        /*
         *  SHA1Result
         *
         *  Description:
         *      This function will return the 160-bit message digest into the
         *      Message_Digest array  provided by the caller.
         *      NOTE: The first octet of hash is stored in the 0th element,
         *            the last octet of hash in the 19th element.
         *
         *  Parameters:
         *      context: [in/out]
         *          The context to use to calculate the SHA-1 hash.
         *      Message_Digest: [out]
         *          Where the digest is returned.
         *
         *  Returns:
         *      sha Error Code.
         *
         */
        private byte[] SHA1Result()
        {
            byte[] Message_Digest = new byte[SHA1HashSize];

            if (Corrupted)
                return null;

            if (!Computed)
            {
                SHA1PadMessage();
                for (int i = 0; i < 64; ++i)
                {
                    /* message may be sensitive, clear it out */
                    Message_Block[i] = 0;
                }
                Length_Low = 0;    /* and clear length */
                Length_High = 0;
                Computed = true;
            }

            for (int i = 0; i < SHA1HashSize; ++i)
            {
                Message_Digest[i] = (byte)(Intermediate_Hash[i >> 2] >> 8 * (3 - (i & 0x03)));
            }

            return Message_Digest;
        }

        /*
         *  SHA1Input
         *
         *  Description:
         *      This function accepts an array of octets as the next portion
         *      of the message.
         *
         *  Parameters:
         *      context: [in/out]
         *          The SHA context to update
         *      message_array: [in]
         *          An array of characters representing the next portion of
         *          the message.
         *      length: [in]
         *          The length of the message in message_array
         *
         *  Returns:
         *      sha Error Code.
         *
         */
        public int Input(byte[] message_array)
        {
            return InputPartialBuf(message_array, 0, message_array.Length);
        }


        public int InputPartialBuf(byte[] message_array, int offset, int length)
        {
            if (message_array == null || length == 0 || message_array.Length < offset + length)
                return 0;

            if (Computed)
            {
                Corrupted = true;
                return -1;
            }

            if (Corrupted)
            {
                return -1;
            }

            int i = offset;
            while (length-- != 0 && !Corrupted)
            {
                Message_Block[Message_Block_Index++] = message_array[i];

                Length_Low += 8;
                if (Length_Low == 0)
                {
                    Length_High++;
                    if (Length_High == 0)
                    {
                        /* Message is too long */
                        Corrupted = true;
                    }
                }

                if (Message_Block_Index == 64)
                {
                    SHA1ProcessMessageBlock();
                }

                i++;
            }
            return 0;
        }

        // Duplicated from Input(byte[])
        public int Input(ByteBufferRef message_array)
        {
            if (message_array.Length == 0)
                return 0;

            var length = message_array.Length;

            if (Computed)
            {
                Corrupted = true;
                return -1;
            }

            if (Corrupted)
            {
                return -1;
            }

            int i = 0;
            Contract.Assert(i + length == message_array.Length);
            while (length > 0 && !Corrupted)
            {
                Contract.Assert(i >= 0);
                Contract.Assert(i + length == message_array.Length);
                Message_Block[Message_Block_Index++] = message_array.Get(i);

                Length_Low += 8;
                if (Length_Low == 0)
                {
                    Length_High++;
                    if (Length_High == 0)
                    {
                        /* Message is too long */
                        Corrupted = true;
                    }
                }

                if (Message_Block_Index == 64)
                {
                    SHA1ProcessMessageBlock();
                }

                i++;
                length--;
            }
            return 0;
        }

        /*
         *  SHA1ProcessMessageBlock
         *
         *  Description:
         *      This function will process the next 512 bits of the message
         *      stored in the Message_Block array.
         *
         *  Parameters:
         *      None.
         *
         *  Returns:
         *      Nothing.
         *
         *  Comments:
         *      Many of the variable names in this code, especially the
         *      single character names, were used because those were the
         *      names used in the publication.
         *
         */
        private void SHA1ProcessMessageBlock()
        {
            int t;                 /* Loop counter                */
            uint temp;              /* Temporary word value        */
            uint[] W = new uint[80];             /* Word sequence               */
            uint A, B, C, D, E;     /* Word buffers                */

            /*
             *  Initialize the first 16 words in the array W
             */
            for (t = 0; t < 16; t++)
            {
                W[t] = (uint)Message_Block[t * 4] << 24;
                W[t] |= (uint)Message_Block[t * 4 + 1] << 16;
                W[t] |= (uint)Message_Block[t * 4 + 2] << 8;
                W[t] |= (uint)Message_Block[t * 4 + 3];
            }

            for (t = 16; t < 80; t++)
            {
                W[t] = SHA1CircularShift(1, W[t - 3] ^ W[t - 8] ^ W[t - 14] ^ W[t - 16]);
            }

            A = Intermediate_Hash[0];
            B = Intermediate_Hash[1];
            C = Intermediate_Hash[2];
            D = Intermediate_Hash[3];
            E = Intermediate_Hash[4];

            for (t = 0; t < 20; t++)
            {
                temp = SHA1CircularShift(5, A) +
                        ((B & C) | ((~B) & D)) + E + W[t] + K[0];
                E = D;
                D = C;
                C = SHA1CircularShift(30, B);
                B = A;
                A = temp;
            }

            for (t = 20; t < 40; t++)
            {
                temp = SHA1CircularShift(5, A) + (B ^ C ^ D) + E + W[t] + K[1];
                E = D;
                D = C;
                C = SHA1CircularShift(30, B);
                B = A;
                A = temp;
            }

            for (t = 40; t < 60; t++)
            {
                temp = SHA1CircularShift(5, A) +
                       ((B & C) | (B & D) | (C & D)) + E + W[t] + K[2];
                E = D;
                D = C;
                C = SHA1CircularShift(30, B);
                B = A;
                A = temp;
            }

            for (t = 60; t < 80; t++)
            {
                temp = SHA1CircularShift(5, A) + (B ^ C ^ D) + E + W[t] + K[3];
                E = D;
                D = C;
                C = SHA1CircularShift(30, B);
                B = A;
                A = temp;
            }

            Intermediate_Hash[0] += A;
            Intermediate_Hash[1] += B;
            Intermediate_Hash[2] += C;
            Intermediate_Hash[3] += D;
            Intermediate_Hash[4] += E;

            Message_Block_Index = 0;
        }


        /*
         *  SHA1PadMessage
         *
         *  Description:
         *      According to the standard, the message must be padded to an even
         *      512 bits.  The first padding bit must be a '1'.  The last 64
         *      bits represent the length of the original message.  All bits in
         *      between should be 0.  This function will pad the message
         *      according to those rules by filling the Message_Block array
         *      accordingly.  It will also call the ProcessMessageBlock function
         *      provided appropriately.  When it returns, it can be assumed that
         *      the message digest has been computed.
         *
         *  Parameters:
         *      context: [in/out]
         *          The context to pad
         *      ProcessMessageBlock: [in]
         *          The appropriate SHA*ProcessMessageBlock function
         *  Returns:
         *      Nothing.
         *
         */

        private void SHA1PadMessage()
        {
            /*
             *  Check to see if the current message block is too small to hold
             *  the initial padding bits and length.  If so, we will pad the
             *  block, process it, and then continue padding into a second
             *  block.
             */
            if (Message_Block_Index > 55)
            {
                Message_Block[Message_Block_Index++] = 0x80;
                while (Message_Block_Index < 64)
                {
                    Message_Block[Message_Block_Index++] = 0;
                }

                SHA1ProcessMessageBlock();

                while (Message_Block_Index < 56)
                {
                    Message_Block[Message_Block_Index++] = 0;
                }
            }
            else
            {
                Message_Block[Message_Block_Index++] = 0x80;
                while (Message_Block_Index < 56)
                {
                    Message_Block[Message_Block_Index++] = 0;
                }
            }

            /*
             *  Store the message length as the last 8 octets
             */
            Message_Block[56] = (byte)(Length_High >> 24);
            Message_Block[57] = (byte)(Length_High >> 16);
            Message_Block[58] = (byte)(Length_High >> 8);
            Message_Block[59] = (byte)(Length_High);
            Message_Block[60] = (byte)(Length_Low >> 24);
            Message_Block[61] = (byte)(Length_Low >> 16);
            Message_Block[62] = (byte)(Length_Low >> 8);
            Message_Block[63] = (byte)(Length_Low);

            SHA1ProcessMessageBlock();
        }

    }
}
