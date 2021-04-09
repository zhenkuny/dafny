// RUN: %dafny /compile:0 /print:"%t.print" /dprint:"%t.dprint" /autoTriggers:0 "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

// ---------- succeeds ------------------------------------

linear datatype D0 = D0(glinear g:int)

function method F0(gshared x:int):bool

method M0(glinear x:int) returns(linear y:D0)
{
  y := D0(x);
  linear var D0(g) := y;
  y := D0(g);
  linear match y
  {
    case D0(h) => {y := D0(h);}
  }
  var b:bool := (gshared var s:int := y.g; F0(s));
}

// ---------- fails ------------------------------------

method M0a(glinear x:int) returns(linear y:D0)
{
  y := D0(x);
  glinear var D0(g) := y;
  y := D0(g);
}

method M0b(glinear x:int) returns(linear y:D0)
{
  y := D0(x);
  linear match y
  {
    case D0(h) => {}
  }
}

method M0c(glinear x:int) returns(linear y:D0)
{
  y := D0(x);
  var b:bool := (shared var s:int := y.g; F0(s));
}

method M1(linear x:int) returns(linear y:D0)
{
  y := M0(x);
}

method M2(linear x:int) returns(glinear y:D0)
{
  y := M1(x);
}

method M3(glinear x:int) returns(glinear y:D0)
{
  y := M2(x);
}

glinear datatype D = D(glinear i:int)

function method F1(gshared d:D):bool {
  gshared var D(i) := d;
  true
}

function method F2(gshared d:D):bool {
  gshared match d { case D(i) => true }
}

method X1(gshared d:D)
{
  gshared var i := d.i;
}

method X2(gshared d:D)
{
  gshared var D(i) := d;
}

method X3(gshared d:D)
{
  gshared match d { case D(i) => {} }
}

