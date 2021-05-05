abstract module L {
}

abstract module U(l: L) {
}

abstract module A(u: U(L)) {
  import E = u.l
}

abstract module W(x: L) refines U(x) {
}

abstract module B(r: L) {
  import D = W(r)
}

abstract module C refines L {
  function the_int() : int { 5 }
}

abstract module Stuff {

  // B(C).D = W(C), which refines U(C)
  // Is U(C) <= U(L)?
  import X = A(B(C).D).E  // This should resolve to C

  lemma stuff()
  ensures X.the_int() == 5
}
