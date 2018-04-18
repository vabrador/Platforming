using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PIDAngleController : MonoBehaviour {

	public PlayerMovementInputController playerMovement;

	public CustomGravityController gravityController;
	public Vector3 gravity {
		get { return gravityController.gravity; }
	}
	public Vector3 gravityDir {
		get { return gravityController.gravity.normalized; }
	}

	public new Rigidbody rigidbody;
	
	public PID pid = new PID();

	public float Kp = 1f;
	public float Ki = 1f;
	public float Kd = 1f;

	public float targetDeltaAngle = 0f;

	void Update() {
		Vector3 inputDir = playerMovement.inputDirection;

		Vector3 currentDir = rigidbody.rotation * Vector3.forward;

    float targetAngleDelta = Vector3.Angle(currentDir, inputDir);
	}

}
