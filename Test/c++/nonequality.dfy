// RUN: %dafny /compile:3 /spillTargetCode:2 /compileTarget:cpp nonequality_extern.h "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

type {:extern "predefined"} Foo

datatype X = X(foo: Foo, b: bool)

method Main() {
  print "Hi\n";
}
