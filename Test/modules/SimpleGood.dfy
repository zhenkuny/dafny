// RUN: %dafny /compile:0 /print:"%t.print" /dprint:"%t.dprint" "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

module ABase {
  type Key
}

abstract module P_normal {
  import A : ABase

  method Test(a:A.Key)
}

// Use an element of a formal parameter
// Morally equivalent to P_normal above
abstract module P(A: ABase) {
  method Test(a:A.Key)
}


// Try simple functor application
abstract module Apply {
  import OutputBase = P(ABase)

  method More(a:OutputBase.A.Key) {
    OutputBase.Test(a);
  }
}


// Make sure functors behave applicatively
abstract module Apply2 {
  import Output0 = P(ABase)
  import Output1 = P(ABase)

  method More(a:Output0.A.Key, b:Output1.A.Key)
    requires a == b
  {
    Output1.Test(a);
  }
}


// Try passing a refinement to a functor
abstract module B refines ABase {
  method BTest()
}

abstract module ApplyRefinement {
  import OutputRefined = P(B)

  method UseB() {
    OutputRefined.A.BTest();
  }

  method KeyTest(b:OutputRefined.A.Key)
  {
    OutputRefined.Test(b);
  }
}


// Try refining the result of a functor application
abstract module FunctorAppRefiner refines P(ABase) {
  method MoreTest(x:A.Key) {
    Test(x);
  }
}


