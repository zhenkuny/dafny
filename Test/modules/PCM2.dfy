module GhostLoc {
  datatype Loc =
    | BaseLoc(ghost t: nat)
    | ExtLoc(ghost s: nat, ghost base_loc: Loc)
}
abstract module PCM {
  import opened GhostLoc
  type M(!new)
  predicate transition(a: M, b: M)
  least predicate reachable(a: M, b: M) {
    a == b || (exists z :: reachable(a, z) && transition(z, b))
  }
}
module Tokens(pcm: PCM) {
  import opened GhostLoc
  type {:extern} Token(!new) {
    function {:extern} loc() : Loc
    function {:extern} get() : pcm.M
  }
}
abstract module PCMExt(Base: PCM) refines PCM {
  type B = Base.M
  type F = M
}
module PCMExtMethods(Base: PCM, Ext: PCMExt(Base)) {
  type B = Base.M
  type F = Ext.M
  import BaseTokens = Tokens(Base)
  import ExtTokens = Tokens(Ext)
}
