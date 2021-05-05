// RUN: %dafny /compile:0 /print:"%t.print" /dprint:"%t.dprint" "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

abstract module A {
  datatype T = T
}

//module B refines A {
//}

abstract module C(a: A) {
}

abstract module D(a_d: A) refines C(a_d) {
}

abstract module E(c: C) { // Invalid: C is a naked Functor.  Can't allow this and disallow MissingParameter in ModuleApplication.dfy
}

abstract module F(e: E) { // Invalid: E is a naked Functor.  Can't allow this and disallow MissingParameter in ModuleApplication.dfy
}

abstract module G(a_g: A) refines E(C(a_g)) {
}

abstract module H(a_h: A) refines F(G(a_h)) {
  import X = a_h
  //import Y = e.c.a    // Invalid?: e is an argument to F, which has already been applied
  import Z = G(a_h).a_g
  import W = G(a_h).a

  lemma types_eq(x: X.T, /* y: Y.T, */ z: Z.T, w: W.T)
  requires x == y == z == w
  {
  }
}
