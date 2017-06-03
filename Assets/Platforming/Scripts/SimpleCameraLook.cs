using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SimpleCameraLook : MonoBehaviour {
	
	public Transform target;

	void Update() {
		this.transform.rotation = Quaternion.LookRotation(target.transform.position - this.transform.position, Vector3.up);
	}

}
