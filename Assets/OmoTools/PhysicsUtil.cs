using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace OmoTools {

  public static class PhysicsUtil {

    #region Collider

    private static List<Collider> s_colliderResultsBuffer = new List<Collider>();
    private static Collider[] s_collidersBuffer = new Collider[16];

    /// <summary>
    /// Returns a List containing the results for Physics.SphereQueryNonAlloc using
    /// a buffer List object. (Don't store references to this List object directly).
    /// </summary>
    public static List<Collider> Query(Vector3 position, float radius) {
      int numResults;
      do {
        numResults = Physics.OverlapSphereNonAlloc(position, radius, s_collidersBuffer);

        if (numResults == s_collidersBuffer.Length) {
          s_collidersBuffer = new Collider[s_collidersBuffer.Length * 2];
          numResults = -1;
        }
      } while (numResults == -1);

      s_colliderResultsBuffer.Clear();
      for (int i = 0; i < numResults; i++) {
        s_colliderResultsBuffer.Add(s_collidersBuffer[i]);
      }

      return s_colliderResultsBuffer;
    }

    private static List<RaycastHit> s_hitResultsBuffer = new List<RaycastHit>();
    private static RaycastHit[] s_hitBuffer = new RaycastHit[16];

    public static List<RaycastHit> Raycast(Ray ray, float maxLength) {
      int numResults;
      do {
        numResults = Physics.RaycastNonAlloc(ray, s_hitBuffer, maxLength, ~(1 << 8));

        if (numResults == s_hitBuffer.Length) {
          s_hitBuffer = new RaycastHit[s_hitBuffer.Length * 2];
          numResults = -1;
        }
      } while (numResults == -1);

      s_hitResultsBuffer.Clear();
      for (int i = 0; i < numResults; i++) {
        s_hitResultsBuffer.Add(s_hitBuffer[i]);
      }

      return s_hitResultsBuffer;
    }

    #endregion

  }

}