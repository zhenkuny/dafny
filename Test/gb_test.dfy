module gb_test {
    predicate IsModEquivalent(x: int, y: int, m: int)
        requires m > 0
        // ensures x % m == y % m <==> (x - y) % m == 0
    {
        (x - y) % m == 0 // same as x % n == y % n, but easier to do induction on x - y than x and y separately
    }

    lemma {:extern} {:groebner} test(x: int, y: int, z: int, m: int)
        requires m > 0;
        requires IsModEquivalent(x, y, m);
        ensures IsModEquivalent(x * z, y * z, m);

    lemma {:extern} {:groebner} test2(p2_full: int, BASE: int,
        ui: int, m0: int, m0d: int, p1_lh: int,
        p1_full: int)
        requires BASE > 0;
        requires p2_full == ui * m0 + p1_lh;
        requires IsModEquivalent(p1_full, p1_lh, BASE);
        requires IsModEquivalent(m0d * m0, -1, BASE);
        requires IsModEquivalent(ui, p1_full * m0d, BASE);
        ensures IsModEquivalent(p2_full, 0, BASE);

    lemma {:extern} {:groebner} mul256_canonize_lemma(
        B: nat,
        p0: nat, p1: nat, p2: nat, p3: nat,
        p4: nat, p5: nat, p6: nat, p7: nat,
        p8: nat, p9: nat, p10: nat, p11: nat,
        p12: nat, p13: nat, p14: nat, p15: nat,
        x: nat, x_0: nat, x_1: nat, x_2: nat, x_3: nat,
        y: nat, y_0: nat, y_1: nat,y_2: nat, y_3: nat)
    
        requires x == x_0 + x_1 * B + x_2 * B * B + x_3 * B * B * B;
        requires y == y_0 + y_1 * B + y_2 * B * B + y_3 * B * B * B; 
        requires p0 == x_0 * y_0;
        requires p1 == x_1 * y_0;
        requires p2 == x_0 * y_1;
        requires p3 == x_2 * y_0;
        requires p4 == x_1 * y_1;
        requires p5 == x_0 * y_2;
        requires p6 == x_3 * y_0;
        requires p7 == x_2 * y_1;
        requires p8 == x_1 * y_2;
        requires p9 == x_0 * y_3;
        requires p10 == x_3 * y_1;
        requires p11 == x_2 * y_2;
        requires p12 == x_1 * y_3;
        requires p13 == x_3 * y_2;
        requires p14 == x_2 * y_3;
        requires p15 == x_3 * y_3;

        requires x == x_0 + x_1 * B + x_2 * B * B + x_3 * B * B * B
        requires y == y_0 + y_1 * B + y_2 * B * B + y_3 * B * B * B
        ensures
            x * y
            ==
            p0 + (p1 + p2) * B + (p3 + p4 + p5) * B * B + (p6 + p7 + p8 + p9) * B * B * B + (p10 + p11 + p12) * B * B * B * B + (p13 + p14) * B * B * B * B * B + p15 * B * B * B * B * B * B;
}
