// RUN: %dafny /compile:0 /print:"%t.print" /dprint:"%t.dprint" "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

module ABase {
    type Key
}

//abstract module P_normal {
//  import A : ABase
//
//  method Test(a:A.Key)
//}

// Use an element of a formal parameter
// Morally equivalent to P_normal above
abstract module P(A: ABase) {
  method Test(a:A.Key)
}

// Try simple functor application
//abstract module Apply {
//  import Output = P(ABase)
//
//  method More(a:Output.A.Key) {
//    Output.Test(a);
//  }
//}

// Make sure functors behave applicatively
//abstract module Apply2 {
//  import Output0 = P(ABase)
//  import Output1 = P(ABase)
//
//  method More(a:Output0.A.Key, b:Output1.A.Key)
//    requires a == b
//  {
//    Output1.Test(a);
//  }
//}

// Try passing a refinement to a functor
abstract module B refines ABase {
  method BTest()
}

abstract module ApplyRefinement {
  import Output = P(B)

  method UseB() {
    Output.A.BTest();
  }

  method KeyTest(b:Output.A.Key)
  {
    Output.Test(b);
  }
}

// Try passing module of the wrong type to a functor
//abstract module NoBase {
//}
//
//abstract module BadApplication {
//  import ShouldFail = P(NoBase)
//}




//abstract module B refines P(ABase) {
//}
//
//abstract module C {
//  import X = P(ABase)
//}

//module InnocentA refines ABase {
//    type Key = int
//}
//
//abstract module B_good {
//    import P1 = P(InnocentA)
//    import P2 = P(InnocentA)
//    lemma foo(a: P1.A.Key, b: P2.A.Key)
////        ensures a == b  // succeeds, as it should
////    { }
//}


/*
abstract module MissingParameter {
    import P    // Fails with: P expects 1 arguments
}

abstract module B_bad_compare_to_abstract {
    module Q refines ABase { }
    import P1 = P(InnocentA)
    lemma foo(a: Q.Key, b: P1.A.Key)
        ensures a == b // Fails with: not-comparable types: Q.Key, P1.A.Key
    { }
}

abstract module B_bad {
    import P1 = P(InnocentA)
    import P2 = P(SinisterA)
    lemma foo(a: P1.A.Key, b: P2.A.Key)
        ensures a == b // Fails with: mismatched types: P1.A.Key, P2.A.Key
    { }
}
*/
