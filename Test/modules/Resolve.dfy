// RUN: %dafny /compile:0 /print:"%t.print" /dprint:"%t.dprint" "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

abstract module A {
  datatype T = T
}

module B refines A {
}

abstract module C(a: A) {
}

module D(a: A) refines C(a) {
}

abstract module E(c: C) {
}

abstract module F(e: E) {
}

module G(a: A) refines E(C(a)) {
}

abstract module H(a: A) refines F(G(a)) {
  import X = a
  import Y = e.c.a
  import Z = G(a).a
  import W = G(a).c.a

  lemma types_eq(x: X.T, y: Y.T, z: Z.T, w: W.T)
  requires x == y == z == w
  {
  }
}
