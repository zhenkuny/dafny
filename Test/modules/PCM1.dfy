module GhostLoc {
  datatype Loc =
    | BaseLoc(ghost t: nat)
    | ExtLoc(ghost s: nat, ghost base_loc: Loc)
}
abstract module PCM {
  import opened GhostLoc
  type M(!new)
}
module Tokens(pcm: PCM) {
  import opened GhostLoc
  type {:extern} Token(!new)

  function {:extern} loc(t:Token) : Loc
  function {:extern} get(t:Token) : pcm.M
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
  function method {:extern} ext_init(
      linear b: BaseTokens.Token,
      ghost f': F)
   : (linear f_out: ExtTokens.Token)
  ensures ExtTokens.loc(f_out).ExtLoc? && ExtTokens.loc(f_out).base_loc == BaseTokens.loc(b)
  ensures ExtTokens.get(f_out) == f'
}
