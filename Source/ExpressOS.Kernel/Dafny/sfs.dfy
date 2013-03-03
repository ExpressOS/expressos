class ByteBufferRef {}

class Thread {}
class UserPtr {}
class IntPtr {}

class SecureFSInode
{
  var FileSize: nat;
  var OnDiskBlock: nat;
  var Handle: IntPtr;

  var Pages : CachePageHolder;
  ghost var Repr : set<object>;

  function Valid() : bool
    reads this, Repr, Pages.Spine;
  {
    this in Repr && null !in Repr
    && Pages != null && Pages in Repr && this !in Pages.Repr
    && Pages.Repr <= Repr && Pages.Valid()
    && Handle != null
  }

  method Read(current: Thread, userBuf: UserPtr, len: int, pos: nat) returns (ret: int)
    requires Valid();
    modifies Repr;
    ensures Valid();
  {
    var readBytes : nat := 0;
    var remainedBytes := len;
    if (FileSize - pos < remainedBytes)
    {
        remainedBytes := FileSize - pos;
    }

    var currentPageIndex := ArchDefinition.PageIndex(pos);
    var page := Pages.Lookup(currentPageIndex / 4096);
    assert currentPageIndex >= 0;

    while (remainedBytes > 0)
      invariant Pages != null && this !in Pages.Repr;
      invariant Valid();
      invariant fresh(Pages.Repr - old(Pages.Repr));
    {
      if (page == null)
      {
        // allocate new Cache page
        page := CachePage.Allocate(currentPageIndex / 4096);
        var succeed := page.Load(Handle); 
        if (!succeed)
        {
          return -5;
        }

        Pages.Add(page);
        Repr := Repr + Pages.Repr;
      }

      // Copying
      var pageCursor := (pos + readBytes) % 4096;
      var chunkLen := if 4096 - pageCursor < remainedBytes
                      then 4096 - pageCursor
                      else remainedBytes;

      var left := WriteUserBuffer(current, userBuf, pageCursor, page, readBytes, chunkLen);
      readBytes := readBytes + chunkLen - left;

      if (left != 0)
      {
          return -14;
      }

      remainedBytes := remainedBytes - chunkLen;
      currentPageIndex := currentPageIndex + 4096;
      page := Pages.Lookup(currentPageIndex / 4096);
    }

    return readBytes;
  }

  method Write(current: Thread, buf: ByteBufferRef, len: int, pos: nat) returns (ret: int)
    requires Valid();
    modifies Repr;
    ensures Valid();
  {
      var writtenBytes :nat := 0;
      var remainedBytes := len;
      var currentPageIndex := ArchDefinition.PageIndex(pos);
      assert currentPageIndex >= 0;

      var page := null;
      while (remainedBytes > 0)
        invariant Valid();
        invariant fresh(Pages.Repr - old(Pages.Repr));
      {
          var page := Pages.Lookup(currentPageIndex / 4096);
          if (page == null)
          {
              page := CachePage.Allocate(currentPageIndex / 4096);
              // Three cases are possible:
              //
              // (1) This is a write-miss on the original file (thus we need to verify integrity)
              // (2) This is a write-miss, but on new page (e.g., open() + ftell())
              // (3) Writing over the end of the file.
              //
              // We bring in a blank page for (2) / (3).

              var currentBlockId := currentPageIndex / 4096;

              if (currentBlockId < OnDiskBlock)
              {
                  // Case (1)
                  var succeed := page.Load(Handle);
                  if (!succeed)
                  {
                      return -5;
                  }
              }
              else
              {
                  // Case (2) / (3)
                  assert page.CurrentState == 0;
              }

              Pages.Add(page);
              Repr := Repr + Pages.Repr;
          }

          // Copying
          var pageCursor := (pos + writtenBytes) % 4096;
          var chunkLen := if 4096 - pageCursor < remainedBytes
                            then 4096 - pageCursor
                            else remainedBytes;

          var left := ReadUserBuffer(current, buf, writtenBytes, page, pageCursor, chunkLen);

          writtenBytes := writtenBytes + chunkLen - left;
          if (left != 0)
          {
              return -14;
          }

          // Update FileSize
          if (pos + writtenBytes > FileSize)
          {
              FileSize := pos + writtenBytes;
          }

          remainedBytes := remainedBytes - chunkLen;
          currentPageIndex := currentPageIndex + 4096;
      }

      return writtenBytes;
  }

  static method LoadPageRaw(handle: IntPtr, page: CachePage) returns (ret: bool)
    requires page != null;
    requires page.CurrentState == 0;
    ensures ret ==> page.CurrentState == 1;

  static method WriteUserBuffer(current: Thread, ptr: UserPtr, buf_offset: nat, page: CachePage, offset: nat, len: nat) returns (ret: nat)
    requires page != null;
    ensures ret <= len;

  static method ReadUserBuffer(current: Thread, ptr: ByteBufferRef, buf_offset: nat, page: CachePage, offset: nat, len: nat) returns (ret: nat)
    requires page != null;
    ensures ret <= len;

}

class ArchDefinition {
  static function method PageIndex(addr: nat) : nat
  {
    (addr / 4096) * 4096
  }
}


//
// For performance of verification, I don't use representation to frame
// the CachePage class. The correctness properties are in the Valid() function
// of CachePageHolder.
//
class CachePage {
  // Ghost variable for state : 0 - Empty, 1 - ReadIn, 2 - Verified, 3 - Decrypted, 4 - Encrypted, 5 - Disposed
  var CurrentState: int;

  var Location: int;
  var Buffer: ByteBufferRef;

  var Next: CachePage;

  function Valid() : bool
    reads this;
  {
    Location >= 0
  }

  constructor Init(idx : int, buf : ByteBufferRef)
    requires idx >= 0;
    modifies this;
    ensures Location == idx;
    ensures Buffer == buf;
    ensures CurrentState == 0;
    ensures Next == null;
    ensures Valid();
  {
    Location := idx;
    Buffer := buf;
    CurrentState := 0;
    Next := null;
  }

  static method Allocate(loc: int) returns (ret: CachePage)
    requires loc >= 0;
    ensures ret != null;
    ensures fresh(ret);
    ensures ret.Valid() && ret.CurrentState == 0 && ret.Next == null;
  {
    var buf := AllocFreeBuffer();
    var p := new CachePage.Init(loc, buf);
    return p;
  }

  static method AllocFreeBuffer() returns (ret: ByteBufferRef)
    ensures fresh(ret);

  method Decrypt(handle: IntPtr)
    requires Valid();
    requires CurrentState == 2;
    modifies this;
    ensures CurrentState == 3;
    ensures Valid();
    ensures Location == old(Location);

  method Encrypt(handle: IntPtr)
    requires Valid();
    requires Writable();
    modifies this`CurrentState;
    ensures CurrentState == 4;
    ensures Valid();
    ensures Location == old(Location);

  method Verify(handle: IntPtr) returns (ret: bool)
    requires Valid();
    requires CurrentState == 1;
    modifies this;
    ensures ret ==> CurrentState == 2;
    ensures Valid();
    ensures Location == old(Location);

  method Load(handle: IntPtr) returns (ret: bool)
    requires Valid() && CurrentState == 0 && Next == null;
    modifies this;
    ensures Valid();
    ensures ret ==> CurrentState == 3 && Next == null;
  {
    var r := SecureFSInode.LoadPageRaw(handle, this);
    if (!r) {
      return false;
    }

    r := Verify(handle);
    if (!r) {
      return false;
    }

    Decrypt(handle);
    return true;
  }

  method Dispose()
    requires Valid();
    modifies this;
    ensures Valid();
    ensures Next == old(Next);
    ensures Location == old(Location);
    ensures CurrentState == 5;

  function method Writable() : bool
    reads this;
  { CurrentState == 0 || CurrentState == 3 }
}

class CachePageHolder {
  var Head : CachePage;
  var Length : nat;

  ghost var Repr : set<object>;
  ghost var Spine : seq<CachePage>;

  function Valid() : bool
    reads this, Repr, Spine;
  {
    this in Repr && null !in Repr && Length == |Spine| &&
    (Head == null ==> this.Spine == []) &&
    (Head != null ==> |Spine| > 0 && Head == Spine[0]) &&
    (forall x :: x in Spine ==> x != null && x in Repr && x.Writable() && x.Valid()) &&
    (forall i :: 0 <= i < Length - 1 ==> Spine[i].Next == Spine[i + 1]) &&
    (Length > 0 ==> Spine[Length - 1].Next == null) &&
    (forall i, j :: 0 <= i < j < Length ==> Spine[i] != Spine[j] && Spine[i].Location < Spine[j].Location)
  }

  method Add(page: CachePage)
    requires Valid();
    requires page != null && page.Valid() && page.Writable() && page.Next == null;
    requires page !in Spine;
    modifies Repr, page;
    ensures Valid();
    ensures fresh(Repr - old(Repr) - {page});
//    ensures Length == old(Length) + 1;
  {
    if (Head == null) {
      Head := page;
      Repr := Repr + {page};
      Spine := [page];
      Length := 1;
      return;
    }

    ghost var spine_idx : int;
    var prev : CachePage;
    prev, spine_idx := LookupPrev(page.Location);

    if (prev == null) {
      assert page.Location < Head.Location;
      page.Next := Head;
      Head := page;
      Repr := Repr + {page};
      Spine := [page] + Spine;
      Length := Length + 1;
      return;
    }

    if (prev.Location == page.Location) {
      // Length := Length + 1;
      // Run-time error
      return;
    }

    var old_next := prev.Next;

    prev.Next := page;
    page.Next := old_next;

    Repr := Repr + {page};

    Spine := Spine[..spine_idx + 1] + [page] + Spine[spine_idx + 1..];
    Length := Length + 1;

  }

  method Seal(handle: IntPtr) returns (ret : array<CachePage>)
    requires Valid();
    modifies Repr, Spine;
    ensures Valid();
    ensures Head == null && Length == 0;
    ensures ret != null && fresh(ret) && forall i :: 0 <= i < ret.Length ==> ret[i] != null && ret[i].CurrentState == 4;
  {
    ret := new CachePage[Length];

    var i := 0;
    var current := Head;
    while (current != null)
      invariant Length == |Spine| == ret.Length;
      invariant forall j :: 0 <= j < Length ==> Spine[j] != null && Spine[j] in Repr && Spine[j].Valid();
      invariant forall j :: i <= j < Length ==> Spine[j].Writable();
      invariant forall j :: 0 <= j < Length - 1 ==> Spine[j].Next == Spine[j + 1];
      invariant Length > 0 ==> Spine[Length - 1].Next == null;

      invariant current != null ==> i < Length && current == Spine[i]
        && (i < Length - 1 ==> current.Next == Spine[i + 1])
        && (i == Length - 1 ==> current.Next == null);
      invariant current == null ==> i == Length;

      invariant fresh(ret);
      invariant forall j :: 0 <= j < i ==> i <= Length && ret[j] == Spine[j] && Spine[j].Valid() && Spine[j].CurrentState == 4;

      modifies Spine, ret;
      decreases Length - i;
    {
      assert current.Valid() && current.Writable();
      current.Encrypt(handle);
      ret[i] := current;
      i := i + 1;
      current := current.Next;
    }

    Head := null;
    Length := 0;
    Spine := [];
    Repr := {this};

    return ret;
  }

  method Truncate(loc : int) returns (ret: CachePage)
    requires Valid();
    modifies Repr, Spine;
    ensures Valid();
    ensures forall x :: x in Spine ==> x.Location <= loc;
    ensures ret != null ==> ret.Location == loc;
  {
    if (Head == null) {
      return null;
    }

    var prev : CachePage := null;
    ghost var spine_idx;
    prev, spine_idx := LookupPrev(loc);

    if (prev == null) {
      // Drops all cache
      ret := null;

      var current := Head;
      ghost var i := 0;

      while (current != null)
        invariant Length == |Spine|;
        invariant forall j :: i <= j < Length ==> Spine[j] != null && Spine[j].Writable() && Spine[j].Valid()
          && Spine[j].Next == (if j == Length - 1 then null else Spine[j + 1]);
        invariant current != null ==> i < Length && current == Spine[i] && current.Valid();
        invariant current != null ==> current.Next == (if i == Length - 1 then null else Spine[i + 1]);

        modifies Spine;
        decreases Length - i;
      {
        current.Dispose();
        i := i + 1;
        current := current.Next;
      }

      Repr := {this};
      Spine := [];
      Head := null;
      Length := 0;

    } else
    {
      ret := if prev.Location == loc then prev else null;
      var page := prev.Next;
      if (page == null) {
        return;
      }

      ghost var spine_left := Spine[..spine_idx + 1];
      ghost var spine_right := Spine[spine_idx + 1..];
      ghost var sr_len := |spine_right|;

      assert Spine[spine_idx] == prev;
      assert Spine[spine_idx + 1] == page;
      assert spine_right[0] == Spine[spine_idx + 1];

      assert forall x :: x in spine_left ==> x.Location <= loc && x !in spine_right;

      ghost var idx := 0;

      while (page != null)
        invariant forall j :: 0 <= j < sr_len - 1 ==> spine_right[j].Next == spine_right[j + 1];
        invariant sr_len > 0 ==> spine_right[sr_len - 1].Next == null;

        invariant page != null ==> idx < sr_len && spine_right[idx] == page
          && (idx < sr_len - 1 ==> page.Next == spine_right[idx + 1])
          && (idx == sr_len - 1 ==> page.Next == null);
        invariant page == null ==> idx == sr_len;
        invariant idx + Length == |Spine|;
        invariant forall j :: idx <= j < sr_len ==> spine_right[j].Valid() && spine_right[j].Writable();

        modifies spine_right, this`Length;
        decreases sr_len - idx;
      {
        page.Dispose();
        Length := Length - 1;
        idx := idx + 1;
        page := page.Next;
      }

      assert idx == sr_len;
      prev.Next := null;
      Spine := spine_left;
    }
  }

  method Lookup(idx : int) returns (ret : CachePage)
    requires Valid();
    ensures Valid();
    ensures ret != null ==> ret.Location == idx;
  {
    if (Head == null) {
      return null;
    }

    ghost var spine_idx : int;
    var r : CachePage;
    r, spine_idx := LookupPrev(idx);

    if (r == null) {
      return null;
    }

    return if r.Location == idx then r else null;
  }

  method LookupPrev(idx : int) returns (ret : CachePage, ghost spine_idx : int)
    requires Valid();
    requires Head != null;
    ensures Valid();
    ensures ret == null && Head != null ==> idx < Head.Location;
    ensures ret != null && ret.Next != null ==> ret.Next.Location > idx;
    ensures ret != null ==> spine_idx >= 0 && spine_idx < Length && Spine[spine_idx] == ret && ret.Location <= idx;
  {
    var prev : CachePage := null;
    var current := Head;
    spine_idx := 0;

    while (current != null && idx >= current.Location)
      invariant current != null ==> spine_idx < Length && current == Spine[spine_idx];
      invariant current != null ==> current.Next == (if spine_idx == Length - 1 then null else Spine[spine_idx + 1]);
      invariant prev == null ==> current != null && current == Head;
      invariant prev != null ==> current == prev.Next && prev.Location <= idx;
      invariant prev != null ==> spine_idx <= Length && spine_idx > 0 && prev == Spine[spine_idx - 1];
      decreases Length - spine_idx;
    {
      prev := current;
      current := current.Next;
      spine_idx := spine_idx + 1;
    }
    return prev, spine_idx - 1;
  }
}
