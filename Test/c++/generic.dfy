newtype{:nativeType "ulong"} uint64 = i:int | 0 <= i < 0x10000000000000000

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

datatype Option<V> = None | Some(value:V)

method Main() {
  var t := new Test(true);
  var u := new UseTest();
  u.DoSomething(t);

  // Test equality on generic datatypes
  var five:uint64 := 5;
  var x := Some(five);
  var y := Some(five);
  var b := x == y;
}
