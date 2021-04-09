// RUN: %dafny /compile:0 /print:"%t.print" /dprint:"%t.dprint" /autoTriggers:0 "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

// ---------- succeeds ------------------------------------

glinear datatype lOption<A> = LNone | LSome(glinear a:A)
glinear datatype nlPair<A, B> = NlPair(ghost a:A, glinear b:B)
glinear datatype llPair<A, B> = LlPair(glinear a:A, glinear b:B)
glinear datatype nlList<A> = NlNil | NlCons(ghost hd:A, glinear tl:nlList<A>)
datatype list<A> = Nil | Cons(hd:int, tl:list)

function method GetInt(gshared x:int):int
glinear function method ShareInt(gshared x:int):gshared int

glinear function method LOptionGet<A>(glinear o:lOption<A>):glinear A
    requires o.LSome?
{
    glinear var LSome(a) := o;
    a
}

glinear function method SOptionGet<A>(gshared o:lOption<A>):gshared A
    requires o.LSome?
{
    o.a
}

glinear function method IncrAll1(glinear l:nlList<int>):glinear nlList<int>
{
    glinear match l
    {
        case NlNil => NlNil
        case NlCons(hd, tl) => NlCons(hd + 1, IncrAll1(tl))
    }
}

glinear function method IncrAll2(glinear l:nlList<int>):glinear nlList<int>
{
    if l.NlNil? then l
    else
    (
        glinear var NlCons(hd, tl) := l;
        NlCons(hd + 1, IncrAll2(tl))
    )
}

glinear function method Share1(glinear p:nlPair<int, int>):glinear nlPair<int, int>
{
    var x:int := p.a + p.a;
    var y:int := (gshared var NlPair(sa, sb) := p; sa + GetInt(sb));
    glinear var NlPair(_, b) := p;
    NlPair(x, b)
}

glinear function method Share2(glinear l:nlList<int>):glinear nlList<int>
{
    var x:int := if l.NlNil? then 0 else l.hd + l.hd;
    var y:int := (
        gshared match l
        {
            case NlNil => 0
            case NlCons(sa, sb) => sa + 1
        }
    );
    glinear match l
    {
        case NlNil => NlNil
        case NlCons(_, b) => NlCons(x + y, b)
    }
}

glinear method M(glinear l_in:nlList<int>, gshared s:nlList<int>, gshared nl:nlPair<int, nlPair<int, int>>) returns(glinear l:nlList<int>)
{
    l := l_in;
    glinear match l
    {
        case NlNil => l := NlCons(10, NlNil);
        case NlCons(hd, tl) => l := NlCons(hd, tl);
    }
    var i := l.hd + l.hd;
    var k:int := (gshared var NlCons(si, sy) := l; si + 1);
    var nla := nl.b.a;
    gshared var nlb := nl.b.b;
    gshared match s
    {
        case NlNil => k := k + 1;
        case NlCons(hd, tl) => k := k + hd + 1;
    }
    glinear var NlCons(a, b) := l;
    l := b;
    if (s.NlCons?)
    {
        gshared var NlCons(sa, sb) := s;
        k := k + sa + 1;
    }
}

glinear method TupleGood(gshared x:(int, glinear int), glinear l:int) returns(glinear q:int)
{
    var i := x.0;
    gshared var j := x.1;
    glinear var z:(int, glinear int) := (i, glinear l);
    glinear var (a, glinear b) := z;
    q := b;
}

type TX
function operator(| |)(tx:TX):nat
datatype dx = DX(tx:TX)
function MX(d:dx):nat
{
    match d { case DX(tx) => |tx| }
}

glinear datatype fd = FD(ghost i:int)
{
  glinear function method f1():(glinear r:fd) {this}
  function f2():int {this.i}
  glinear function method f3():(glinear r:fd) {this.f1()}
  glinear method m1() returns(glinear r:fd) {r := this;}
  glinear method m2() returns(glinear r:fd) {r := m1();}
  glinear method m3() returns(glinear r:fd) {var i := f2(); r := m1();}
}

glinear datatype D0 = D0
glinear datatype Dg = Dg(ghost g:int)

glinear method TestD0(glinear x:D0)
{
    glinear var D0() := x; // note:it's important to allow the "()"; this is *not* the same as "glinear var D0 := x;"
}

glinear method TestDg(glinear x:Dg)
{
    glinear var Dg(g) := x;
}

//
//
//
//
//
//
//
//
//
//
//
//
//
//
//
//
//
//
//
//
//
//
//
//
//
//

// ---------- fails ------------------------------------

glinear function method LOptionGet_b<A>(glinear o:lOption<A>):glinear lOption<A>
    requires o.LSome?
{
    gshared var LSome(a) := o;
    o
}

glinear function method LOptionGet_c<A>(glinear o:lOption<A>):glinear lOption<A>
    requires o.LSome?
{
    gshared match o
    {
        case LSome(a) => o
    }
}

glinear function method SOptionGet_a<A>(glinear o:lOption<A>):glinear A
    requires o.LSome?
{
    o.a
}

glinear function method F_e(gshared i:int):gshared nlPair<int, int>
{
    NlPair(i, i)
}

glinear function method F_f(glinear i:int):gshared nlPair<int, int>
{
    NlPair(i, i)
}

glinear function method F_h(gshared i:int):glinear nlPair<int, int>
{
    NlPair(i, i)
}

glinear function method F_i(glinear i:int):glinear nlPair<int, int>
{
    NlPair(i, i)
}

glinear function method G_f(glinear p:nlPair<int, int>):glinear nlPair<int, int>
{
    gshared var NlPair(a, b) := p; p
}

glinear function method H_b(gshared p:nlPair<int, int>):gshared int
{
    var NlPair(a, b) := p; a
}

glinear function method H_d(gshared p:nlPair<int, int>):glinear int
{
    var NlPair(a, b) := p; a
}

glinear function method G_f'(glinear p:nlPair<int, int>):glinear nlPair<int, int>
{
    gshared match p {case NlPair(a, b) => p}
}

glinear function method H_b'(gshared p:nlPair<int, int>):gshared int
{
    match p {case NlPair(a, b) => a}
}

glinear function method H_d'(gshared p:nlPair<int, int>):glinear int
{
    match p {case NlPair(a, b) => a}
}

glinear method G_f''(glinear p:nlPair<int, int>) returns(glinear x:nlPair<int, int>)
{
    gshared var NlPair(a, b) := p; x := p;
}

glinear method H_b''(gshared p:nlPair<int, int>) returns(gshared x:int)
{
    var NlPair(a, b) := p; x := a;
}

glinear method H_d''(gshared p:nlPair<int, int>) returns(glinear x:int)
{
    var NlPair(a, b) := p; x := a;
}

glinear method G_f'''(glinear p:nlPair<int, int>) returns(glinear x:nlPair<int, int>)
{
    gshared match p {case NlPair(a, b) => x := p;}
}

glinear method H_b'''(gshared p:nlPair<int, int>) returns(gshared x:int)
{
    match p {case NlPair(a, b) => x := a;}
}

glinear method H_d'''(gshared p:nlPair<int, int>) returns(glinear x:int)
{
    match p {case NlPair(a, b) => x := a;}
}

glinear function method Match_a(glinear l:nlList<int>, glinear r:nlList<int>):glinear nlList<int>
{
    glinear match l
    {
        case NlNil => r
        case NlCons(a, b) => b
    }
}

glinear function method Match_b(glinear l:nlList<int>, glinear r:nlList<int>):glinear nlList<int>
{
    glinear match l
    {
        case NlCons(a, b) => b
        case NlNil => r
    }
}

glinear method Match_c(glinear l:nlList<int>, glinear r:nlList<int>) returns(glinear x:nlList<int>)
{
    glinear match l
    {
        case NlNil => x := r;
        case NlCons(a, b) => x := b;
    }
}

glinear method Match_d(glinear l:nlList<int>, glinear r:nlList<int>) returns(glinear x:nlList<int>)
{
    glinear match l
    {
        case NlCons(a, b) => x := b;
        case NlNil => x := r;
    }
}

glinear method Match_e(glinear l:nlList<int>) returns(glinear x:nlList<int>)
{
    glinear match l
    {
        case NlNil => {}
        case NlCons(a, b) => x := b;
    }
}

glinear method Match_f(glinear l:nlList<int>) returns(glinear x:nlList<int>)
{
    glinear match l
    {
        case NlCons(a, b) => x := b;
        case NlNil => {}
    }
}

glinear method Vars_a() returns(glinear l:nlList<int>)
{
    l := NlCons(10, NlNil);
    gshared var NlCons(si, sy) := l;
}

glinear method Vars_b() returns(glinear l:nlList<int>)
{
    l := NlCons(10, NlNil);
    glinear var ltl := (gshared var NlCons(li, ly) := l; ly);
}

glinear method Vars_c() returns(glinear l:nlList<int>)
{
    l := NlCons(10, NlNil);
    gshared var ltl := (gshared var NlCons(li, ly) := l; ly);
}

glinear method Leak_a'()
{
    glinear var l:nlList<int> := NlNil;
}

glinear method Leak_b'()
{
    glinear var NlCons(a, b) := NlCons(10, NlNil);
}

glinear method Leak_c'()
{
    glinear match NlCons(10, NlNil) {case NlCons(a, b) => {}}
}

glinear function method id<A>(glinear a:A):glinear A

glinear method ExpSelect(glinear l:nlPair<int, nlPair<int, int>>, gshared s:nlPair<int, nlPair<int, int>>)
{
    var x1 := l.b.a; // ok: borrow l
    gshared var x2 := l.b.b; // can't smuggle gshared value into x2
    gshared var x3 := s.b.a; // can't assign ghost to gshared (TODO: currently, the resolver just demotes x3 to ghost, which is sound but not what we want)
    var x4 := s.b.b; // can't assign ordinary to gshared
    ghost var y1 := id(l).b.a; // ok, this is ghost
}

glinear method TupleBad(gshared x:(int, glinear int), glinear l:int) returns(glinear q:int)
{
    gshared var i := x.0;
    var j := x.1;
    glinear var z:(int, glinear int) := (i, glinear l);
    glinear var (a, glinear b) := z;
    q := b;
}

glinear datatype fd' = FD'(ghost i:int)
{
  glinear function method f1():(glinear r:fd') {FD'(this.i)}
  gshared function method f2():int {f1().i}
  function method f3():int {f2()}
  glinear method m1() returns(glinear r:fd') {r := FD'(this.i);}
  method m2() returns(glinear r:fd') {r := m1();}
}
