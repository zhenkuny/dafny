newtype{:nativeType "ulong"} uint64 = i:int | 0 <= i < 0x10000000000000000

/*
class Test<T> {
  var t:T;

  constructor (e:T) {
    t := e;
  }
}

class UseTest<T> {
  constructor () {}
  method DoSomething(t:Test<T>)
  {
    var x:Test<T> := t;
  }
}


datatype Err<V> = Fail(b:bool) | Ok(value:V)
method ErrTest() returns (e:Err<bool>)
{
  return Fail(false);
}
*/

datatype Option<V> = None | Some(value:V)

//datatype FixedSizeLinearHashMap<V> = FixedSizeLinearHashMap(
//  storage: seq<Item<V>>,
//  count: uint64,
//  /* ghost */ contents: map<uint64, Option<V>>)

datatype LinearHashMap<V> = LinearHashMap(
    //underlying: FixedSizeLinearHashMap<V>,
    o:Option<V>,
    count: uint64)
    ///* ghost */ contents: map<uint64, V>)

method Main() {
  var u:uint64 := 5;
  var d := LinearHashMap(Some(u), u);
  /*
  var t := new Test(true);
  var u := new UseTest();
  u.DoSomething(t);
  */
}
