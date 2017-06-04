using UnityEngine;

public static class Utils {

  #region Vector3

  public static Vector3 FlipAnglesAbove180(this Vector3 eulerAngles) {
    return new Vector3(eulerAngles.x > 180F ? -360F + eulerAngles.x : eulerAngles.x,
                       eulerAngles.y > 180F ? -360F + eulerAngles.y : eulerAngles.y,
                       eulerAngles.z > 180F ? -360F + eulerAngles.z : eulerAngles.z);
  }

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

}