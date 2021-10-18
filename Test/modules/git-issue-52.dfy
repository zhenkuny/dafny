abstract module NRIfc { }

module ContentsTypeMod {
}

module NodeReplica(nrifc: NRIfc) refines ContentsTypeMod { }

module RwLockImpl(contentsTypeMod: ContentsTypeMod) {
}

module Impl(nrifc: NRIfc) {
  import opened NodeReplicaApplied = NodeReplica(nrifc)
  import opened RwLockImpl(NodeReplicaApplied)
}

module ConcreteNrifc2 refines NRIfc { }
module ConcreteNrifc1 refines NRIfc { }

import X = Impl(ConcreteNrifc1)
import Y = Impl(ConcreteNrifc2)
