module Atomics {
  type Atomic<G>

  method execute_atomic_add<G>(a: Atomic<G>)
  returns (
      ret_value: int,
      ghost orig_value: int,
      ghost new_value: int,
      ghost g: G)

  ghost method finish_atomic<G>(a: Atomic<G>, new_value: int, g': G)

  method f(a: Atomic<int>, haha: Atomic<int>) {
    var ret, b, c, d := execute_atomic_add(a);
    var a := haha;
    finish_atomic_add(a, c, 5);
  }

  method f1(a: Atomic<int>, haha: Atomic<int>) {
    var ret;
    ghost var b, c, d;
    ret, b, c, d := execute_atomic_add(a);
    var a := haha;
    finish_atomic_add(a, c, 5);
  }

  method f2(a: Atomic<int>, haha: Atomic<int>) {
    var ret;
    ghost var b, c, d;
    ret, b, c, d := execute_atomic_add(a);
    finish_atomic_add(a, c, 5);
  }

  method two_out_method()
  returns (a: int, b: int)

  datatype Foo = Foo(a: int, b: int)

  method datatype_out_method()
  returns (a: Foo)

  method inout_method(inout x: int)

  method f3(a: Atomic<int>, haha: Atomic<int>) {
    var ret;
    var foo;
    ghost var b, c, d;
    ret, b, c, d := execute_atomic_add(a);
    c := 2; // error
    c, foo := two_out_method(); // error
    //Foo(c, foo) := datatype_out_method(); // error
    //inout_method(inout c); // error
    finish_atomic_add(a, c, 5);
  }


}
