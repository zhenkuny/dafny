// RUN: %dafny /compile:0 /print:"%t.print" /dprint:"%t.dprint" /autoTriggers:0 "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

// ---------- succeeds ------------------------------------

function method F1(linear x_in:int):linear int
{
    x_in
}

function method F2(linear x_in:int):(linear x:int)
{
    F1(x_in)
}

method M1(linear x_in:int) returns(linear x:int)
    ensures x == x_in
{
    x := x_in;
    x := F2(x);
}

method M2(linear x_in:int) returns(linear x:int)
    ensures x == x_in
{
    x := M1(x_in);
}

method N1(linear x_in:int, linear y_in:int) returns(linear x:int, linear y:int)
    ensures x == x_in
{
    x := x_in;
    x := F2(x);
    y := y_in;
}

method N2(linear x_in:int, linear y_in:int) returns(linear x:int, linear y:int)
    ensures x == x_in
{
    x, y := N1(x_in, y_in);
}

method S0(linear l_in:int, shared s_in:int) returns(linear l:int)
{
  l := l_in;
}

method S1(linear l_in:int, shared s_in:int) returns(linear l:int, shared s1:int, shared s2:int)
{
  l := S0(l_in, s_in);
  s1 := s_in;
  s2 := s_in;
}

method S2(linear x_in:int, linear y_in:int) returns(linear x:int, linear y:int)
{
  x := S0(x_in, y_in);
  y := y_in;
}

// ---------- fails ------------------------------------

function method F1_a(x_in:int):linear int
{
    x_in
}

function method F1_b(linear x_in:int):int
{
    x_in
}

function method F1_c(linear x_in:int, linear y_in:int):linear int
{
    x_in
}

function method F1_d(linear x_in:int, linear y_in:int):linear int
{
    x_in + y_in
}

function method F1_e():linear int
{
    7
}

function method F2_a(linear x_in:int):(linear x:int)
{
    F1_c(x_in, x_in)
}

function method F2_b(linear x_in:int):(linear x:int)
{
    F1_e()
}

method M1_a(x_in:int) returns(linear x:int)
    ensures x == x_in
{
    x := x_in;
    x := F2(x);
}

method M1_b(linear x_in:int) returns(x:int)
    ensures x == x_in
{
    x := x_in;
    x := F2(x);
}

method M1_c(linear x_in:int, linear y_in:int) returns(linear x:int)
    ensures x == x_in
{
    x := x_in;
    x := F2(x);
}

method M1_d(linear x_in:int) returns(linear x:int, linear y:int)
    ensures x == x_in
{
    x := x_in;
    x := F2(x);
}

method M1_e() returns(linear x:int)
{
}

method M2_a(linear x_in:int) returns(linear x:int)
    ensures x == x_in
{
    x := M1_c(x_in, x_in);
}

method M2_b(linear x_in:int) returns(linear x:int)
    ensures x == x_in
{
    x := M1_e();
}

method S1_a(linear l_in:int, shared s_in:int) returns(shared s1:int, shared s2:int)
{
  s1 := l_in;
  s2 := s_in;
}

method S1_b(shared s_in:int) returns(linear l:int, shared s1:int, shared s2:int)
{
  l := s_in;
  s1 := s_in;
  s2 := s_in;
}

method S2_a(linear x_in:int, linear y_in:int) returns(linear x:int, linear y:int, shared s1:int, shared s2:int)
{
  x, s1, s2 := S1(x_in, y_in);
  y := y_in;
}

method Lambda0(linear x:int)
{
  var f := (i:int) => x;
}

method Lambda1(shared x:int)
{
  var f := (i:int) => x;
}
