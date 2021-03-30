module Atomics {
  type Atomic<G>

  method execute_atomic_add<G>(a: Atomic<G>)
  returns (
      ret_value: int,
      ghost orig_value: int,
      ghost new_value: int,
      ghost g: G)

  ghost method finish_atomic_add<G>(a: Atomic<G>, new_value: int, g': G)

  method f(a: Atomic<int>, haha: Atomic<int>) {
    var ret, b, c, d := execute_atomic_add(a);
    var a := haha;
    finish_atomic_add(a, c, 5);
  }
}
