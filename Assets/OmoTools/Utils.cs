using UnityEngine;

public static class Utils {

  public static Vector3 FlipAnglesAbove180(this Vector3 eulerAngles) {
    return new Vector3(eulerAngles.x > 180F ? -360F + eulerAngles.x : eulerAngles.x,
                       eulerAngles.y > 180F ? -360F + eulerAngles.y : eulerAngles.y,
                       eulerAngles.z > 180F ? -360F + eulerAngles.z : eulerAngles.z);
  }

}