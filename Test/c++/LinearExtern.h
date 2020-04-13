#include "DafnyRuntime.h"

#include <vector>

namespace LinearExtern {

////////////////////////////////////////////////////////////
//
//   Linear sequences
//
////////////////////////////////////////////////////////////

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

////////////////////////////////////////////////////////////
//
//   Maybe
//
////////////////////////////////////////////////////////////

template <typename A>
struct maybe {
  A a;
};

template <typename A>
A peek(maybe<A> m) { return m.a; }

template <typename A>
A unwrap(maybe<A> m) { return m.a; }

template <typename A>
maybe<A> give(A a) { return maybe(a); }

template <typename A>
//maybe<A> empty() { return maybe(get_default<A>::call()); }
maybe<A> empty() { return maybe(get_default<A>::call()); }    // REVIEW: Safe, b/c !has ?

template <typename A>
Tuple0 discard(maybe<A> m) { m; Tuple0 ret; return ret; } 

////////////////////////////////////////////////////////////
//
//   lseqs
//
////////////////////////////////////////////////////////////
template <typename A>
using lseq = std::vector<A>;

template <typename A>
uint64 lseq_length_raw(lseq<A> s) {
  return s.size();
}

template <typename A>
lseq<A> lseq_alloc_raw(uint64 length) {
  lseq<A> ret;
  ret.assign(length, get_default<A>::call());
  return ret;
}

template <typename A>
Tuple0 lseq_free_raw(lseq<A> s) {
  s.clear();
  Tuple0 ret;
  return ret;
}

template <typename A>
Tuple2<lseq<A>, maybe<A>> lseq_swap_raw_fun(lseq<A> s1, uint64 i, maybe<A> a1) {
  Tuple2 ret(s1, s1[i]);
  s1[i] = a1;
  return ret;
}

template <typename A>
maybe<A> lseq_share_raw(lseq<A> s, uint64 i) {
  return s[i];
}

}
