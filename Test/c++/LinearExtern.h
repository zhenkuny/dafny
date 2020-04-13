#include "DafnyRuntime.h"

#include <vector>

namespace LinearExtern {

template <typename A>
using linear_seq = std::vector<A>;

template <typename A>
A seq_get(linear_seq<A> s, uint64 i) {
  return s[i];
}

template <typename A>
linear_seq<A> seq_set(linear_seq<A> s, uint64 i, A a) {
  s[i] = a;
  return s;
}

template <typename A>
uint64 seq_length(linear_seq<A> s) {
  return s.size();
}

template <typename A>
linear_seq<A> seq_alloc(uint64 length) {
  linear_seq<A> ret;
  ret.assign(length, get_default<A>::call());
  return ret;
}

template <typename A>
Tuple0 seq_free(linear_seq<A> s) {
  s.clear();
  Tuple0 ret;
  return ret;
}

template <typename A>
DafnySequence<A> seq_unleash(linear_seq<A> s) {
  DafnySequence<A> ret(s);  // Copies contents of s into ret
  seq_free(s);
  return ret;
}

}
