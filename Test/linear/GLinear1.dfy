// RUN: %dafny /compile:0 /print:"%t.print" /dprint:"%t.dprint" /autoTriggers:0 "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

// ---------- succeeds ------------------------------------


// ---------- fails ------------------------------------

glinear datatype DX0 = DX0()
gshared datatype DX1 = DX1()
shared datatype DX2 = DX2()
