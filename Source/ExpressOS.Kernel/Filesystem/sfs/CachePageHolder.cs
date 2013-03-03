using System.Diagnostics.Contracts;

namespace ExpressOS.Kernel
{
    internal class CachePageHolder
    {
        internal CachePage Head;
        internal int Length { get; set; }

        internal CachePageHolder()
        {
        }

        internal void Add(CachePage page)
        {
            Contract.Requires(page != null && (page.CurrentState == CachePage.State.Empty || page.CurrentState == CachePage.State.Decrypted));
            Contract.Requires(page.Next == null);

            if (Head == null)
            {
                Head = page;
                ++Length;
                return;
            }

            var prev = LookupPrev(page.Location);
            if (prev == null)
            {
                page.Next = Head;
                Head = page;
                ++Length;
                return;
            }

            Utils.Assert(prev.Location != page.Location);

            page.Next = prev.Next;
            prev.Next = page;
            ++Length;
        }

        internal CachePage[] Seal()
        {
            Contract.Ensures(Head == null && Length == 0);

            var ret = new CachePage[Length];
            CachePage current = Head;
            var i = 0;
            while (current != null)
            {
                // Proven by dafny
                Contract.Assume(current.CurrentState == CachePage.State.Decrypted || current.CurrentState == CachePage.State.Empty); 
                current.Encrypt();
                ret[i] = current;
                current = current.Next;
                i = i + 1;
            }

            Head = null;
            Length = 0;

            return ret;
        }

        internal CachePage Truncate(int loc)
        {
            CachePage ret = null;
            if (Head == null)
            {
                return null;
            }

            var prev = LookupPrev(loc);
            if (prev == null)
            {
                var current = Head;
                while (current != null)
                {
                    current.Dispose();
                    current = current.Next;
                }
                Head = null;
                Length = 0;
                return null;
            }
            else
            {
                ret = prev.Location == loc ? prev : null;

                var page = prev.Next;
                while (page != null)
                {
                    page.Dispose();
                    prev.Next = page.Next;
                    page = page.Next;
                    Length = Length - 1;
                }
                return ret;
            }
        }

        [Pure]
        internal CachePage Lookup(int idx)
        {
            if (Head == null)
            {
                return null;
            }

            var r = LookupPrev(idx);
            if (r == null)
            {
                return null;
            }

            return r.Location == idx ? r : null;
        }

        [Pure]
        private CachePage LookupPrev(int idx)
        {
            Contract.Requires(Head != null);

            var current = Head;
            CachePage prev = null;
            while (current != null && idx >= current.Location) 
            {
                prev = current;
                current = current.Next;
            }
            return prev;
        }
    }
}
