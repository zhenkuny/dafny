// Does not test anything Exceptions-related, but is included by other tests
// RUN: %dafny "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

datatype NatOutcome =
| NatSuccess(value: nat)
| NatFailure(error: string)
{
    predicate method IsFailure() {
        this.NatFailure?
    }
    function method PropagateFailure(): NatOutcome requires IsFailure() {
        this
    }
    function method Extract(): nat requires !IsFailure() {
        this.value
    }
}
