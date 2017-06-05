using System.Collections.Generic;
using UnityEngine;

namespace OmoTools {
  
  public static class Utils {

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

    public static Vector3 FlipAnglesAbove180(this Vector3 eulerAngles) {
      return new Vector3(eulerAngles.x > 180F ? -360F + eulerAngles.x : eulerAngles.x,
                        eulerAngles.y > 180F ? -360F + eulerAngles.y : eulerAngles.y,
                        eulerAngles.z > 180F ? -360F + eulerAngles.z : eulerAngles.z);
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

    public static void CalculateDepenetration(this CapsuleCollider capsule,
                                              Collider other,
                                              out Vector3 point,
                                              out Vector3 normal) {
      point = Vector3.zero;
      normal = Vector3.up;

      // TODO: Implement me!
    }

    #endregion

    #region Dictionaries

    public struct DictionaryKeyEnumerator<K, V> {
      private Dictionary<K, V>.Enumerator enumerator;

      public DictionaryKeyEnumerator(Dictionary<K, V> dict) {
        enumerator = dict.GetEnumerator();
      }

      public DictionaryKeyEnumerator<K, V> GetEnumerator() { return this; }

      public K Current { get { return enumerator.Current.Key; } }
      public bool MoveNext() {
        return enumerator.MoveNext();
      }
    }
    
    public static DictionaryKeyEnumerator<K, V> GetKeysNonAlloc<K, V>(this Dictionary<K, V> dictionary) {
      return new DictionaryKeyEnumerator<K, V>(dictionary);
    }

    public struct DictionaryValueEnumerator<K, V> {
      private Dictionary<K, V>.Enumerator enumerator;

      public DictionaryValueEnumerator(Dictionary<K, V> dict) {
        enumerator = dict.GetEnumerator();
      }

      public DictionaryValueEnumerator<K, V> GetEnumerator() { return this; }

      public V Current { get { return enumerator.Current.Value; } }
      public bool MoveNext() {
        return enumerator.MoveNext();
      }
    }
    
    public static DictionaryValueEnumerator<K, V> GetValuesNonAlloc<K, V>(this Dictionary<K, V> dictionary) {
      return new DictionaryValueEnumerator<K, V>(dictionary);
    }

    #region Geometry

    public struct OrbitData {
      public float   angle;
      public Vector3 position;
      public Vector3 axisDir;
      public Vector3 radialDir;
      public Vector3 tangentDir;
    }

    public struct OrbitEnumerator {
      private Vector3 origin;
      private Vector3 axis;
      private float radius;
      private int resolution;
      private float minAngle;
      private float maxAngle;

      private int idx;

      private Vector3 position;
      private float   angle;
      private Vector3 radialDirection;
      private Vector3 tangentDirection;

      private Vector3 radial;
      private float deltaAngle { get { return 360F / resolution; } }

      public OrbitEnumerator(Vector3 origin,
                             Vector3 axis,
                             float radius,
                             int resolution = 32,
                             float minAngle = 0F,
                             float maxAngle = 360F) {
        this.origin = origin;
        this.axis = axis.normalized;
        this.radius = radius;
        this.resolution = resolution;
        this.minAngle = minAngle;
        this.maxAngle = maxAngle;

        this.idx = -1;
        this.radial = Vector3.Cross(axis, axis.GetPerpendicular()).normalized;
        this.angle = 0F;

        this.position = Vector3.zero;
        this.radialDirection = Vector3.zero;
        this.tangentDirection = Vector3.zero;

        updateData();
      }

      public OrbitEnumerator GetEnumerator() { return this; }
      public OrbitData Current {
        get {
          return new OrbitData() {
                                   angle = angle,
                                   position = position,
                                   axisDir = axis,
                                   radialDir = radialDirection,
                                   tangentDir = tangentDirection
                                 };
        }
      }
      public bool MoveNext() {
        int numLoops = 0;
        do {
          numLoops++;
          this.idx += 1;

          updateData();
        } while (this.angle < minAngle && numLoops < 256);

        if (this.angle > maxAngle) return false;
        else return true;
      }

      private void updateData() {
        this.angle = deltaAngle * this.idx;
        this.radial = Quaternion.AngleAxis(this.angle, axis) * this.radial;

        this.position = origin + radial * radius;
        this.radialDirection = radial.normalized;
        this.tangentDirection = Vector3.Cross(axis, radialDirection).normalized;
      }
    }

    public static OrbitEnumerator Orbit(this Vector3 origin, Vector3 axis, float radius,
                                        int resolution = 32, float minAngle = 0F,
                                        float maxAngle = 360F) {
      return new OrbitEnumerator(origin, axis, radius, resolution, minAngle, maxAngle);
    } 

    #endregion

    #endregion

  }

}
