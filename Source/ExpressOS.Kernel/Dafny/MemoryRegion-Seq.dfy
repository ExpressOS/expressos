class Pointer {}
class ProcessInfo {}
class GenericINode {}
class File
{
    var inode: GenericINode;
    var GhostOwner: ProcessInfo;
}

class MemoryRegion
{
    var Access: nat;
    var Flags: int;
    var BackingFile: File;
    var FileOffset: nat;
    var FileSize: int;
    var StartAddress: nat;
    var Size: int;
    var Next: MemoryRegion;
    var IsFixed: bool;
    var GhostOwner: ProcessInfo;

    constructor Init(owner: ProcessInfo, access: nat, flags: int, file: File, fileOffset: nat, fileSize: int, vaddr: nat, size: int, isFixed: bool)
    requires size > 0;
    requires 0 <= fileSize <= size;
    requires file != null ==> fileSize > 0 && file.GhostOwner == owner;
    requires file == null ==> fileSize == 0 && fileOffset == 0;
    modifies this;
    ensures Valid();
    ensures GhostOwner == owner;
    ensures FileSize == fileSize;
    ensures StartAddress == vaddr;
    ensures Size == size;
    ensures Access == access;
    {
        this.Access := access;
        this.Flags := flags;
        this.BackingFile := file;
        this.FileOffset := fileOffset;
        this.FileSize := fileSize;
        this.StartAddress := vaddr;
        this.Size := size;
        this.Next := null;
        this.IsFixed := isFixed;
        this.GhostOwner := owner;
    }

    function Valid() : bool
    reads this, BackingFile;
    {
        Size > 0 && 0 <= FileSize <= Size
        && (BackingFile != null ==> FileSize > 0 && BackingFile.GhostOwner == GhostOwner)
        && (BackingFile == null ==> FileSize == 0 && FileOffset == 0)
    }

    function method End() : int
    reads this;
    { StartAddress + Size }

    function method FileEnd() : int
    reads this;
    { FileOffset + FileSize }

    function method Overlapped(rhs: MemoryRegion) : bool
    requires rhs != null;
    requires Valid();
    requires rhs.Valid();
    reads this, rhs;
    ensures Overlapped(rhs) <==> !(End() <= rhs.StartAddress || rhs.End() <= StartAddress);
    { OverlappedInt(rhs.StartAddress, rhs.Size) }

    function method OverlappedInt(start: nat, size: int) : bool
    requires size > 0;
    requires Valid();
    reads this;
    ensures OverlappedInt(start, size) <==> !(End() <= start || start + size <= StartAddress);
    { !(End() <= start || start + size <= StartAddress) }

    method CutRight(size: int)
    requires Valid();
    requires 0 < size < Size;
    modifies this`Size, this`FileSize;
    ensures Size == old(Size) - size;
    ensures Valid();
    {
        Size := Size - size;
        if (FileSize > Size)
        {
            FileSize := Size;
        }
    }

    method CutLeft(size: nat)
    requires Valid();
    requires 0 < size < Size;
    modifies this`FileOffset, this`StartAddress, this`FileSize, this`Size, this`BackingFile;
    ensures Size == old(Size) - size;
    ensures StartAddress == old(StartAddress) + size;
    ensures End() == old(End());
    ensures Valid();
    ensures BackingFile == old(BackingFile) || BackingFile == null;
    {
        FileOffset := FileOffset + size;
        StartAddress := StartAddress + size;
        Size := Size - size;
        if (FileSize <= size)
        {
            FileSize := 0;
            FileOffset := 0;
            BackingFile := null;
        }
        else
        {
            FileSize := FileSize - size;
        }
    }

    method Expand(r: MemoryRegion)
    requires r != null;
    requires Valid() && r.Valid();
    requires CanMerge(this, r);
    modifies this`Size, this`FileSize;
    ensures Valid();
    ensures Size == old(Size) + r.Size;
    ensures BackingFile != null ==> FileSize == old(FileSize) + r.FileSize;
    {
        Size := Size + r.Size;
        if (BackingFile != null)
        {
            FileSize := FileSize + r.FileSize;
        }
    }

    static function method CanMerge(prev: MemoryRegion, next: MemoryRegion) : bool
    requires prev != null && next != null;
    reads prev, prev.BackingFile, next, next.BackingFile;
    ensures CanMerge(prev, next) ==> prev.End() == next.StartAddress;
    {
        (prev.BackingFile == next.BackingFile ||
          (prev.BackingFile != null && next.BackingFile != null && prev.BackingFile.inode == next.BackingFile.inode))
        && prev.Access == next.Access
        && prev.End() == next.StartAddress
        && (prev.BackingFile == null || prev.FileEnd() == next.FileOffset)
    }

    method UpdateAccessRights(space: AddressSpace, newaccess: nat)
    requires Valid();
    modifies this`Access;
    ensures Valid();
    ensures Access == newaccess;

}

class AddressSpace
{
    var Head: MemoryRegion;
    var GhostOwner: ProcessInfo;

    ghost var Repr: set<object>;
    ghost var Contents: seq<MemoryRegion>;

    function Valid() : bool
    reads this, Repr;
    {
        null !in Repr && this in Repr
        && (forall i :: 0 <= i < |Contents| ==> Contents[i] != null &&
          Contents[i] in Repr && (Contents[i].BackingFile != null ==> Contents[i].BackingFile in Repr) &&
          Contents[i].GhostOwner == GhostOwner && Contents[i].Valid())
        && Head != null && |Contents| > 0 && Head == Contents[0] && Head.StartAddress == 0

        && (forall i :: 0 <= i < |Contents| - 1 ==> Contents[i].Next == Contents[i + 1] && Contents[i].End() <= Contents[i + 1].StartAddress)
        && Contents[|Contents| - 1].Next == null
        && LemmaRecNoOverlaps(0)
    }

    ghost method LemmaInsertOrMerge(r: MemoryRegion, idx: nat)
    requires Valid();
    requires 0 <= idx < |Contents| && Contents[idx] == r;
    ensures Valid();
    ensures r.Next != null ==> idx < |Contents| - 1 && r.Next == Contents[idx + 1];
    ensures r.Next == null ==> idx == |Contents| - 1;
    ensures 0 <= idx < |Contents| && Contents[idx] == r;
    {}

    function LemmaRecNoOverlaps(idx: nat) : bool
    reads this, Contents;
    requires 0 <= idx < |Contents|;
    requires forall i :: idx <= i < |Contents| ==> Contents[i] != null && Contents[i].Valid();
    requires forall i :: idx <= i < |Contents| - 1 ==> Contents[i].End() <= Contents[i + 1].StartAddress;
    ensures LemmaRecNoOverlaps(idx);
    ensures forall j :: idx < j < |Contents| ==> (Contents[idx].End() <= Contents[j].StartAddress && !Contents[idx].Overlapped(Contents[j]));
    ensures forall i, j :: idx <= i < j < |Contents| ==> (Contents[i] != Contents[j] && Contents[j].StartAddress >= Contents[i].End());
    decreases |Contents| - idx;
    {
          0 <= idx < |Contents| &&
          if idx == |Contents| - 1 then true
          else (LemmaRecNoOverlaps(idx + 1) && Contents[idx].StartAddress < Contents[idx].End() <= Contents[idx + 1].StartAddress)
    }

    function LemmaRecPivotLeft(idx : nat) : bool
    reads this, Contents;
    requires 0 <= idx < |Contents|;
    requires forall i :: 0 <= i <= idx ==> Contents[i] != null && Contents[i].Valid();
    requires forall i :: 0 <= i <= idx - 1 ==> Contents[i].End() <= Contents[i + 1].StartAddress;
    ensures LemmaRecPivotLeft(idx);
    ensures forall i :: 0 <= i < idx ==> Contents[i].End() <= Contents[idx].StartAddress;
    decreases idx;
    {
          0 <= idx < |Contents| &&
          if idx == 0 then true
          else (LemmaRecPivotLeft(idx - 1) && Contents[idx - 1].StartAddress <= Contents[idx].End())
    }

    function LemmaRecPivotRight(idx : nat) : bool
    reads this, Contents;
    requires 0 <= idx < |Contents|;
    requires forall i :: idx <= i < |Contents| ==> Contents[i] != null && Contents[i].Valid();
    requires forall i :: idx <= i < |Contents| - 1 ==> Contents[i].End() <= Contents[i + 1].StartAddress;
    ensures LemmaRecPivotRight(idx);
    ensures forall i :: idx < i < |Contents| ==> Contents[idx].End() <= Contents[i].StartAddress;
    decreases |Contents| - idx;
    {
          0 <= idx < |Contents| &&
          if idx == |Contents| - 1 then true
          else (LemmaRecPivotRight(idx + 1) && Contents[idx].End() <= Contents[idx + 1].StartAddress)
    }

    method RemoveNode(prev: MemoryRegion, r: MemoryRegion, ghost prev_idx: nat)
    requires Valid();
    requires 0 <= prev_idx < |Contents| - 1 && Contents[prev_idx] == prev;
    requires r != null && Contents[prev_idx + 1] == r;
    modifies prev`Next, this`Contents, this`Repr;
    ensures Valid();
    ensures Contents == old(Contents[..prev_idx + 1] + Contents[prev_idx + 2..]);
    ensures prev.Next == r.Next;
    ensures prev.Valid();
    {
        assert Contents[|Contents| - 1] != prev;

        prev.Next := r.Next;

        assert Contents[prev_idx + 1] == r;
        Contents := Contents[..prev_idx + 1] + Contents[prev_idx + 2..];
        assert r !in Contents;

        Repr := Repr - {r};

        assert (forall i :: 0 <= i < |Contents| ==> Contents[i] != null &&
          Contents[i] in Repr && (Contents[i].BackingFile != null ==> Contents[i].BackingFile in Repr) &&
          Contents[i].GhostOwner == GhostOwner && Contents[i].Valid());
    }

    method InsertNode(prev: MemoryRegion, r: MemoryRegion, ghost prev_idx: nat)
    requires Valid();
    requires 0 <= prev_idx < |Contents| && Contents[prev_idx] == prev;
    requires r != null && r !in Contents;
    requires r.Valid() && r.GhostOwner == GhostOwner;
    requires prev.End() <= r.StartAddress;
    requires prev.Next != null ==> r.End() <= prev.Next.StartAddress;
    modifies prev`Next, r`Next, this`Contents, this`Repr;
    ensures Valid();
    ensures Contents == old(Contents[..prev_idx + 1] + [r] + Contents[prev_idx + 1..]);
    ensures prev.Next == r;
    ensures r.Next == old(prev.Next);
    {
        r.Next := prev.Next;
        prev.Next := r;

        Contents := Contents[..prev_idx + 1] + [r] + Contents[prev_idx + 1..];

        Repr := Repr + {r};
        if (r.BackingFile != null)
        {
            Repr := Repr + {r.BackingFile};
        }

        assert Contents[prev_idx + 1] == r;
        assert r in Repr && (r.BackingFile != null ==> r.BackingFile in Repr);
        assert r.Valid() && r.GhostOwner == GhostOwner;

        assert (forall i :: 0 <= i < |Contents| ==> Contents[i] != null &&
          Contents[i] in Repr && (Contents[i].BackingFile != null ==> Contents[i].BackingFile in Repr) &&
          Contents[i].GhostOwner == GhostOwner && Contents[i].Valid());
    }

    method TryMergeWithNext(r: MemoryRegion, ghost idx: nat) returns (ret: bool)
    requires Valid();
    requires 0 <= idx < |Contents| && Contents[idx] == r;
    requires r.Next != null ==> idx < |Contents| - 1 && r.Next == Contents[idx + 1];
    requires r.Next == null ==> idx == |Contents| - 1;
    modifies r`Next, r`Size, r`FileSize;
    modifies this`Contents, this`Repr;
    ensures ret <==> (old(r.Next) != null && old(MemoryRegion.CanMerge(r, r.Next)));
    ensures !ret ==> Contents == old(Contents);
    ensures !ret ==> (r.Next == old(r.Next) && r.Size == old(r.Size) && r.FileSize == old(r.FileSize));
    ensures ret ==> Contents == old(Contents[..idx + 1] + Contents[idx + 2..]);
    ensures ret ==> old(r.Next) != null && r.Next == old(r.Next.Next);
    ensures ret ==> Contents[idx] == r && Contents[idx].End() == old(r.Next.End());
    ensures Valid();
    {
        var next := r.Next;
        if (next != null && MemoryRegion.CanMerge(r, next))
        {
            RemoveNode(r, next, idx);
            r.Expand(next);
            return true;
        }
        else
        {
            return false;
        }
    }

    method InsertOrMerge(prev: MemoryRegion, r: MemoryRegion, next: MemoryRegion, ghost prev_idx: int)
    requires prev != null && r != null;
    requires Valid();
    requires 0 <= prev_idx < |Contents| && Contents[prev_idx] == prev;
    requires r !in Contents;
    requires r.Valid() && r.GhostOwner == GhostOwner;
    requires prev.Next == next;
    requires prev.End() <= r.StartAddress;
    requires next != null ==> r.End() <= next.StartAddress;
    modifies r`Next, r`FileSize, r`Size;
    modifies prev`Next, prev`FileSize, prev`Size;
    modifies this`Contents, this`Repr;
    ensures Valid();
    ensures 0 <= prev_idx < |Contents|;
    ensures (Contents[prev_idx].OverlappedInt(r.StartAddress, old(r.Size)) && Contents[prev_idx].Access == r.Access)
      || (prev_idx + 1 < |Contents| && Contents[prev_idx + 1].OverlappedInt(r.StartAddress, old(r.Size)) && Contents[prev_idx + 1].Access == r.Access);
    {
        if (MemoryRegion.CanMerge(prev, r))
        {
            prev.Expand(r);
            assert Valid();
            LemmaInsertOrMerge(prev, prev_idx);
            var dummy := TryMergeWithNext(prev, prev_idx);
            return;
        }

        InsertNode(prev, r, prev_idx);
        var dummy := TryMergeWithNext(r, prev_idx + 1);
    }

    method Find(address: nat) returns (ret: MemoryRegion)
    requires Valid();
    ensures Valid();
    ensures ret != null ==> ret.Valid() && ret.GhostOwner == GhostOwner;
    {
        var h := Head;
        ghost var idx := 0;
        while (h != null)
        invariant h != null ==> idx < |Contents| && Contents[idx] == h;
        invariant h != null ==> h.Valid() && h.GhostOwner == GhostOwner;
        decreases |Contents| - idx;
        {
            if (h.StartAddress <= address && address < h.StartAddress + h.Size)
            {
                return h;
            }
            h := h.Next;
            idx := idx + 1;
        }
        return null;
    }

    method Insert(r: MemoryRegion)
    requires r != null && r.GhostOwner == GhostOwner;
    requires Valid() && r.Valid();
    requires forall x :: x in Contents ==> !x.Overlapped(r);
    requires r !in Contents;
    modifies r`Next, r`Size, r`FileSize;
    modifies Contents, this`Contents, this`Repr;
    ensures Valid();
    ensures exists x :: x in Contents && x.Valid() && x.OverlappedInt(old(r.StartAddress), old(r.Size)) && x.Access == r.Access;
    {
        var h := Head.Next;
        var prev : MemoryRegion := Head;
        ghost var prev_idx := 0;

        while (h != null && h.End() <= r.StartAddress)
        invariant Valid();
        invariant prev != null && prev.Next == h;
        invariant 0 <= prev_idx < |Contents| && Contents[prev_idx] == prev;
        invariant prev.End() <= r.StartAddress;
        decreases |Contents| - prev_idx;
        {
            prev := h;
            h := h.Next;
            prev_idx := prev_idx + 1;
        }

        InsertOrMerge(prev, r, h, prev_idx);
        return;
    }

    method Split(r: MemoryRegion, offset: nat, ghost idx: nat) returns (ret: MemoryRegion)
    requires Valid();
    requires 0 <= idx < |Contents| && Contents[idx] == r;
    requires 0 < offset < r.Size;
    modifies r`FileSize, r`Next, r`Size;
    modifies this`Contents, this`Repr;
    ensures 0 <= idx < |Contents| && Contents[idx] == r;
    ensures ret != null && fresh(ret);
    ensures Contents == old(Contents[..idx + 1]) + [ret] + old(Contents[idx + 1..]);
    ensures r.Size == offset && ret.StartAddress == r.StartAddress + offset
            && ret.Size == old(r.Size - offset);
    ensures forall x :: x in Contents ==> (x in old(Contents) || fresh(x));
    ensures Valid();
    {
        if (offset >= r.Size)
        {
            return null;
        }

        var next: MemoryRegion := null;
        LemmaInsertOrMerge(r, idx);

        if (offset >= r.FileSize)
        {
            next := new MemoryRegion.Init(r.GhostOwner, r.Access, r.Flags, null, 0, 0, r.StartAddress + offset, r.Size - offset, r.IsFixed);
        }
        else
        {
            next := new MemoryRegion.Init(r.GhostOwner, r.Access, r.Flags, r.BackingFile, r.FileOffset + offset,
                                          r.FileSize - offset, r.StartAddress + offset, r.Size - offset, r.IsFixed);
        }

        r.CutRight(r.Size - offset);
        InsertNode(r, next, idx);
        return next;
    }

    method RemoveWorkingSet(vaddr: nat, size: int)
    requires Valid();
    ensures Valid();

    method UpdateAccessRights(r: MemoryRegion, newaccess: nat)
    requires Valid();
    requires r in Contents;
    modifies r`Access;
    ensures Valid();
    ensures r.Access == newaccess;
    {
        r.UpdateAccessRights(this, newaccess);
    }

    // Implement mmap().
    //
    // The specification of mmap() requires unmapping all segments specified in the parameters
    // before adding new mapping.
    // The dynamic linker relies on this behavior.
    //
    method AddMapping(access: nat, flags: nat, file: File, fileOffset: nat, fileSize: nat, vaddr: nat, memorySize: nat)
    returns (ret: int)
    requires file != null ==> file.GhostOwner == GhostOwner;
    requires memorySize > 0 && memorySize % 4096 == 0;
    requires vaddr > 0 && vaddr % 4096 == 0;
    requires 0 <= fileSize <= memorySize;
    requires file != null ==> fileSize > 0;
    requires file == null ==> fileSize == 0 && fileOffset == 0;
    requires Valid();
    modifies Contents, this`Contents, this`Repr;
    ensures Valid();
    ensures ret == 0 ==> exists x :: x in Contents && x.Valid() && x.OverlappedInt(vaddr, memorySize) && x.Access == access;
    {
        var r := RemoveMapping(vaddr, memorySize);
        if (r != 0)
        {
            return r;
        }

        var newRegion := new MemoryRegion.Init(GhostOwner, access, flags, file, fileOffset, fileSize, vaddr, memorySize, false);
        Insert(newRegion);
        return 0;
    }

    method UpdateAccessRightRange(start: nat, size: nat, access: nat) returns (ret: bool)
    requires Valid();
    requires start % 4096 == 0 && size % 4096 == 0;
    requires size > 0;
    modifies Contents, this`Contents, this`Repr;
    ensures Valid();
    {
        var prev := Head;
        var r := prev.Next;
        var end := start + size;

        ghost var prev_idx := 0;
        while (r != null && r.StartAddress < end)
        invariant Valid();
        invariant prev != null && prev.Next == r;
        invariant 0 <= prev_idx < |Contents| && Contents[prev_idx] == prev;
        invariant r != null ==> prev_idx + 1 < |Contents| && Contents[prev_idx + 1] == r;
        invariant forall x :: x in Contents ==> (x in old(Contents) || fresh(x));
        decreases |Contents| - prev_idx;
        {
            if (!r.OverlappedInt(start, size) || access == r.Access)
            {
                prev := r;
                r := r.Next;
                prev_idx := prev_idx + 1;
            }
            else
            {
                if (r.IsFixed)
                {
                    return false;
                }

                if (r.StartAddress < start)
                {
                    var region_end := r.End();
                    var middleRegion := Split(r, start - r.StartAddress, prev_idx + 1);

                    if (end < region_end)
                    {
                        // update middle region
                        prev := Split(middleRegion, size, prev_idx + 2);
                        UpdateAccessRights(middleRegion, access);
                        return true;
                    }
                    else
                    {
                        UpdateAccessRights(middleRegion, access);
                        var dummy := TryMergeWithNext(middleRegion, prev_idx + 2);
                        prev := middleRegion;
                        r := prev.Next;
                        prev_idx := prev_idx + 2;
                    }
                }
                else
                {
                    if (r.End() <= end)
                    {
                        UpdateAccessRights(r, access);
                        var merged := TryMergeWithNext(r, prev_idx + 1);
                        merged := TryMergeWithNext(prev, prev_idx);
                        if (merged)
                        {
                            r := prev.Next;
                        }
                        else
                        {
                            prev := prev.Next;
                            r := prev.Next;
                            prev_idx := prev_idx + 1;
                        }
                    }
                    else
                    {
                        var right_region := Split(r, end - r.StartAddress, prev_idx + 1);
                        UpdateAccessRights(r, access);
                        var merged := TryMergeWithNext(prev, prev_idx);
                        prev := right_region;
                        r := prev.Next;
                        prev_idx := if merged then prev_idx + 1 else prev_idx + 2;
                    }
                }
            }
        }
        return true;
    }

    method RemoveMapping(vaddr: nat, size: int) returns (ret: int)
    requires size > 0 && size % 4096 == 0;
    requires vaddr > 0 && vaddr % 4096 == 0;
    requires Valid();
    modifies Contents, this`Contents, this`Repr;
    ensures Valid();
    ensures ret == 0 ==> forall x :: x in Contents ==> !x.OverlappedInt(vaddr, size);
    ensures forall x :: x in Contents ==> (x in old(Contents) || fresh(x));
    {
        var prev : MemoryRegion;
        ghost var prev_idx : int;
        var end := vaddr + size;
        var c := false;
        var changed := false;

        ret, c, prev, prev_idx := RemoveMappingLeft(vaddr, size);
        changed := changed || c;
        if (ret <= 0)
        {
            if (changed) { RemoveWorkingSet(vaddr, size); }
            return ret;
        }

        ret, c, prev, prev_idx := RemoveMappingCenter(vaddr, size, prev, prev_idx);
        changed := changed || c;
        if (ret <= 0)
        {
            if (changed) { RemoveWorkingSet(vaddr, size); }
            return ret;
        }

        var r := prev.Next;
        changed := true;
        var s := end - r.StartAddress;
        r.CutLeft(s);

        prev := r;
        r := r.Next;
        prev_idx := prev_idx + 1;
        RemoveWorkingSet(vaddr, size);

        return 0;
    }

    method RemoveMappingLeft(vaddr: nat, size: int)
    returns (ret: int, changed: bool, prev: MemoryRegion, ghost prev_idx: int)
    requires size > 0 && size % 4096 == 0;
    requires vaddr > 0 && vaddr % 4096 == 0;
    requires Valid();
    modifies Contents, this`Contents, this`Repr;
    ensures Valid();
    ensures 0 <= prev_idx < |Contents| && Contents[prev_idx] == prev;
    ensures ret > 0 ==> prev.End() <= vaddr && (prev.Next != null ==> vaddr <= prev.Next.StartAddress);
    ensures ret >= 0 ==> forall j :: 0 <= j <= prev_idx ==> !Contents[j].OverlappedInt(vaddr, size);
    ensures ret == 0 ==> forall j :: 0 <= j < |Contents| ==> !Contents[j].OverlappedInt(vaddr, size);
    ensures forall x :: x in Contents ==> (x in old(Contents) || fresh(x));
    {
        prev_idx := 0;
        prev := Head;
        var r := prev.Next;
        var end := vaddr + size;

        if (Head.OverlappedInt(vaddr, size))
        {
            return -22, false, prev, prev_idx;
        }

        while (r != null && r.End() <= vaddr)
        decreases *;
        invariant Valid();
        invariant prev != null && prev.Next == r;
        invariant 0 <= prev_idx < |Contents| && Contents[prev_idx] == prev;
        invariant r != null ==> prev_idx + 1 < |Contents| && Contents[prev_idx + 1] == r;
        invariant forall j :: 0 <= j <= prev_idx ==> !Contents[j].OverlappedInt(vaddr, size);
        {
            prev := r;
            r := r.Next;
            prev_idx := prev_idx + 1;
        }

        // No overlaps
        if (r == null || r.StartAddress >= end)
        {
            return 0, false, prev, prev_idx;
        }

        if (r.IsFixed)
        {
            return -22, false, prev, prev_idx;
        }

        if (r.StartAddress < vaddr)
        {
            if (end < r.End())
            {
                var offset := vaddr - r.StartAddress;
                var middleRegion := Split(r, offset, prev_idx + 1);
            }
            else
            {
                r.CutRight(r.End() - vaddr);
            }
            prev := r;
            r := r.Next;
            prev_idx := prev_idx + 1;
            return 1, true, prev, prev_idx;
        }
        return 1, false, prev, prev_idx;
    }

    method RemoveMappingCenter(vaddr: nat, size: int, prev: MemoryRegion, ghost prev_idx:int)
    returns (ret :int, changed: bool, prev_out: MemoryRegion, ghost prev_idx_out: int)
    requires size > 0 && size % 4096 == 0;
    requires vaddr > 0 && vaddr % 4096 == 0;
    requires Valid();
    requires prev != null && prev.End() <= vaddr;
    requires 0 <= prev_idx < |Contents| && Contents[prev_idx] == prev;
    requires prev.Next != null ==> vaddr <= prev.Next.StartAddress;
    modifies Contents, this`Contents, this`Repr;
    ensures Valid();
    ensures 0 <= prev_idx_out < |Contents| && Contents[prev_idx_out] == prev_out;
    ensures ret >= 0 ==> forall j :: 0 <= j <= prev_idx_out ==> !Contents[j].OverlappedInt(vaddr, size);
    ensures ret >= 0 ==> forall j :: prev_idx_out + 1 < j < |Contents| ==> !Contents[j].OverlappedInt(vaddr, size);
    ensures ret > 0 ==> prev_out.Next != null && prev_out.Next.StartAddress < vaddr + size < prev_out.Next.End();
    ensures ret == 0 ==> forall j :: 0 <= j < |Contents| ==> !Contents[j].OverlappedInt(vaddr, size);
    ensures forall x :: x in Contents ==> x in old(Contents);
    {
        changed := false;
        var r := prev.Next;
        var end := vaddr + size;
        ghost var j := 0;

        while (r != null && !r.IsFixed && r.End() <= end)
        decreases *;
        invariant Valid();
        invariant prev != null && prev.Next == r;
        invariant 0 <= prev_idx < |Contents| && Contents[prev_idx] == prev;
        invariant r != null ==> prev_idx + 1 < |Contents| && Contents[prev_idx + 1] == r;
        invariant r == null ==> prev_idx == |Contents| - 1;
        invariant prev.End() <= vaddr;
        invariant |old(Contents)| - |Contents| == j;
        invariant Contents[..prev_idx + 1] == old(Contents)[..prev_idx + 1];
        invariant Contents[prev_idx + 1..] == old(Contents)[prev_idx + 1 + j..];
        {
            changed := true;
            ghost var old_contents := Contents;
            assert old_contents[prev_idx + 2..] == old_contents[prev_idx + 1..][1..];
            RemoveNode(prev, r, prev_idx);
            r := prev.Next;
            j := j + 1;
        }

        assert Contents == old(Contents)[..prev_idx + 1] + old(Contents)[prev_idx + 1 + j..];
        assert forall i :: 0 <= i < |Contents| ==> Contents[i] in old(Contents);

        if (r != null && r.End() <= end && r.IsFixed)
        {
            return -22, changed, prev, prev_idx;
        }

        if (r == null || r.StartAddress >= end)
        {
            return 0, changed, prev, prev_idx;
        }
        return 1, changed, prev, prev_idx;
    }
}
