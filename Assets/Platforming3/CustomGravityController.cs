using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CustomGravityController : MonoBehaviour {

	public Vector3 gravity;

	// Debug: Test changes to gravity
	public float xAngle = 0f;
	
	public new Rigidbody rigidbody;

	void Reset() {
		if (rigidbody == null) rigidbody = GetComponent<Rigidbody>();
	}

	private bool _useGravityPreGravController = false;

	void OnEnable() {
		_useGravityPreGravController = rigidbody.useGravity;
		gravity = Physics.gravity;

		rigidbody.useGravity = false;
	}

	void OnDisable() {
		rigidbody.useGravity = _useGravityPreGravController;
	}

	void FixedUpdate() {
	  gravity = Quaternion.AngleAxis(xAngle, Vector3.forward) * Physics.gravity;

		if (rigidbody.useGravity) {
			Debug.LogWarning("CustomGravityController is enabled on this rigidbody; let it apply gravity "
										 + "instead of PhysX. Disabling useGravity on the Rigidbody...", this);
			rigidbody.useGravity = false;
		}

		rigidbody.ApplyGravity(gravity);
	}

}
