using System.Collections;
using System.Collections.Generic;
using OmoTools;
using UnityEngine;

public class PlayerController : MonoBehaviour {
	
  private Rigidbody _rigidbody;
	public
	#if UNITY_EDITOR
	new
	#endif
	Rigidbody rigidbody {
		get { return _rigidbody; }
	}

	private CapsuleCollider _capsule;
	public CapsuleCollider capsule {
		get { return _capsule; }
	}

	void OnValidate() {
    _rigidbody = GetComponent<Rigidbody>();
	}

	void Start() {
    _rigidbody = GetComponent<Rigidbody>();
	  rigidbody.maxAngularVelocity = 20;

		_capsule = GetComponentInChildren<CapsuleCollider>();
		
		PhysicsCallbacks.OnPostPhysics += onPostPhysics;
	}

	void Update() {
		updateIntention();
	}

	void FixedUpdate() {
		fixedUpdateState();
		fixedUpdateAction();
	}

	#region Intention

	[Header("Intention")]
	public Transform playerCameraTransform;

	private Vector3 _targetFacing;
	public Vector3 targetFacing { get { return _targetFacing; } }

  private bool _hasIntendedMovement = false;
	public bool hasIntendedMovement { get { return _hasIntendedMovement; } }

	private Vector3 _targetForwardRun;
	public Vector3 targetForwardRun { get {return _targetForwardRun; } }

	private bool _intendingJump = false;
	public bool intendingJump { get { return _intendingJump; } }

	private void updateIntention() {
		float forwardIntention = Input.GetAxis("Vertical");
		float sidewaysIntention = Input.GetAxis("Horizontal");

		_hasIntendedMovement = Mathf.Abs(forwardIntention) > 0.01F || Mathf.Abs(sidewaysIntention) > 0.01F;

    _targetFacing = forwardIntention  * Vector3.Cross(currentUpward, -playerCameraTransform.right  ).normalized
		              + sidewaysIntention * Vector3.Cross(currentUpward,  playerCameraTransform.forward).normalized;

		_targetForwardRun = Vector3.Dot(_targetFacing, currentFacing) * currentFacing;

		_intendingJump = Input.GetButtonDown("Jump");
	}

	void OnDrawGizmos() {
		// Target facing
		Gizmos.color = Color.blue;
		Gizmos.DrawLine(this.transform.position, this.transform.position + targetFacing);
		Gizmos.DrawCube(this.transform.position + targetFacing, Vector3.one * 0.2F);

		// Current facing
		Gizmos.DrawCube(this.transform.position + currentFacing * 0.7F, Vector3.one * 0.22F);

		// Target forward run
		Gizmos.color = Color.red;
		Gizmos.DrawCube(this.transform.position + targetForwardRun, Vector3.one * 0.25F);
	}

	#endregion

	#region State

	public Vector3 currentUpward { get { return rigidbody.rotation * Vector3.up; } }
	public Vector3 currentFacing { get { return rigidbody.rotation * Vector3.forward; } }

	private bool _isGrounded;
	public bool isGrounded { get { return _isGrounded; } }

	private RaycastHit[] raycastHits = new RaycastHit[32];

	private void fixedUpdateState() {
		// Update grounded state.
		_isGrounded = false;
		int numHits = Physics.SphereCastNonAlloc(rigidbody.position - rigidbody.rotation * Vector3.up * (capsule.height / 2F - capsule.radius),
																					   capsule.radius * 0.9F, Vector3.down, raycastHits, 0.05F, ~0);
    for (int i = 0; i < numHits; i++) {
			if (raycastHits[i].collider != capsule) {
				Debug.Log("GROUNDED");
				_isGrounded = true;
				break;
			}
		}
	}

	#endregion

	#region Action

	private const float TURN_POWER = 5F;
	private const float RUN_POWER  = 14F;
	private const float JUMP_POWER = 10F;

	void fixedUpdateAction() {
		// Moving
		Vector3 setLinearVelocity = rigidbody.velocity;

		Vector3 targetLinearVelocity;
		if (hasIntendedMovement) {
		  targetLinearVelocity = targetForwardRun / Time.fixedDeltaTime * 0.01F * RUN_POWER;
		}
		else {
			targetLinearVelocity = Vector3.zero;
		}
		Vector3 curVelToTargetVel = targetLinearVelocity - rigidbody.velocity;
		curVelToTargetVel = Vector3.Scale(curVelToTargetVel, new Vector3(1F, 0F, 1F));

		setLinearVelocity += curVelToTargetVel / Time.fixedDeltaTime * 0.0001F
												* RUN_POWER;

		// Jumping
		if (intendingJump && isGrounded) {
			if (setLinearVelocity.y < 0F) setLinearVelocity.y = 0F;

			setLinearVelocity += Vector3.up / Time.fixedDeltaTime * 0.01F
											   * JUMP_POWER;
		}

		// (Set linear velocity)
		rigidbody.velocity = setLinearVelocity;

		// Turning
		Quaternion uprightingRotation = Quaternion.FromToRotation(rigidbody.rotation * Vector3.up, Vector3.up);
		rigidbody.rotation = uprightingRotation * rigidbody.rotation;

		Vector3 targetAngularVelocity = Vector3.zero;

		if (hasIntendedMovement) {
	    Quaternion targetRot = Quaternion.LookRotation(targetFacing, Vector3.up);
			Quaternion curRotToTargetRot = targetRot * Quaternion.Inverse(rigidbody.rotation);

			targetAngularVelocity = curRotToTargetRot.eulerAngles.FlipAnglesAbove180() / Time.fixedDeltaTime * 0.001F * TURN_POWER;
		}

		// (Set angular velocity)
		rigidbody.angularVelocity = Vector3.Lerp(rigidbody.angularVelocity, targetAngularVelocity, 20F * Time.fixedDeltaTime);
	}

	private void onPostPhysics() {

	}

	#endregion

}
