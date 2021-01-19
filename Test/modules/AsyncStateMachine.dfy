// Ifc, StateMachine

abstract module Ifc {
  type Op
}

abstract module StateMachine(Ifc: Ifc) {
  type Variables
  predicate Init(s: Variables)
  predicate Next(s: Variables, s': Variables, op: Ifc.Op)
}

// Async state machines

abstract module InputOutputIfc refines Ifc {
  type Input
  type Output

  datatype Op = Op(input: Input, output: Output)
}

module AsyncIfc(Ifc: InputOutputIfc) {
  type {:extern} RequestId(==,!new)

  type Op =
    | Start(rid: RequestId, input: Ifc.Input)
    | End(rid: RequestId, output: Ifc.Output)
    | InternalOp
}

module AsyncSpec(InnerIfc: InputOutputIfc, SM: StateMachine(InnerIfc))
    refines StateMachine(AsyncIfc(InnerIfc))
{
  type Variables = Variables(
      s: SM.Variables,
      reqs: map<RequestId, InnerIfc.Input>,
      resps: map<RequestId, InnerIfc.Output>)

  predicate Init(s: Variables)
  {
    && SM.Init(s.s)
    && s.reqs == map[]
    && s.resps == map[]
  }

  predicate Next(s: Variables, s': Variables, op: Ifc.Op)
  {
    match op {
      case Start(rid, input) =>
        // add 'input' to 'reqs'
        && s' == s.(reqs := s.reqs[rid := input])
        && rid !in s.reqs
      case InternalOp => (
        // stutter step
        || (s' == s)
        // resolve request step
        // serialization point: remove 'input' from 'reqs',
        // add 'output' to 'resps'
        || (exists rid, input, output ::
          && rid in s.reqs
          && s.reqs[rid] == input
          && s'.reqs == MapRemove1(s.reqs, rid)
          && s'.resps == s.resps[rid := output]
          && SM.Next(s.s, s'.s, Ifc.Op(input, output))
        )
      )
      case End(rid, output) =>
        // remove from 'resps'
        && s == s'.(resps := s'.resps[rid := output])
        && rid !in s'.resps
    }
  }
}

// MapSpec

module MapIfc refines InputOutpuIfc {
  type Key = int
  type Value = int

  type Input =
    | QueryRequest(key: Key)

  type Output
    | QueryResponse(value: Value)
}

module MapSpec refines StateMachine(MapIfc) {
  datatype Variables = Variables(m: map<Key, Value>)

  predicate Init(s: Variables) {
    s.m == map[]
  }

  predicate Next(s: Variables, s': Variables, uiop: MapIfc.Op) {
    && s'.m == s.m
    && uiop.input.QueryRequest?
    && uiop.output.QueryResponse?
    && uiop.input.key in s.m
    && uiop.output.value == s.m[uiop.input.key]
  }
}
