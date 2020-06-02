newtype{:nativeType "uint"} uint32  = i:int | 0 <= i < 0x100000000
newtype{:nativeType "ulong"} uint64 = i:int | 0 <= i < 0x10000000000000000

//function method Test(x:uint32) : uint64 {
//  x as uint64 + 1
//}
//
//function method Seqs<T>(s:seq<T>, x:uint32, default_val:T) : T 
//  requires |s| < 1000;
//{
//  if |s| as uint32 > x then s[x] else default_val
//}
//

function method AddOne(x:uint64) : uint64
  requires x < 100;
{
  x + 1
}

method {:extern "Extern", "Caller"} Caller(inc:uint64-->uint64, x:uint64) returns (y:uint64)
  requires inc.requires(x);

method {:extern "Extern", "GenericCaller"} GenericCaller<A>(inc:A-->A, x:A) returns (y:A)
  requires inc.requires(x);

class {:extern "Extern", "GenericClass"} GenericClass<A>
{
  constructor {:extern "Extern", "GenericClass"} (inc:A-->A, x:A)
    requires inc.requires(x)
}


method CallTest() {
//  var x := Caller(AddOne, 5);
//  print x;
//  var y := GenericCaller(AddOne, 5);
//  print y;
  var z := new GenericClass(AddOne, 7);
  print z;
  print "\n";
}

method Main() {
  //var y := Test(12);
  CallTest();
}
