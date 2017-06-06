using UnityEngine;

namespace OmoTools {

  public struct OrbitData {
    public float   angle;
    public Vector3 position;
    public Vector3 center;
    public Vector3 axisDir;
    public Vector3 radialDir;
    public Vector3 tangentDir;
  }

  public struct Orbit {
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

    public Orbit(Vector3 origin,
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

    public Orbit GetEnumerator() { return this; }
    public OrbitData Current {
      get {
        return new OrbitData() {
                                angle = angle,
                                position = position,
                                center = origin,
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

}