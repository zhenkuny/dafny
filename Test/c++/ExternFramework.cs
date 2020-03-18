
namespace Extern {
    public class __default {
      public static T[] @newArrayFill<T>(ulong n, T t)
      {
        T[] res = new T[n];
        for (ulong i = 0; i < n; i++) {
          res[i] = t;
        }
        return res;
      }
    }

    public class state {
    }

    public class ExternClass {
      public bool my_method0(ulong a) { return true; }
      public bool my_method1(ulong c) { return false; }
    }

}
