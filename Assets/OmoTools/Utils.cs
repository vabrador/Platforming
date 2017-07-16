using System.Collections.Generic;
using UnityEngine;

namespace OmoTools {
  
  public static partial class Utils {

    #region Float

    public static float Map(this float f, float minValue, float maxValue) {
      return Mathf.Clamp(f, minValue, maxValue);
    }

    public static float Map(this float f, float minValue, float maxValue, float minResult, float maxResult) {
      f = Mathf.Clamp(f, minValue, maxValue);
      return f.MapUnclamped(minValue, maxValue, minResult, maxResult);
    }

    public static float MapUnclamped(this float f, float minValue, float maxValue, float minResult, float maxResult) {
      return (f - minValue) / (maxValue - minValue) * (maxResult - minResult) + minResult;
    }

    #endregion

    #region Vector3

    /// <summary>
    /// Returns a vector that is perpendicular to this vector.
    /// The returned vector is not guaranteed to be a unit vector,
    /// nor is its length guaranteed to be the same as the source
    /// vector's.
    /// </summary>
    public static Vector3 GetPerpendicular(this Vector3 vector) {
      float x2 = vector.x * vector.x;
      float y2 = vector.y * vector.y;
      float z2 = vector.z * vector.z;

      float mag0 = z2 + x2;
      float mag1 = y2 + x2;
      float mag2 = z2 + y2;

      if (mag0 > mag1) {
        if (mag0 > mag2) {
          return new Vector3(-vector.z, 0, vector.x);
        } else {
          return new Vector3(0, vector.z, -vector.y);
        }
      } else {
        if (mag1 > mag2) {
          return new Vector3(vector.y, -vector.x, 0);
        } else {
          return new Vector3(0, vector.z, -vector.y);
        }
      }
    }

    public static Vector3 ClampMagnitude(this Vector3 v, float maxMagnitude) {
      float vMag = v.magnitude;
      if (vMag > maxMagnitude) {
        v = v * (maxMagnitude / vMag);
      }
      return v;
    }

    #endregion

    #region Vector3 (Euler Angles)

    public static Vector3 FlipAnglesAbove180(this Vector3 eulerAngles) {
      return new Vector3(eulerAngles.x > 180F ? -360F + eulerAngles.x : eulerAngles.x,
                        eulerAngles.y > 180F ? -360F + eulerAngles.y : eulerAngles.y,
                        eulerAngles.z > 180F ? -360F + eulerAngles.z : eulerAngles.z);
    }

    #endregion

    #region Color

    public static Color WithAlpha(this Color c, float a) {
      return new Color(c.r, c.g, c.b, a);
    }

    #endregion
    
    #region CapsuleCollider

    public static Vector3 GetLocalDirection(this CapsuleCollider capsule) {
      switch (capsule.direction) {
        case 0: return Vector3.right;
        case 1: return Vector3.up;
        case 2: default: return Vector3.forward;
      }
    }

    public static Vector3 GetWorldCenter(this CapsuleCollider capsule) {
      Vector3 basePos = capsule.attachedRigidbody == null ? capsule.transform.position
                                                          : capsule.attachedRigidbody.position;
      Quaternion baseRot = capsule.attachedRigidbody == null ? capsule.transform.rotation
                                                             : capsule.attachedRigidbody.rotation;
      return basePos + Vector3.Scale(baseRot * capsule.center,
                                     capsule.transform.lossyScale);
    }

    public static float GetWorldRadius(this CapsuleCollider capsule) {
      Vector3 scale = capsule.transform.lossyScale;
      return Mathf.Max(Mathf.Max(scale.x, scale.y), scale.z) * capsule.radius;
    }

    /// <summary>
    /// Returns the bottom (A, against capsule direction axis) and top
    /// (B, along capsule direction axis) of the line segments of the capsule
    /// in world space.
    /// </summary>
    public static void GetWorldSegmentPoints(this CapsuleCollider capsule,
                                             out Vector3 segmentA,
                                             out Vector3 segmentB) {
      Vector3 scale = capsule.transform.lossyScale;

      Vector3 worldCenter;
      Vector3 basePos = capsule.attachedRigidbody == null ? capsule.transform.position
                                                          : capsule.attachedRigidbody.position;
      Quaternion baseRot = capsule.attachedRigidbody == null ? capsule.transform.rotation
                                                             : capsule.attachedRigidbody.rotation;
      worldCenter = basePos + Vector3.Scale(baseRot * capsule.center, scale);

      Vector3 localDir = capsule.GetLocalDirection();
      Vector3 localCapsuleHeightVector = capsule.height * localDir;
      
      Vector3 worldDir = baseRot * localDir;
      Vector3 worldHeightVector = Vector3.Scale(baseRot * localCapsuleHeightVector, scale);
      float worldRadius = Mathf.Max(Mathf.Max(scale.x, scale.y), scale.z) * capsule.radius;

      Vector3 segmentOffset = (worldHeightVector / 2F) - (worldDir * worldRadius);

      segmentA = worldCenter - segmentOffset;
      segmentB = worldCenter + segmentOffset;
    }

    public static Vector3 GetSegmentA(this CapsuleCollider capsule) {
      Vector3 a, b;
      capsule.GetWorldSegmentPoints(out a, out b);

      return a;
    }

    public static Vector3 GetSegmentB(this CapsuleCollider capsule) {
      Vector3 a, b;
      capsule.GetWorldSegmentPoints(out a, out b);

      return b;
    }

    /// <summary>
    /// WARNING: This method is HIGHLY incomplete and will probably give you
    /// bad results.
    /// </summary>
    public static Ray CalculateDepenetration(this CapsuleCollider capsule,
                                              Collider collider) {
      Ray depenetration = new Ray();

      // TODO: THIS IS INVALID FOR GENERAL CAPSULES. (Sphere at A != Capsule)
      Vector3 a = capsule.GetSegmentA();

      // TODO: Collider needs to use proper calculations, not just transform pos and rot!!
      // TODO: Also, this method assumes the capsule isn't THAT penetrated.
      Vector3 closestPointOnCollider = Physics.ClosestPoint(a, collider,
                                                            collider.transform.position,
                                                            collider.transform.rotation);
      depenetration.origin = closestPointOnCollider;
      depenetration.direction = a - closestPointOnCollider;

      return depenetration;
    }

    #endregion

    #region Dictionaries

    public struct DictionaryKeyEnumerator<K, V> {
      private Dictionary<K, V> dict;
      private Dictionary<K, V>.Enumerator enumerator;

      public DictionaryKeyEnumerator(Dictionary<K, V> dict) {
        this.dict = dict;
        enumerator = dict.GetEnumerator();
      }

      public DictionaryKeyEnumerator<K, V> GetEnumerator() { return this; }

      public K Current { get { return enumerator.Current.Key; } }
      public bool MoveNext() {
        return enumerator.MoveNext();
      }

      public int Count { get { return dict.Count; } }
    }
    
    
    /// <summary>
    /// Normally, iterating over the keys in a Dictionary allocates garbage.
    /// Using this method instead, you can iterate over a Dictionary's values without allocation.
    /// </summary>
    public static DictionaryKeyEnumerator<K, V> GetKeysNonAlloc<K, V>(this Dictionary<K, V> dictionary) {
      return new DictionaryKeyEnumerator<K, V>(dictionary);
    }

    public struct DictionaryValueEnumerator<K, V> {
      private Dictionary<K, V> dict;
      private Dictionary<K, V>.Enumerator enumerator;

      public DictionaryValueEnumerator(Dictionary<K, V> dict) {
        this.dict = dict;
        enumerator = dict.GetEnumerator();
      }

      public DictionaryValueEnumerator<K, V> GetEnumerator() { return this; }

      public V Current { get { return enumerator.Current.Value; } }
      public bool MoveNext() {
        return enumerator.MoveNext();
      }

      public int Count { get { return dict.Count; } }
    }
    
    /// <summary>
    /// Normally, iterating over the values in a Dictionary allocates garbage.
    /// Using this method instead, you can iterate over a Dictionary's values without allocation.
    /// </summary>
    public static DictionaryValueEnumerator<K, V> GetValuesNonAlloc<K, V>(this Dictionary<K, V> dictionary) {
      return new DictionaryValueEnumerator<K, V>(dictionary);
    }

    #endregion

  }

}
