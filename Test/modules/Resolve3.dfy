abstract module L {
}

abstract module U(l: L) {
}

module A(u: U(L)) {
  import E = u.l
}

module W(x: L) refines U(x) {
}

module B(r: L) {
  import D = W(r)
}

module C refines L {
  function the_int() : int { 5 }
}

module Stuff {
  // This should resolve to C
  import X = A(B(C).D).E

  lemma stuff()
  ensures X.the_int() == 5
}
