abstract module Ifc {
  type TransitionLabel(==,!new)
}

module MapIfc {
  datatype TransitionLabel =
    | Query(k: int, value: int)
    | Insert(k: int, value: int)
}

abstract module StateMachine(ifc: Ifc) {
  type Variables(==,!new)
  predicate Init(s: Variables)
  predicate Next(s: Variables, s': Variables, tlabel: Ifc.TransitionLabel)
}

// error: MapIfc doesn't refine Ifc
module MapStateMachineWrong1 refines StateMachine(MapIfc)
{
}

// error: Ifc is abstract
module MapStateMachineWrong2 refines StateMachine(Ifc)
{
}
