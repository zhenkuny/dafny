// RUN: %dafny /compile:0 "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

module Atomics {
  type Atomic(==)<G>
  {
    function identifier() : nat
  }

  method execute_atomic_add<G>(a: Atomic<G>)
  returns (
      ret_value: int,
      ghost orig_value: int,
      ghost new_value: int,
      glinear g: G)

  method execute_atomic_noop<G>(ghost a: Atomic<G>)
  returns (
      ghost ret_value: int,
      ghost orig_value: int,
      ghost new_value: int,
      glinear g: G)

  glinear method finish_atomic<G>(ghost a: Atomic<G>, ghost new_value: int, glinear g': G)
}

module Stuff {
  import opened Atomics

  method okay(a1: Atomic<int>, a2: Atomic<int>, a3: Atomic<int>)
  returns (x: int)
  {
    var moo := 5;

    atomic_block var ret := execute_atomic_add(a1) {
      ghost_acquire g;

      var t := 7;
      var s := 9;

      if ret == 5 {
        glinear var x;
        x := g;
        g := x;
      }

      ghost_release g;
    }

    x := moo;
  }
}
