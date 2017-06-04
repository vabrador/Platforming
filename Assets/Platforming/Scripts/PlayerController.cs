using OmoTools;
using UnityEngine;

public class PlayerController : MonoBehaviour {

  public CapsuleCollider groundCapsule;
	public CapsuleCollider wallCapsule;
	
	public
	#if UNITY_EDITOR
	new
	#endif
	Rigidbody rigidbody;

	void OnValidate() {
    if (rigidbody == null) rigidbody = GetComponent<Rigidbody>();
	}

	void Start() {
    if (rigidbody == null) rigidbody = GetComponent<Rigidbody>();
	  rigidbody.maxAngularVelocity = 20;
		
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

	private bool _jumpJustPressed = false;
	private float _lastJumpTimer = 0F;
	private float _minLastJumpTime = 0.35F;
	public bool intendingJump { get { return _lastJumpTimer > _minLastJumpTime && _jumpJustPressed; } }

	private void updateIntention() {
		float forwardIntention = Input.GetAxis("Vertical");
		float sidewaysIntention = Input.GetAxis("Horizontal");

		_hasIntendedMovement = Mathf.Abs(forwardIntention) > 0.01F || Mathf.Abs(sidewaysIntention) > 0.01F;

    _targetFacing = forwardIntention  * Vector3.Cross(currentUpward, -playerCameraTransform.right  ).normalized
		              + sidewaysIntention * Vector3.Cross(currentUpward,  playerCameraTransform.forward).normalized;

		_targetForwardRun = Vector3.Dot(_targetFacing, currentFacing).Map(0F, 1F, 0.8F, 1F) * _targetFacing;

		_jumpJustPressed |= Input.GetButtonDown("Jump");
		if (_lastJumpTimer < _minLastJumpTime) {
			_lastJumpTimer += Time.deltaTime;
		}
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
	private float _timeSinceLastGrounded;
	private float _maxTimeSinceLastGroundedForJump = 0.080F;
	public bool isGrounded { get { return _isGrounded; } }

	private RaycastHit[] raycastHits = new RaycastHit[32];

	private void fixedUpdateState() {
		// Update grounded state.
		_isGrounded = false;
		int numHits = Physics.SphereCastNonAlloc(rigidbody.position - rigidbody.rotation * Vector3.up * (groundCapsule.height / 2F - groundCapsule.radius),
																					   groundCapsule.radius * 0.95F, Vector3.down, raycastHits, 0.25F, ~0);
    for (int i = 0; i < numHits; i++) {
			if (!raycastHits[i].collider.tag.Equals("Player")) {
				_isGrounded = true;
				break;
			}
		}

		// Grounded state delay
		if (isGrounded) {
			_timeSinceLastGrounded = 0F;
		}
		else {
			if (_timeSinceLastGrounded < _maxTimeSinceLastGroundedForJump) {
			  _timeSinceLastGrounded += Time.fixedDeltaTime;
			}
		}
	}

	#endregion

	#region Action

	private const float RUN_SPEED  = 12F;
	private const float RUN_POWER  = 30F;
	private const float STOP_POWER = 40F;

	private const float JUMP_POWER = 10F;

	private const float TURN_POWER = 5F;

	void fixedUpdateAction() {
		// Moving
		Vector3 setLinearVelocity = rigidbody.velocity;

		Vector3 targetLinearVelocity;
		if (hasIntendedMovement) {
		  targetLinearVelocity = targetForwardRun * Time.fixedDeltaTime * 30F
													 * RUN_SPEED;
		}
		else {
			targetLinearVelocity = Vector3.zero;
		}

		Vector3 curVelToTargetVel = targetLinearVelocity - rigidbody.velocity;
		curVelToTargetVel = Vector3.Scale(curVelToTargetVel, new Vector3(1F, 0F, 1F));

		setLinearVelocity += curVelToTargetVel * Time.fixedDeltaTime * 0.2F
											* (hasIntendedMovement ? RUN_POWER : STOP_POWER);

		// Jumping
		if (intendingJump && (isGrounded || _timeSinceLastGrounded < _maxTimeSinceLastGroundedForJump)) {
			_lastJumpTimer = 0F;

			if (setLinearVelocity.y < 0F) setLinearVelocity.y = 0F;

			setLinearVelocity += Vector3.up * 0.5F
							           * JUMP_POWER;
		}

		// (Consume the jump press.)
    if (_jumpJustPressed) { _jumpJustPressed = false; }

		// (Set linear velocity)
		rigidbody.velocity = setLinearVelocity;

		// Turning
		Quaternion uprightingRotation = Quaternion.FromToRotation(rigidbody.rotation * Vector3.up, Vector3.up);
		rigidbody.rotation = uprightingRotation * rigidbody.rotation;

		Vector3 targetAngularVelocity = Vector3.zero;

		if (hasIntendedMovement) {
	    Quaternion targetRot = Quaternion.LookRotation(targetFacing, Vector3.up);
			Quaternion curRotToTargetRot = targetRot * Quaternion.Inverse(rigidbody.rotation);

			targetAngularVelocity = curRotToTargetRot.eulerAngles.FlipAnglesAbove180() * Time.fixedDeltaTime
														* TURN_POWER;
		}

		// (Set angular velocity)
		rigidbody.angularVelocity = Vector3.Lerp(rigidbody.angularVelocity, targetAngularVelocity, 20F * Time.fixedDeltaTime);
	}

	private void onPostPhysics() {

	}

	#endregion

}
