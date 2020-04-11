
include "LinearSequence.s.dfy"

//linear datatype Node =
//  | Leaf(linear keys: seq<uint64>, linear values: seq<uint64>)
//  | Index(linear pivots: seq<uint64>, linear children: lseq<uint64>)

method Test(name:string, b:bool) 
  requires b;
{
  if b {
    print name, ": This is expected\n";
  } else {
    print name, ": This is *** UNEXPECTED *** !!!!\n";
  }
}

method TestLinearSequences() 
{
  linear var s0 := seq_alloc<uint64>(10);
  var x := seq_get(s0, 0);
  print x;
  linear var s1 := seq_set(s0, 0, 42);
//  x := seq_get(s0, 0);   // Fails linearity check
//  print x;
  Test("Test result of set", seq_get(s1, 0) == 42);
  linear var s2 := seq_set(s1, 0, 24);
  Test("Test result of set again", seq_get(s2, 0) == 24);
//  Test("Test length", seq_length(s1) == 10);  // Fails linearity check
  Test("Test length", seq_length(s2) == 10);
  var s3 := seq_unleash(s2);
  Test("Normal seq", s3[0] == 24);

  linear var t0 := seq_alloc<uint64>(5);
  linear var t1 := seq_set(t0, 4, 732);
  var _ := seq_free(t1);
}

method Main()
{
  TestLinearSequences();
}
