abstract module NativePackedInt {
  type Integer
}

module NativePackedUint32 refines NativePackedInt {
  type Integer = int
}

// Marshalling
abstract module Marshalling {
  type UnmarshalledType
}

// Integer marshalling
abstract module IntegerMarshalling(Int: NativePackedInt) refines Marshalling {
  type UnmarshalledType = Int.Integer
}

// Sequence marshalling
abstract module SeqMarshalling(ElementMarshalling: Marshalling) refines Marshalling {
  type Element = ElementMarshalling.UnmarshalledType
  type UnmarshalledType = Element
}

abstract module Uniform(elementMarshalling: Marshalling)
  refines SeqMarshalling(elementMarshalling) {
  predicate parse_prefix(x:elementMarshalling.UnmarshalledType, y: UnmarshalledType)
    requires x == y
}

abstract module IntegerSeqMarshalling(Int: NativePackedInt) {
  import B = Uniform(IntegerMarshalling(Int))
}

