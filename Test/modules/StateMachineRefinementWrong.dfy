
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
// Without the requires clause: requires L.ifc == H.ifc
// This is gonna fail
{
  import ifc = L.ifc

  function I(s: L.Variables) : H.Variables

  lemma InitRefinement(s: L.Variables)
  requires L.Init(s)
  ensures H.Init(I(s))

  lemma NextRefinement(s: L.Variables, s': L.Variables, l: ifc.TransitionLabel)
  requires L.Next(s, s', l)
  ensures H.Next(I(s), I(s'), l) // error: L.ifc doesn't match H.ifc
}

