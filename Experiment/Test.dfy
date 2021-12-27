include "InputGen.dfy"

module test {
  import opened InputGen

  function method addc(x: uint, y: uint, cin: uint1): (uint, uint1)
  {
    var sum : int := x + y + cin;
    var sum_out := if sum < BASE() then sum else sum - BASE();
    var cout := if sum < BASE() then 0 else 1;
    (sum_out, cout)
  }

  method dw_add(x_lh: uint, x_uh: uint, y_lh: uint, y_uh: uint)
    returns (z_lh: uint, z_uh: uint)
  {
    var r1 := addc(x_lh, y_lh, 0);
    var r2 := addc(x_uh, y_uh, r1.1);
    dw_add_correct(
      x_lh, y_lh, r1.0, r1.1,
      x_uh, y_uh, r2.0, r2.1);
    z_lh := r1.0;
    z_uh := r2.0;
  }

  lemma {:axiom} dw_add_correct(
    x_lh: uint, y_lh: uint, z_lh: uint, c1: uint1,
    x_uh: uint, y_uh: uint, z_uh: uint, c2: uint1)
    requires (z_lh, c1) == addc(x_lh, y_lh, 0);
    requires (z_uh, c2) == addc(x_uh, y_uh, c1);

  method Main() {
    var i0 := Gen.GetRandomUint();
    var i1 := Gen.GetRandomUint();
    var i2 := Gen.GetRandomUint();
    var i3 := Gen.GetRandomUint();
    var a, b := dw_add(i0, i1, i2, i3);
  }
}
