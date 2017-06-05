using System.Collections.Generic;

namespace OmoTools {
  
  public static class Pool<T> where T : new() {

    private static Stack<T> _stack = new Stack<T>();

    public static T Spawn() {
      if (_stack.Count == 0) {
        _stack.Push(new T());
      }

      return _stack.Pop();
    }

    public static void Recycle(T obj) {
      _stack.Push(obj);
    }

  }

}