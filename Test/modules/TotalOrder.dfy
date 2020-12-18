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
  predicate IsStrictlySorted(s: seq<T.K>)
  {
    forall i, j | 0 <= i < j < |s| :: T.lt(s[i], s[j])
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

module Stuff2 {
  import T = IntTotalOrder
  import SortUtil = SortUtil(IntTotalOrder)

  lemma foo(a: T.K, b: T.K)
  requires T.lt(a, b)
  ensures SortUtil.IsStrictlySorted([a, b])
  {
  }
}

module Stuff3 {
  import opened IntTotalOrder
  import opened SortUtil(IntTotalOrder)

  lemma foo(a: K, b: K)
  requires lt(a, b)
  ensures IsStrictlySorted([a, b])
  {
  }
}

module Stuff4 {
  import T = IntTotalOrder
  import S = SortUtil(IntTotalOrder)

  // We are allowed to refer to 'S.T', T being
  // a parameter of S.
  lemma foo(a: T.K, b: S.T.K)
  requires T.lt(a, b)
  ensures S.T.IsStrictlySorted([a, b])
  {
  }
}

module StuffForwardParameter(T: TotalOrder) {
  import SortUtil = SortUtil(T)

  lemma foo(a: T.K, b: T.K)
  requires T.lt(a, b)
  ensures SortUtil.IsStrictlySorted([a, b])
  {
  }
}

module StuffNameCollision(T: TotalOrder) {
  import SortUtil = SortUtil(T)

  // SortUtil still refers to the global functor SortUtil,
  // not the imported module SortUtil
  import X = SortUtil(T) 

  lemma foo(a: T.K, b: T.K)
  requires T.lt(a, b)
  ensures X.IsStrictlySorted([a, b])
  {
  }
}

module WrongStuff {
  // error: TotalOrder is abstract
  import SortUtil = SortUtil(TotalOrder)
}

module WrongStuff2 {
  // error: TotalOrder is abstract
  import T = TotalOrder
}

abstract module WrongStuff3 {
  // error: TotalOrder is abstract
  import SortUtil = SortUtil(TotalOrder)
}

abstract module WrongStuff4 {
  // error: TotalOrder is abstract
  import T = TotalOrder
}
