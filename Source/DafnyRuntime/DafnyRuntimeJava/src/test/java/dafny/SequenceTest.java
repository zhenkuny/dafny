package dafny;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertThrows;
import static org.junit.jupiter.api.Assertions.assertTrue;

import java.math.BigInteger;
import java.util.ArrayList;
import java.util.Iterator;
import java.util.List;
import org.junit.jupiter.api.Test;

class SequenceTest {

  Integer[] testSequenceArr = new Integer[]{1, 3, 2, 4, 2, 4, 6, 5, 4, 1, 7};
  Integer[] testSequencePreArr = new Integer[]{1, 3, 2, 4, 2, 4};
  Integer[] testSequenceNPreArr = new Integer[]{1, 3, 2, 4, 2, 5};
  Integer[] testSequenceNPre2Arr = new Integer[]{1, 3, 2, 4, 2, 4, 6, 5, 4, 1, 7, 3};
  Integer[] testSequenceSubArr = new Integer[]{2, 4, 6, 5};
  Integer[] testSequenceTakeArr = new Integer[]{1, 3, 2, 4, 2};
  Integer[] testSequenceDropArr = new Integer[]{4, 6, 5, 4, 1, 7};
  Integer[] testSequenceEmptyArr = new Integer[]{};
  DafnySequence<Integer> testSequence = DafnySequence.fromArray(testSequenceArr);
  DafnySequence<Integer> testSequencePre = DafnySequence.fromArray(testSequencePreArr);
  DafnySequence<Integer> testSequenceNPre = DafnySequence.fromArray(testSequenceNPreArr);
  DafnySequence<Integer> testSequenceNPre2 = DafnySequence.fromArray(testSequenceNPre2Arr);
  DafnySequence<Integer> testSequenceSub = DafnySequence.fromArray(testSequenceSubArr);
  DafnySequence<Integer> testSequenceDrop = DafnySequence.fromArray(testSequenceDropArr);
  DafnySequence<Integer> testSequenceTake = DafnySequence.fromArray(testSequenceTakeArr);
  DafnySequence<Integer> testSequenceEmpty = DafnySequence.fromArray(testSequenceEmptyArr);
  DafnySequence<Integer> testCopy = DafnySequence.fromArray(testSequenceArr);

  @Test
  void testSequencePrefix() {
    assertTrue(testSequencePre.isPrefixOf(testSequence));
    assertFalse(testSequenceNPre.isPrefixOf(testSequence));
    assertFalse(testSequenceNPre2.isPrefixOf(testSequence));
    assertTrue(testSequence.isPrefixOf(testSequence));
    assertFalse(testSequence.isPrefixOf(testSequencePre));
  }

  @Test
  void testSequenceProperPrefix() {
    assertTrue(testSequencePre.isProperPrefixOf(testSequence));
    assertFalse(testSequence.isProperPrefixOf(testSequenceNPre));
    assertFalse(testSequence.isProperPrefixOf(testSequence));
    assertFalse(testSequenceNPre.isProperPrefixOf(testSequence));
    assertFalse(testSequenceNPre2.isProperPrefixOf(testSequence));
  }

  @Test
  void testSequenceConcatenate() {
    DafnySequence<Integer> fatty = testSequence.concatenate(testSequencePre);
    assertEquals(fatty.length(), testSequencePre.length() + testSequence.length());
    for (int i = 0; i < testSequence.length(); i++) {
      assertEquals(fatty.select(i), testSequence.select(i));
    }
    for (int i = 0; i < testSequencePre.length(); i++) {
      assertEquals(fatty.select(i + testSequence.length()), testSequencePre.select(i));
    }
  }

  @Test
  void testSequenceLength() {
    assertEquals(11, testSequence.length());
    assertEquals(6, testSequencePre.length());
    assertEquals(6, testSequenceNPre.length());
    assertEquals(12, testSequenceNPre2.length());
  }

  @Test
  void testSequenceUpdate() {
    DafnySequence<Integer> temp;
    temp = testSequence.update(5, 5);
    DafnySequence<Integer> testUpdate = DafnySequence
        .fromArray(new Integer[]{1, 3, 2, 4, 2, 5, 6, 5, 4, 1, 7});
    assertEquals(temp, testUpdate);
    assertEquals(testSequence, testCopy);
  }

  @Test
  void testSequenceMembership() {
    assertTrue(testSequence.contains(1));
    assertTrue(testSequence.contains(2));
    assertTrue(testSequence.contains(3));
    assertTrue(testSequence.contains(4));
    assertTrue(testSequence.contains(5));
    assertTrue(testSequence.contains(6));
    assertTrue(testSequence.contains(7));
    assertFalse(testSequence.contains(8));
    assertFalse(testSequence.contains(9));
    assertFalse(testSequence.contains(10));
  }

  @Test
  void testSequenceSubsequenceStuff() {
    assertEquals(testSequenceSub, testSequence.subsequence(4, 8));
    assertEquals(testSequenceDrop, testSequence.drop(5));
    assertEquals(testSequenceTake, testSequence.take(5));
  }

  @Test
  void testSequenceMultisetConversion() {
    DafnyMultiset<Integer> m = new DafnyMultiset<>();
    m = m.update(1, BigInteger.valueOf(2));
    m = m.update(2, BigInteger.valueOf(2));
    m = m.update(3, BigInteger.valueOf(1));
    m = m.update(4, BigInteger.valueOf(3));
    m = m.update(5, BigInteger.valueOf(1));
    m = m.update(6, BigInteger.valueOf(1));
    m = m.update(7, BigInteger.valueOf(1));
    DafnyMultiset<Integer> c = testSequence.asDafnyMultiset();
    assertEquals(m, c);

  }

  @Test
  void testSequenceSlice() {
    List<Integer> l = new ArrayList<>();
    l.add(5);
    l.add(0);
    l.add(6);
    DafnySequence<DafnySequence<Integer>> sliced = testSequence.slice(l);
    Iterator<DafnySequence<Integer>> it = sliced.iterator();
    assertEquals(it.next(), testSequenceTake);
    assertEquals(it.next(), testSequenceEmpty);
    assertEquals(it.next(), testSequenceDrop);
  }

  @Test
  void testObjectMethods() {
    assertEquals(testSequence, testCopy);
    assertEquals(testSequence.hashCode(), testCopy.hashCode());
    assertEquals("[1, 3, 2, 4, 2, 4, 6, 5, 4, 1, 7]", testSequence.toString());
    assertEquals("[1, 3, 2, 4, 2, 4, 6, 5, 4, 1, 7]", testCopy.toString());
  }

  @Test
  @SuppressWarnings("all")
  void testNullFailures() {
    List<Integer> l = null;
    assertThrows(AssertionError.class, () -> DafnySequence.fromList(l));
    assertThrows(AssertionError.class, () -> testSequence.isPrefixOf(null));
    assertThrows(AssertionError.class, () -> testSequence.contains(null));
    assertThrows(AssertionError.class, () -> testSequence.concatenate(null));
    assertThrows(AssertionError.class, () -> testSequence.update(1, null));
    assertThrows(AssertionError.class, () -> testSequence.slice(null));
    assertThrows(AssertionError.class, () -> {
      List<Integer> l1 = new ArrayList<>();
      l1.add(null);
      testSequence.slice(l1);
    });
    assertThrows(NullPointerException.class, () -> testSequence.forEach(null));
  }

  @Test
  void testIndexFailures() {
    assertThrows(AssertionError.class, () -> {
      testSequence.drop(13);
      testSequence.drop(-3);
      testSequence.take(13);
      testSequence.take(-3);
      testSequence.subsequence(-3, 4);
      testSequence.subsequence(3, 42);
      testSequence.subsequence(2, 1);
      testSequence.subsequence(testSequence.length(), testSequence.length());
      testSequence.update(45, 3);
      testSequence.update(-8, 3);
    });
  }

  @Test
  void testNullMembers() {
    Integer[] testNulls = new Integer[]{3, null, 2};
    DafnySequence<Integer> testNull = DafnySequence.fromArray(testNulls);
    assertThrows(AssertionError.class, () -> testNull.update(0, null));
    assertEquals(testNull, DafnySequence.fromArray(new Integer[]{3, null, 2}));
  }
}
