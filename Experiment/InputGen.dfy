module {:extern "InputGen"} InputGen {
  // const BASE_256: int := 115792089237316195423570985008687907853269984665640564039457584007913129639936

  const POW2_8: int := 256

  type uint1 = i: int
    | 0 <= i < 2

  type uint = i: int
    | 0 <= i < POW2_8

  function method BASE(): int
  {
    POW2_8
  }

  class Gen {
    static method {:extern "InputGen.Gen", "GetRandomUint"} GetRandomUint() returns (r: uint)
  }
}