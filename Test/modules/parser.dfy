// Int stuff

// Simplifying the example to use int instead of NativeTypes.byte makes the problem disappear
module NativeTypes {
  newtype{:nativeType "byte"} byte = i:int | 0 <= i < 0x100
}

abstract module NativePackedInt {
  import opened NativeTypes

  type Integer

  function unpack(s: seq<byte>) : Integer
}

module NativePackedUint32 refines NativePackedInt {
  type Integer = int
}

// Marshalling

abstract module Marshalling {
  import opened NativeTypes

  type UnmarshalledType

  function parse(data: seq<byte>) : UnmarshalledType
}

// Integer marshalling

abstract module IntegerMarshalling(Int: NativePackedInt) refines Marshalling {
  type UnmarshalledType = Int.Integer

  function parse(data: seq<byte>) : UnmarshalledType
  {
    Int.unpack(data)
  }
}

module Uint32Marshalling refines IntegerMarshalling(NativePackedUint32) {
  // ERR:
  // module-test.dfy[Uint32Marshalling](36,15): Error: type mismatch for argument (function expects seq<byte>, got seq<byte>) (covariant type parameter would require byte <: byte)
}

/*
// Works on normal Dafny
module Uint32Marshalling refines IntegerMarshalling {
  import Int = NativePackedUint32
}
*/
