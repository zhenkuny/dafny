#include "DafnyRuntime.h"

template <typename A>
typedef struct {
  uint64 length;
  A* elts;
} linear_seq;

template <typename A>
A seq_get(linear_seq s, uint64 i) {
  return s.elts[i];
}

template <typename A>
linear_seq seq_set(linear_seq s, uint64 i, A a) {
  s.elts[i] = a;
  return s;
}

template <typename A>
uint64 seq_length(linear_seq s) {
  return s.length;
}

template <typename A>
linear_seq seq_alloc(uint64 length) {
  linear_seq ret;
  ret.length = length;
  ret.elts = new A[length];
  return ret;
}


