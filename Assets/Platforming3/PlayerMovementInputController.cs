using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerMovementInputController : MonoBehaviour, IDirectionProvider {

	public Transform playerCameraTransform;

	public CustomGravityController playerGravityController;

	public Vector3 inputDirection;

	void Update() {
		float rightLeft   = Input.GetAxis("Horizontal");
		float forwardBack = Input.GetAxis("Vertical");

		Vector3 upDir = -playerGravityController.gravity.normalized;
		Vector3 flatCameraForward = Vector3.ProjectOnPlane(playerCameraTransform.forward,
																											 upDir).normalized;
    Vector3 flatCameraRight = Vector3.Cross(upDir, flatCameraForward).normalized;

		inputDirection = flatCameraForward * forwardBack 
									 + flatCameraRight   * rightLeft;
	}

  public Vector3 GetDirection() {
    return inputDirection;
  }

}
