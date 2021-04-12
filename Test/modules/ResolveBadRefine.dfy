// RUN: %dafny /compile:0 /print:"%t.print" /dprint:"%t.dprint" "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

abstract module TotalOrder {
  type K(==,!new)
}

abstract module IntTotalOrder refines TotalOrder {
  type K = int
}

abstract module AnyTotalOrderWillDo(T: TotalOrder) {
}

abstract module IntOnly(I: IntTotalOrder) {
}

abstract module Stuff {
  import I = IntTotalOrder
  import W = AnyTotalOrderWillDo(IntTotalOrder).T  // This is okay
  import X = IntOnly(I)     // This is okay
  import T = TotalOrder
  import Z = IntOnly(T).I   // This is not okay!

}


