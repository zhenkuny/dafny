abstract module Rw { }

module RwTokens(rw: Rw) { }

abstract module StoredTypeModule { }

module RwLock(stm: StoredTypeModule) refines Rw { }

module RwLockTokens(stm: StoredTypeModule) {
  import rwlock = RwLock(stm)
  import T = RwTokens(RwLock(stm))
}

module RwLockImpl(stm: StoredTypeModule) {
  import RwLock = RwLock(stm)
  import RwLockTokens = RwLockTokens(stm)
}
