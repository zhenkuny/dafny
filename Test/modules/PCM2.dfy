abstract module PCM {
  type M(!new)
  least predicate reachable(a: M, b: M) {
    a == b || (exists z :: reachable(a, z))
  }
}
abstract module PCMExt(Base: PCM) refines PCM {
}
module PCMExtMethods(Base: PCM, Ext: PCMExt(Base)) {
  type B = Base.M
}
