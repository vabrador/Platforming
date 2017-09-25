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

    public static List<RaycastHit> Raycast(Ray ray, float maxLength, int layerMask = ~0) {
      int numResults;
      do {
        numResults = Physics.RaycastNonAlloc(ray, s_hitBuffer, maxLength, layerMask);

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

    public static List<RaycastHit> Raycast(Vector3 pos, Vector3 dir, float maxLength, int layerMask = ~0) {
      return Raycast(new Ray(pos, dir), maxLength, layerMask);
    }

    public static RaycastHit? RaycastClosest(Ray ray, float maxLength, int layerMask = ~0) {
      Vector3 pos = ray.origin;
      RaycastHit closestHit = default(RaycastHit);
      float closestDist = float.PositiveInfinity;
      bool hitAnything = false;
      foreach (var hit in Raycast(ray, maxLength, layerMask)) {
        hitAnything = true;
        float testDist = (hit.point - pos).sqrMagnitude;
        if (testDist < closestDist) {
          closestHit = hit;
          closestDist = testDist;
        }
      }
      if (!hitAnything) return null;
      else return closestHit;
    }

    public static RaycastHit? RaycastClosest(Vector3 pos, Vector3 dir, float maxLength, int layerMask = ~0) {
      return RaycastClosest(new Ray(pos, dir), maxLength, layerMask);
    }

    #endregion

  }

}