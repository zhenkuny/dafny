// implicit params now

abstract module Ifc {
  type TransitionLabel(==,!new)
}

module MapIfc refines Ifc {
  datatype TransitionLabel =
    | Query(k: int, value: int)
    | Insert(k: int, value: int)
}

abstract module StateMachine(ifc: Ifc) {
  type Variables(==,!new)
  predicate Init(s: Variables)
  predicate Next(s: Variables, s': Variables, l: ifc.TransitionLabel)
}

module MapStateMachine refines StateMachine(MapIfc)
{
  type Variables = map<int, int>
  predicate Init(s: Variables)
  {
    s == map[]
  }
  predicate Next(s: Variables, s': Variables, l: ifc.TransitionLabel)
  {
    && (l.Query? ==> l.k in s && l.v == s[k] && s' == s)
    && (l.Insert? ==> s' == s[l.k := l.v])
  }
}

abstract module StateMachineRefinement(
    L: StateMachine,
    H: StateMachine)
requires L.ifc == H.ifc
{
  import ifc = L.ifc

  function I(s: L.Variables) : H.Variables

  lemma InitRefinement(s: L.Variables)
  requires L.Init(s)
  ensures H.Init(I(s))

  lemma NextRefinement(s: L.Variables, s': L.Variables, l: ifc.TransitionLabel)
  requires L.Next(s, s', l)
  ensures H.Next(I(s), I(s'), l)
}

module ComposeRefinements(
    Ref1: StateMachineRefinement,
    Ref2: StateMachineRefinement,
)
  requires Ref1.H == Ref2.L
  refines StateMachineRefinement(ifc, P, R)
{
  // Check that Ref1.L.ifc == Ref2.H.ifc
  lemma random_lemma(x: Ref1.L.ifc.TransitionLabel, y: Ref2.H.ifc.TransitionLabel)
  requires x == y

  function I(s: L.Variables) : H.Variables
  {
    Ref2.I(Ref1.I(s))
  }

  lemma InitRefinement(s: L.Variables)
  requires L.Init(s)
  ensures H.Init(I(s))
  {
    Ref1.InitRefinement(s);
    Ref2.InitRefinement(Ref1.I(s));
  }

  lemma NextRefinement(s: L.Variables, s': L.Variables, l: ifc.TransitionLabel)
  requires L.Next(s, s', l)
  ensures H.Next(I(s), I(s'), l)
  {
    Ref1.NextRefinement(s, s', l);
    Ref2.NextRefinement(Ref1.I(s), Ref1.I(s'), l);
  }
}

module MapStateMachine2 refines StateMachine(MapIfc)
{
  datatype Variables = X(m: map<int, int>)
  predicate Init(s: Variables)
  {
    s.m == map[]
  }
  predicate Next(s: Variables, s': Variables, l: ifc.TransitionLabel)
  {
    && (l.Query? ==> l.k in s.m && l.v == s.m[k] && s.m' == s.m)
    && (l.Insert? ==> s.m' == s.m[l.k := l.v])
  }
}

module MapStateMachine3 refines StateMachine(MapIfc)
{
  datatype Variables = Y(n: map<int, int>)
  predicate Init(s: Variables)
  {
    s.n == map[]
  }
  predicate Next(s: Variables, s': Variables, l: ifc.TransitionLabel)
  {
    && (l.Query? ==> l.k in s.n && l.v == s.n[k] && s.n' == s.n)
    && (l.Insert? ==> s.n' == s.n[l.k := l.v])
  }
}

module Refinement_1_2 refines StateMachineRefinement(MapStateMachine, MapStateMachine2)
{
  function I(s: L.Variables) : H.Variables
  {
    H.Variables(s)
  }

  lemma InitRefinement(s: L.Variables)
  requires L.Init(s)
  ensures H.Init(I(s))
  {
  }

  lemma NextRefinement(s: L.Variables, s': L.Variables, l: ifc.TransitionLabel)
  requires L.Next(s, s', l)
  ensures H.Next(I(s), I(s'), l)
  {
    assert l.Query? || l.Insert?;
  }
}

module Refinement_2_3 refines StateMachineRefinement(MapStateMachine2, MapStateMachine3)
{
  function I(s: L.Variables) : H.Variables
  {
    H.Variables(s.m)
  }

  lemma InitRefinement(s: L.Variables)
  requires L.Init(s)
  ensures H.Init(I(s))
  {
  }

  lemma NextRefinement(s: L.Variables, s': L.Variables, l: ifc.TransitionLabel)
  requires L.Next(s, s', l)
  ensures H.Next(I(s), I(s'), l)
  {
  }
}

module Final {
  import BigRef = ComposeRefinements(
      Refinement_1_2,
      Refinement_2_3)
  import A = MapStateMachine
  import B = MapStateMachine3
  import MapIfc

  lemma stuff() {
    var s : A.Variables := map[];
    assert BigRef.I(s) == B.Variables(map[])
    BigRef.InitRefinement(s);

    BigRef.NextRefinement(
        map[1 := 2],
        map[1 := 2],
        MapIfc.Query(1, 2));
  }
}
