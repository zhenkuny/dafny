abstract module TotalOrder {
  type K(==,!new)
  predicate lt(a: K, b: K)
}

module IntTotalOrder refines TotalOrder {
  type K = int

  predicate lt(a: K, b: K)
  {
    a < b
  }
}

module SortUtil(T: TotalOrder) {
  import opened T // allowed to 'import opened' a parameter

  predicate IsStrictlySorted(s: seq<K>)
  {
    forall i, j | 0 <= i < j < |s| :: lt(s[i], s[j])
  }
}

module Stuff1 {
  import T = IntTotalOrder
  import SortUtil = SortUtil(T)

  lemma foo(a: T.K, b: T.K)
  requires T.lt(a, b)
  ensures SortUtil.IsStrictlySorted([a, b])
  {
  }
}
