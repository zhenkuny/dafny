namespace _module {

struct Foo {
  int x;
};

Foo get_Foo_default() {
  Foo foo;
  foo.x = 0;
  return foo;
}

}
