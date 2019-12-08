
method R0(x:bool)
{
  if x {
    R1(false);
  } 
}

method R1(y:bool)
{
  if y {
    R0(false);
  }
}

method Main() {
  R0(true);
}
