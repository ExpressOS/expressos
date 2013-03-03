class Process {
  
}

class File {
  ghost var Owner : Process;
  constructor Init(proc: Process)
    modifies this;
    ensures Owner == proc;
  {
    Owner := proc;
  }
}

class FileDescriptorTable
{
  var descriptors : array<File>;
  var finger : nat;

  ghost var Owner : Process;
  ghost var Contents : seq<File>;

  predicate Valid
    reads this, descriptors, Contents;
  { finger > 0 && finger < |Contents|
    && descriptors != null && |Contents| == descriptors.Length
    && (Contents == descriptors[0..])
    && (forall x :: x in Contents ==> (x != null ==> x.Owner == Owner))
  }

/*
  method ClearDescriptors(descriptors : array<File>)
    requires descriptors != null;
    modifies descriptors;
    ensures forall x :: 0 <= x < descriptors.Length ==> descriptors[x] == null;
  {
    parallel (i | 0 <= i < descriptors.Length) {
      descriptors[i] := null;
    }
    Contents := descriptors[0..];
  }
*/

  constructor Init(owner: Process)
    modifies this;
    ensures Owner == owner;
    ensures fresh(descriptors);
    ensures Valid;
    ensures |Contents| == 128 && forall x :: x in Contents ==> x == null;
  {
    descriptors := new File[128];

    parallel (i | 0 <= i < descriptors.Length) {
      descriptors[i] := null;
    }

    Contents := descriptors[0..];
    finger := 3;
    Owner := owner;
  }

  method Lookup(fd: int) returns (ret: File)
    requires Valid;
    ensures ret != null ==> (IsValidFd(fd) && ret == Contents[fd] && ret.Owner == Owner);
    ensures Valid;
  {
    if (!IsValidFd(fd))
    {
      return null;
    }

    ret := descriptors[fd];
	assert ret != null ==> IsValidFd(fd);
	assert ret != null ==> (ret == Contents[fd] && ret.Owner == Owner);
	return ret;
  }

  method Add(fd: int, file: File)
    requires Valid;
    requires IsAvailableFd(fd);
    requires file != null && file.Owner == Owner;
    modifies this`Contents, descriptors;
    ensures Valid;
    ensures IsValidFd(fd) && Contents[fd] == file;
  {
    descriptors[fd] := file;
    Contents := Contents[fd := file];
  }

  method GetUnusedFd() returns (ret: int)
    requires Valid;
    modifies this`finger, this`Contents, this`descriptors;
    ensures Valid;
    ensures fresh(descriptors) || descriptors == old(descriptors);
    ensures IsAvailableFd(ret);
  {

    var size := descriptors.Length;
    var i := finger;
    while (i < size)
    {
      if (IsAvailableFd(i)) {
        UpdateFinger(i);
        ret := i;
        return;
      }
      i := i + 1;
    }

    // Double the size of the container

    var new_descriptors := new File[2 * size];
    parallel (j | 0 <= j < size) {
      new_descriptors[j] := descriptors[j];
    }

    parallel (j | size <= j < size * 2) {
      new_descriptors[j] := null;
    }

    Contents := new_descriptors[0..];
    descriptors := new_descriptors;

    UpdateFinger(size);
    
    return size;
  }

  method Remove(fd: int) returns (ret: int)
    requires Valid;
    modifies this`finger, this`Contents, descriptors;
    ensures Valid;
  {
    if (!IsValidFd(fd)) {
      return -1;
    }

    descriptors[fd] := null;
    Contents := Contents[fd := null];
    UpdateFinger(fd);
    return 0;
  }

  method UpdateFinger(fd: int)
    requires Valid;
	requires IsAvailableFd(fd);
    modifies this`finger;
    ensures Valid;
	ensures IsAvailableFd(fd);
  {
    if (fd >= 3) {
      finger := fd;
    }
  }

  function method IsValidFd(fd: int) : bool
    requires Valid;
    reads this;
    ensures Valid;
  {
    fd > 0 && fd < descriptors.Length
  }

  function method IsAvailableFd(fd: int) : bool
    requires Valid;
    reads this, descriptors;
    ensures Valid;
  {
    IsValidFd(fd) && descriptors[fd] == null
  }
}

method Test()
{
  var proc := new Process;
  var proc1 := new Process;

  var file := new File.Init(proc);
  var fdtable := new FileDescriptorTable.Init(proc);

//  var fd := 5;
  var fd := fdtable.GetUnusedFd();

  fdtable.Add(fd, file);

//  file.Owner := proc1;

//  var l := fdtable.Lookup(fd);

}
