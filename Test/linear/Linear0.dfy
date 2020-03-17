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

