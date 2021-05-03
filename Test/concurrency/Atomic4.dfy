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

  /*method bad(a1: Atomic<int>, a2: Atomic<int>, a3: Atomic<int>)
  {
    var ret1;
    ghost var b1, c1;
    glinear var d1;

    var ret2;
    ghost var b2, c2;
    glinear var d2;

    ret1, b1, c1, d1 := execute_atomic_add(a1);
    ret2, b2, c2, d2 := execute_atomic_noop(a2); // ERROR
    finish_atomic(a2, c2, d2);
    finish_atomic(a1, c1, d1);
  }*/

  method okay(a1: Atomic<int>, a2: Atomic<int>, a3: Atomic<int>)
  {
    atomic_block ret := execute_atomic_add(a1) {
      ghost_acquire g;

      ghost var t := old_value;
      ghost var s := new_value;
      ghost var r := ret;
      glinear var g_heh := g;

      atomic_block j1 := execute_atomic_noop(a2) {
        ghost_acquire g1;

        atomic_block j2 := execute_atomic_noop(a3) {
          ghost_acquire g2;
          ghost_release g2;
        }

        ghost_release g1;
      }

      ghost_release g_heh;
    }
  }

  /*method bad2(a1: Atomic<int>, a2: Atomic<int>, a3: Atomic<int>)
  {
    var ret1;
    ghost var b1, c1;
    glinear var d1;

    var ret2;
    ghost var b2, c2;
    glinear var d2;

    var ret3;
    ghost var b3, c3;
    glinear var d3;

    ret1, b1, c1, d1 := execute_atomic_add(a1);
    assert a1.identifier() != a2.identifier();
    ret2, b2, c2, d2 := execute_atomic_noop(a2);
    assert a1.identifier() != a3.identifier();
    assert a2.identifier() != a2.identifier(); // ERROR
    ret3, b3, c3, d3 := execute_atomic_noop(a3);
    finish_atomic(a3, c3, d3);
    finish_atomic(a2, c2, d2);
    finish_atomic(a1, c1, d1);
  }*/


}
