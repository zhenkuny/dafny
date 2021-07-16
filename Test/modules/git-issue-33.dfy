abstract module PCM {
}

abstract module BasicPCM refines PCM {
}

module Tokens(pcm: PCM) {
  datatype Token = Token(loc: nat, val: nat)

  function method get_unit(loc: nat) : Token
}

abstract module PCMExt(base: PCM) refines PCM {
}

abstract module PCMWrap refines PCM {
  function singleton_loc(): nat
}

module PCMWrapTokens(pcm: PCMWrap) {
  import T = Tokens(pcm)
}

module ExtTokens(base: PCM, ext: PCMExt(base)) {
  import ExtTokens = Tokens(ext)
  import BaseTokens = Tokens(base)

  function ext_init(b: BaseTokens.Token) : (f_out: ExtTokens.Token)
}

abstract module RW {
}

module RW_PCMWrap(rw: RW) refines PCMWrap {
}

module RW_PCMExt(rw: RW) refines PCMExt(RW_PCMWrap(rw)) {
}

module RWTokens(rw: RW) {
  import Wrap = RW_PCMWrap(rw)
  import MyBaseTokens = Tokens(RW_PCMWrap(rw))

  import T = Tokens(RW_PCMExt(rw))
  import ET = ExtTokens(Wrap, RW_PCMExt(rw))

  method init()
  returns (t: T.Token)
  {
    ghost var base_loc := Wrap.singleton_loc();
    t := ET.ext_init(MyBaseTokens.get_unit(base_loc));
  }
}
