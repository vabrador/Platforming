using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class GravityUtil {

	public static Vector3 defaultGravity = Physics.gravity;

	public static void ApplyGravity(this Rigidbody body, Vector3 gravity) {
		body.AddForce(gravity);
	}

}
