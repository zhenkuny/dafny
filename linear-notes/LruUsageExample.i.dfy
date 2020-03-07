include "LruModel.i.dfy"
include "LruImpl.i.dfy"

module LruUsageExample {
  import LruModel
  import LruImpl

  // Some special syntax or something
  implement
    !LruModel.LruQueue
  with
    class LruImpl.LruImplQueue

  function remove2(queue: !LruModel.LruQueue, x: uint64, y: uint64) :
      (queue': !LruModel.LruQueue)
  requires LruModel.WF(queue)
  {
    var queue0 := queue;
    var queue1 := LruModel.Remove(queue0, x);
    var queue2 := LruModel.Remove(queue1, x);
    queue2
  }

  // This would be translated into the following:
  //
  // method remove2Impl(queue: LruImpl.LruImplQueue, x: uint64, y: uint64)
  // requires queue.Inv()
  // modifies queue.Repr
  // ensures fresh(queue.Repr - old(queue.Repr))
  // ensures queue.Inv()
  // ensures queue.I() == remove2(old(queue.I()), x, y)
  // {
  //   queue.Remove(x);
  //   queue.Remove(y);
  // }
}
