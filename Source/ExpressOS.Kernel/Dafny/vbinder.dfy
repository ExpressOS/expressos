class Thread {}
class ByteBufferRef {}

class VBinderMessage {
  var from: Thread;
  var Label: int;
  var payload: ByteBufferRef;

  ghost var GhostTarget: Thread;
}

class VBinderMessageBuffer {
  // public view of the class:
  ghost var Contents: seq<VBinderMessage>;  // the contents of the ring buffer
  ghost var N: nat;  // the capacity of the ring buffer

  ghost var Owner: Thread;

  // private implementation:
  var data: array<VBinderMessage>;
  var first: nat;
  var len: nat;

  // Valid encodes the consistency of RingBuffer objects (think, invariant)
  predicate Valid
    reads *;
  {
    data != null &&
    data.Length == N &&
    (N > 0 && len <= N && first < N) &&
    (Contents == if first + len <= N then data[first..first+len] 
                                    else data[first..] + data[..first+len-N]) &&
    (forall x :: x in Contents ==> x != null && x.GhostTarget == Owner)

  }

  constructor Create(n: nat, owner: Thread)
    requires n > 0;
    modifies this;
    ensures Contents == [];
    ensures N == n;
    ensures Owner == owner;
    ensures Valid && fresh(data);
  {
    data := new VBinderMessage[n];
    first, len := 0, 0;
    Contents, N := [], n;
    Owner := owner;
  }

  method Clear()
    requires Valid;
    modifies this`len, this`Contents, data;
    ensures Contents == [] && N == old(N);
    ensures Valid;
  {
    len := 0;
    Contents := [];
    var i :nat := 0;
    while (i < data.Length)
      invariant Contents == [] && N == old(N);
      invariant Valid;
    {
      data[i] := null;
      i := i + 1;
    }
  }

  method Head() returns (x: VBinderMessage)
    requires Valid;
    requires !IsEmpty();
    ensures x == Contents[0];
    ensures Valid;
  {
    x := data[first];
  }

  function method IsEmpty() : bool
    reads this`len;
  { len == 0 }

  method Enqueue(x: VBinderMessage)
    requires x != null && x.GhostTarget == Owner;
    requires Valid;
    requires |Contents| != N;
    modifies data, this`len, this`Contents;
    ensures Contents == old(Contents) + [x] && N == old(N);
    ensures Valid;
    ensures !IsEmpty();
  {
    var nextEmpty := if first + len < data.Length 
                     then first + len else first + len - data.Length;
    data[nextEmpty] := x;
    len := len + 1;
    Contents := Contents + [x];
  }

  method Dequeue() returns (x: VBinderMessage)
    requires Valid;
    requires !IsEmpty();
    modifies data, this`first, this`len, this`Contents;
    ensures x == old(Contents)[0] && Contents == old(Contents)[1..] && N == old(N);
    ensures x != null && x.GhostTarget == Owner;
    ensures Valid;
  {
    x := data[first];
    data[first] := null;
    first, len := if first + 1 == data.Length then 0 else first + 1, len - 1;
    Contents := Contents[1..];
  }

}

method TestHarness()
{
  var thr1 := new Thread;
  var thr2 := new Thread;

  var b := new VBinderMessageBuffer.Create(5, thr1);
  var x := new VBinderMessage;
  var y := new VBinderMessage;
  var z := new VBinderMessage;

  x.GhostTarget := thr1;
  y.GhostTarget := thr1;
  z.GhostTarget := thr1;

  b.Enqueue(x);
  b.Enqueue(y);
  var h := b.Dequeue();  assert h == x;
  b.Enqueue(z);
  h := b.Dequeue();  assert h == y;
  h := b.Dequeue();  assert h == z;
}