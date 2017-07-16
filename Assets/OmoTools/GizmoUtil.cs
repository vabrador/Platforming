using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace OmoTools {

  public static class GizmoUtil {

    public static void DrawWireCone(Vector3 origin, Vector3 direction, float radius) {
      foreach (var pose in new Orbit(origin, direction, radius)) {
        Gizmos.DrawLine(pose.position, origin + direction);
      }
    }

  }

}