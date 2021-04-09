// RUN: %dafny /compile:0 /print:"%t.print" /dprint:"%t.dprint" /autoTriggers:0 "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

glinear datatype F = F

glinear method maybe_swap(glinear x: F, glinear y: F, ghost b: bool)
returns (glinear x': F, glinear y': F)
{
  if b {
    x' := y;
    y' := x;
  } else {
    x' := x;
    y' := y;
  }
}
