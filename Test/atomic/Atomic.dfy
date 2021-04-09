module Atomics {
  type Atomic<G>

  method execute_atomic_add<G>(a: Atomic<G>)
  returns (
      ret_value: int,
      ghost orig_value: int,
      ghost new_value: int,
      glinear g: G)

  glinear method finish_atomic<G>(ghost a: Atomic<G>, ghost new_value: int, glinear g': G)

  /*method f(a: Atomic<int>, haha: Atomic<int>) {
    var ret;
    ghost var b, c;
    glinear var d;

    ret, b, c, d := execute_atomic_add(a);
    var a := haha; // error - not ghost
    finish_atomic(a, c, d); // ERROR - didn't pass in the same a
  }

  function method {:extern} transform(glinear d: int) : (glinear d': int)

  method f2(a: Atomic<int>, haha: Atomic<int>) {
    var ret;
    ghost var b, c;
    glinear var d;
    ret, b, c, d := execute_atomic_add(a);
    finish_atomic(a, c, transform(d));
  }

  method f3(a: Atomic<int>, haha: Atomic<int>) {
    var ret;
    ghost var b, c;
    glinear var d;
    ret, b, c, d := execute_atomic_add(a);
    finish_atomic(a, b, d); // ERROR - didn't pass in c
  }

  ghost method two_out_method()
  returns (a: int, b: int)

  datatype Foo = Foo(a: int, b: int)

  method f_overwrite_c(a: Atomic<int>, haha: Atomic<int>) {
    var ret;
    ghost var b, c, foo;
    glinear var d;
    ret, b, c, d := execute_atomic_add(a);
    c := 2; // ERROR - can't update c
    c, foo := two_out_method(); // ERROR - can't update c
    finish_atomic(a, c, d);
  }
  */

  method some_rando_nonghost_method()
  { }

  lemma some_rando_lemma()
  { }

  ghost method some_rando_ghost_method()
  { }

  glinear method some_rando_glinear_method()
  { }

  glinear method some_rando_glinear_method1(glinear x: int)
  returns (glinear x': int)

  glinear method some_rando_glinear_method2()
  returns (glinear y: int)

  glinear method some_rando_glinear_method3(glinear y: int)

  method f_do_nonghost_stuff(a: Atomic<int>, haha: Atomic<int>) {
    var some_var := 5;

    var ret;
    ghost var b, c;
    glinear var d;
    ret, b, c, d := execute_atomic_add(a);

    some_var := 6; // ERROR - not ghost 

    some_rando_nonghost_method(); // ERROR - not ghost

    finish_atomic(a, c, d);
  }

  method f_do_ghost_stuff(a: Atomic<int>, haha: Atomic<int>) {
    ghost var some_var := 5;

    var ret;
    ghost var b, c;
    glinear var d;
    ret, b, c, d := execute_atomic_add(a);

    // all ghost stuff

    some_var := 6;
    some_rando_lemma();
    some_rando_ghost_method();
    some_rando_glinear_method();
    d := some_rando_glinear_method1(d);
    glinear var y := some_rando_glinear_method2();
    some_rando_glinear_method3(y);
    glinear var monkey := d;
    d := monkey;

    finish_atomic(a, c, d);
  }



}
