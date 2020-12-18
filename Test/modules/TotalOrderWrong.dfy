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
