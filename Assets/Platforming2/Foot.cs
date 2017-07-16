using OmoTools;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Platforming2 {

  public class Foot : MonoBehaviour {

    public Body body;
    public float footRadius = 0.2F;
    public float maxForce = 10.0F;

    [Header("Auto")]
    public bool isColliding;
    public float initLength = 1F;
    public float maxLength = 1F;

    private void Reset() {
      body = FindObjectOfType<Body>();
    }

    private void OnValidate() {
      initializeLength();
    }

    private void Awake() {
      initializeLength();
    }
    
    private void initializeLength() {
      initLength = Vector3.Distance(body.rigidbody.position, this.transform.position);
      maxLength = initLength * 1.05F;
    }

    private void FixedUpdate() {
      updatePosition();

      isColliding = checkColliding();

      updateVelocity();
    }

    private bool checkColliding() {
      var colliders = PhysicsUtil.Query(this.transform.position, footRadius);
      return colliders.Count > 0;
    }

    private void updatePosition() {
      Ray ray = new Ray(body.rigidbody.position, Vector3.down);

      bool useNewPosition = false;
      Vector3 newPosition = Vector3.zero;
      foreach (var hit in PhysicsUtil.Raycast(ray, maxLength)) {
        useNewPosition = true;
        newPosition = hit.point;
        break;
      }

      if (useNewPosition) {
        this.transform.position = newPosition;
      }
      else {
        this.transform.position = body.rigidbody.position + Vector3.down * maxLength;
      }
    }

    private bool _hasPositionLastFrame = false;
    private Vector3 _positionLastFrame;
    private Vector3 _velocity;
    public Vector3 velocity { get { return _velocity; } }

    private void updateVelocity() {
      if (_hasPositionLastFrame) {
        _velocity = (transform.position - _positionLastFrame) / Time.deltaTime;
      }

      _hasPositionLastFrame = true;
      _positionLastFrame = this.transform.position;
    }

    private Vector3 _totalForce = Vector3.zero;
    private Vector3 _lastTotalForce = Vector3.zero;
    public Vector3 clampedTotalForce {
      get { return _totalForce.ClampMagnitude(maxForce); }
    }

    public void BeginForces() {
      _totalForce = Vector3.zero;
    }

    private Vector3 projectBodyVelocity(Vector3 appliedForce) {
      return body.rigidbody.velocity
           + (appliedForce / body.rigidbody.mass
              + Physics.gravity) * Time.fixedDeltaTime;
    }

    private Vector3 projectBodyVelocity() {
      return projectBodyVelocity(clampedTotalForce);
    }

    private Vector3 projectBodyPosition(float numFrames = 1F) {
      return body.rigidbody.position + projectBodyVelocity() * Time.fixedDeltaTime * numFrames;
    }

    public void AddVelocity(Vector3 velocity) {
      _totalForce += velocity * body.rigidbody.mass / Time.fixedDeltaTime;
    }

    public void AddForce(Vector3 force) {
      _totalForce += force;
    }

    public void ApplyForces(bool preserveForceBuildup = false) {
      //Debug.Log("Attempted force magnitude: " + _totalForce.magnitude);

      var actualForce = clampedTotalForce;

      body.rigidbody.AddForce(actualForce);

      _lastTotalForce = actualForce;
      if (!preserveForceBuildup) {
        _totalForce = Vector3.zero;
      }
    }

    public void ZeroForces() {
      Vector3 projectedVelocity = projectBodyVelocity();
      AddVelocity(-projectedVelocity);
    }

    public void PushToTarget(Vector3 targetPosition) {
      Vector3 projectedPosition = projectBodyPosition(20F);
      AddVelocity((targetPosition - projectedPosition) / Time.fixedDeltaTime);
    }

    private bool _hasLastTargetPosition = false;
    private Vector3 _lastTargetPosition;
    private Vector3 _targetPosition;
    private Vector3 _predictedTargetPosition;

    public void UpdateTargetPrediction(Vector3 targetPosition) {
      _targetPosition = targetPosition;
      if (_hasLastTargetPosition) {
        Vector3 velocityFromLast = (_targetPosition - _lastTargetPosition) / Time.fixedDeltaTime;
        _predictedTargetPosition = _targetPosition + velocityFromLast * Time.fixedDeltaTime * 20F;
      }
      else {
        _predictedTargetPosition = _targetPosition;
      }

      _hasLastTargetPosition = true;
      _lastTargetPosition = _targetPosition;
    }

    public void PushToPredictedTarget() {
      PushToTarget(_predictedTargetPosition);
    }
    
    private const int GRAV_ITERATIONS = 8;
    public void PrioritizeGravity() {
      Vector3 force = clampedTotalForce;
      _origClampedForce = force;

      float fixAngle = Vector3.Angle(force, -Physics.gravity);
      Vector3 fixAxis = Vector3.Cross(force, -Physics.gravity).normalized;
      _lastFixAxis = fixAxis;
      _gravityForce = -Physics.gravity;

      _origClampedForceProjected = Vector3.Dot(force, -Physics.gravity.normalized) * -Physics.gravity.normalized;

      float gravMag = Physics.gravity.magnitude;

      if (Vector3.Dot(force, -Physics.gravity.normalized) > gravMag * 2F) {
        for (int i = 0; i < _gravityCorrectionIterations.Length; i++) {
          _gravityCorrectionIterations[i] = Vector3.zero;
        }
        _correctedThisFrame = false;
        //Debug.Log("no need to correct, force on gravity normal is " + Vector3.Dot(force, -Physics.gravity.normalized));
        //Debug.Log("and neg grav mag is " + -Physics.gravity.magnitude);
        return;
      }
      else {
        _correctedThisFrame = true;
        Vector3 testForce = force;
        for (int i = 0; i < GRAV_ITERATIONS; i++) {
          testForce = Quaternion.AngleAxis(fixAngle, fixAxis) * testForce;
          _gravityCorrectionIterations[i] = testForce;

          float projectedHeight = Vector3.Dot(testForce, -Physics.gravity.normalized);
          if (Vector3.Dot(testForce, -Physics.gravity.normalized) > gravMag * 2F) {
            // result too large, go the other direction.
            fixAngle = fixAngle / 2F;
            if (fixAngle > 0F) {
              fixAngle = -fixAngle;
            }
          }
          else {
            // result too small, continue increasing angle.
            fixAngle = fixAngle / 2F;
            if (fixAngle < 0F) {
              fixAngle = -fixAngle;
            }
          }
        }

        _totalForce = testForce;
      }
    }

    private Vector3 _gravityForce;
    private bool _correctedThisFrame = false;
    private Vector3[] _gravityCorrectionIterations = new Vector3[GRAV_ITERATIONS];
    private Vector3 _lastFixAxis = Vector3.zero;
    private Vector3 _origClampedForce = Vector3.zero;
    private Vector3 _origClampedForceProjected = Vector3.zero;

    private void OnDrawGizmosSelected() {
      Gizmos.color = Color.Lerp(Color.green, Color.blue, 0.7F);
      Gizmos.DrawWireSphere(this.transform.position, footRadius);

      //Gizmos.color = Color.blue;
      //GizmoUtil.DrawWireCone(this.transform.position, Vector3.up * initLength * 0.25F, footRadius * 2F);
      //Gizmos.color = Color.green;
      //GizmoUtil.DrawWireCone(this.transform.position, _lastTotalForce * 0.02F, footRadius);

      Gizmos.color = Color.green;
      if (_correctedThisFrame) Gizmos.color = Color.Lerp(Color.green, Color.black, 0.5F);
      GizmoUtil.DrawWireCone(this.transform.position, _origClampedForce * 0.05F, footRadius);

      Gizmos.color = Color.cyan;
      GizmoUtil.DrawWireCone(this.transform.position, _origClampedForceProjected * 0.05F, footRadius);

      Gizmos.color = Color.red;
      GizmoUtil.DrawWireCone(this.transform.position, _gravityForce * 0.05F, footRadius);

      Gizmos.color = Color.black;
      GizmoUtil.DrawWireCone(this.transform.position, _lastFixAxis, footRadius * 0.5F);

      Gizmos.color = Color.Lerp(Color.red, Color.yellow, 0.7F);
      int numCones = _gravityCorrectionIterations.Length;
      for (int i = 0; i < numCones; i++) {
        float fade = (numCones - i) / (float)numCones;
        Gizmos.color = Gizmos.color.WithAlpha(fade * fade * fade);
        GizmoUtil.DrawWireCone(this.transform.position, _gravityCorrectionIterations[i] * 0.05F, footRadius * 0.2F);
      }

      // Predicted target position
      Gizmos.color = Color.Lerp(Color.red, Color.blue, 0.3F);
      int numSpheres = 7;
      float radius = 0.1F;
      float radiusStep = 0.1F;
      for (int i = 0; i < numSpheres; i++) {
        Gizmos.color = Gizmos.color.WithAlpha((numSpheres - i) / (float)numSpheres);
        Gizmos.DrawWireSphere(_predictedTargetPosition, radius);
        radius += radiusStep;
      }

      // sheer prevention (projected velocity)
      //Gizmos.color = Color.red;
      //GizmoUtil.DrawWireCone(this.transform.position, _sheerProjectedVelocity, footRadius);
      //Gizmos.color = Color.Lerp(Color.red, Color.yellow, 0.6F);
      //GizmoUtil.DrawWireCone(this.transform.position, _sheerPreventionVelocity, footRadius);
    }

    //public void CounterGravity(float fraction) {
    //  AddVelocity(-Physics.gravity * Time.fixedDeltaTime * fraction);
    //}

    //public void ZeroRelativeVelocity() {
    //  Vector3 projectedBodyVelocity = 0.8F * projectBodyVelocity(clampedTotalForce);

    //  Vector3 bodyVelocityRelativeToFoot = this.velocity - projectedBodyVelocity;
    //  AddVelocity(bodyVelocityRelativeToFoot * 1F);
    //}

    //public void PushForTarget(Vector3 targetBodyPosition) {
    //  Vector3 projectedBodyVelocity = 1F * projectBodyVelocity(clampedTotalForce);

    //  Vector3 projectedBodyPosition = body.rigidbody.position + projectedBodyVelocity * Time.fixedDeltaTime;

    //  Vector3 toTargetPosition = targetBodyPosition - projectedBodyPosition;

    //  Vector3 reqVelocity = toTargetPosition / Time.fixedDeltaTime;

    //  AddVelocity(reqVelocity * 1F);
    //}

    //public void EnforceLimits() {
    //  Vector3 footToBody = body.rigidbody.position - this.transform.position;

    //  if (footToBody.magnitude > maxLength) {
    //    Vector3 limitPosition = footToBody.ClampMagnitude(maxLength);

    //    Vector3 projectedRigidbodyVelocity = projectBodyVelocity(clampedTotalForce);
    //    Vector3 projectedRigidbodyPosition = body.rigidbody.position + projectedRigidbodyVelocity * Time.fixedDeltaTime;

    //    AddVelocity()
    //  }
    //}

    //public void PushForVelocity(Vector3 targetBodyVelocity) {
    //  Vector3 projectedBodyVelocity = 1F * projectBodyVelocity(clampedTotalForce);

    //  Vector3 toTargetVelocity = targetBodyVelocity - projectedBodyVelocity;

    //  AddVelocity(toTargetVelocity * 1F);
    //}

    //private Vector3 _sheerProjectedVelocity = Vector3.zero;
    //private Vector3 _sheerPreventionVelocity = Vector3.zero;

    //public void PreventSheer(Vector3 targetBodyVelocity) {
    //  Vector3 projectedBodyVelocity = projectBodyVelocity(clampedTotalForce);
    //  projectedBodyVelocity = Vector3.ProjectOnPlane(projectedBodyVelocity, Vector3.up);
    //  _sheerProjectedVelocity = projectedBodyVelocity;

    //  Vector3 preventionVelocity = -projectedBodyVelocity * (1F - Mathf.Abs(Vector3.Dot(targetBodyVelocity.normalized, projectedBodyVelocity.normalized)));

    //  preventionVelocity = preventionVelocity * 1F;

    //  _sheerPreventionVelocity = preventionVelocity;

    //  AddVelocity(preventionVelocity);
    //}

  }


}
