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

    #endregion

  }

}
