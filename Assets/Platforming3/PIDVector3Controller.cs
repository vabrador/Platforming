using System.Collections;
using System.Collections.Generic;
using OmoTools;
using UnityEngine;

public class PIDVector3Controller : MonoBehaviour {

	public new Rigidbody rigidbody;

	public PlayerMovementInputController playerMovement;

	public CustomGravityController gravityController;
	public Vector3 gravity {
		get { return gravityController.gravity; }
	}
	public Vector3 gravityDir {
		get { return gravityController.gravity.normalized; }
	}

	public float playerHeight = 1f;
	
	public Vector3 target;

	public float Kp = 70f;
	public float Ki = 10f;
	public float Kd = 20f;
	public VectorPID pid = new VectorPID(Vector3.zero);

	public float inputPosSpeed = 1f;

	public float maxNonAltitudePidOutput = 70f;
	public float maxAltitudePidOutput = 70f;

	public bool enableDebugDrawing = true;

	void Reset() {
		if (gravityController == null) gravityController = GetComponent<CustomGravityController>();
	}

	void OnValidate() {
		if (gravityController == null) gravityController = GetComponent<CustomGravityController>();
	}

	private bool _isTouchingGround = false;

	void Update() {
		Vector3 inputMoveDir = playerMovement.GetDirection();
		
		Quaternion fromToGravity = Quaternion.FromToRotation(Physics.gravity, gravity);
		inputMoveDir = fromToGravity * inputMoveDir;

		float effSpeed = inputPosSpeed;
		if (Input.GetKey(KeyCode.LeftShift)) {
			effSpeed *= 0.33f;
		}
		target += inputMoveDir * effSpeed * Time.deltaTime;

		// Clamp target distance from actual distance.
		Vector3 delta = target - rigidbody.position;
		Vector3 gravXZDelta = Vector3.ProjectOnPlane(delta, -gravityDir);
		gravXZDelta = gravXZDelta.ClampMagnitude(0.3f);
		target = Vector3.ProjectOnPlane(rigidbody.position, -gravityDir)
						 + gravXZDelta
						 + Vector3.Project(target, -gravityDir);

		Vector3 closestHitPos = Vector3.zero;
		_isTouchingGround = false;
		RaycastHit? maybeHit = PhysicsUtil.RaycastClosest(this.transform.position,
																											gravityController.gravity,
																											playerHeight,
																											~(1 << this.gameObject.layer));
    if (maybeHit.HasValue) {
			var hit = maybeHit.Value;
			closestHitPos = hit.point;
			_isTouchingGround = true;
		}

		if (_isTouchingGround) {
		  Vector3 groundHeightPos = Vector3.Project(closestHitPos, -gravityDir);
			target = Vector3.ProjectOnPlane(target, -gravityDir)
							 + (groundHeightPos + (-gravityDir * playerHeight * 0.8f));
		}
		else {
			//target = 
		}
	}

	void FixedUpdate() {
		pid.target = this.target;
		pid.SetConstants(Kp, Ki, Kd);
		pid.Update(this.rigidbody.position, Time.fixedDeltaTime);

		// Clamp PID output.
		Vector3 nonAltitudeOutput = Vector3.ProjectOnPlane(pid.output, -gravityDir);
		Vector3 altitudeOutput    = Vector3.Project(pid.output, -gravityDir);
		nonAltitudeOutput = Vector3.ClampMagnitude(nonAltitudeOutput, maxNonAltitudePidOutput);
		altitudeOutput    = Vector3.ClampMagnitude(altitudeOutput, maxAltitudePidOutput);
		pid.output = nonAltitudeOutput + altitudeOutput;

		// Only allow the PID to apply force against gravity if we're on the ground.
		if (!_isTouchingGround) {
			pid.output = Vector3.ProjectOnPlane(pid.output, -gravityDir);
		}

		this.rigidbody.AddForce(pid.output, ForceMode.Force);
	}

	void OnDrawGizmos() {
		if (!enableDebugDrawing) return;

		Gizmos.color = Color.red;

		float initRad = 1f;
		for (int i = 0; i < 4; i++) {
			Gizmos.DrawWireSphere(this.target, initRad * (1 - 0.2f * i));
		}

		Gizmos.color = Color.Lerp(Color.blue, Color.magenta, 0.3f);
		foreach (var orbitPoint in new Orbit(this.transform.position, gravityDir, 0.2f)) {
			Gizmos.DrawLine(orbitPoint.position, this.transform.position + gravityDir * playerHeight);
		}
	}

}
