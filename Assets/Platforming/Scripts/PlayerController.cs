using System;
using System.Collections.Generic;
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

	private float _timeSinceLastGrounded;
	private float _maxTimeSinceLastGroundedForJump = 0.080F;
	public bool isGrounded { get { return _groundingObjectsMap.Count > 0; } }

	public class GroundingObject {
		public Transform  transform;
		public Collider   collider;
		public Vector3    pointOfContact;
		public Vector3    contactNormal;
		public Vector3    positionLastFrame;
		public Quaternion rotationLastFrame;

		public bool       canMove   { get { return rigidbody != null; } }
		public Rigidbody  rigidbody { get { return collider.attachedRigidbody; } }
		public Vector3    position  { get { return rigidbody != null ? rigidbody.position 
																																 : transform.position; } }
    public Quaternion rotation  { get { return rigidbody != null ? rigidbody.rotation
																																 : transform.rotation; } }

		public GroundingObject(Transform transform,
													 Collider collider) {
			this.transform = transform;
			this.collider = collider;
			this.pointOfContact = Vector3.zero;
			this.contactNormal = Vector3.zero;
			positionLastFrame = Vector3.zero;
			rotationLastFrame = Quaternion.identity;

			positionLastFrame = position;
			rotationLastFrame = rotation;
		}

		public void GetPointMovementFromMovementLastFrame(Vector3 point,
																		         			    out Vector3 pointMovement) {
			Vector3    curPos = this.position;
			Quaternion curRot = this.rotation;

			Vector3    lastPos = this.positionLastFrame;
			Quaternion lastRot = this.rotationLastFrame;

			pointMovement = Vector3.zero;

			// Due to translation
			pointMovement += curPos - lastPos;

			// Due to rotation
			Quaternion   deltaRot    = curRot * Quaternion.Inverse(lastRot);
			Vector3 relativePoint    = point - curPos;
			Vector3 relPointAfterRot = deltaRot * relativePoint;
			Vector3 movementDueToRot = relPointAfterRot - relativePoint;
			pointMovement += movementDueToRot;
		}

		public void UpdateLastFramePositionRotation() {
			this.positionLastFrame = this.position;
			this.rotationLastFrame = this.rotation;
		}

	}

	private RaycastHit[] _groundRaycastHits = new RaycastHit[32];
	private Dictionary<Collider, GroundingObject> _groundingObjectsMap = new Dictionary<Collider, GroundingObject>();

	private void fixedUpdateState() {
		fixedUpdateGroundedState();

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

	private void fixedUpdateGroundedState() {
		HashSet<Collider> groundingCollidersBuffer = null;
		try {
			groundingCollidersBuffer = Pool<HashSet<Collider>>.Spawn();

			int numHits = Physics.SphereCastNonAlloc(rigidbody.position - rigidbody.rotation * Vector3.up * (groundCapsule.height / 2F - groundCapsule.radius),
																							groundCapsule.radius * 0.95F, Vector3.down, _groundRaycastHits, 0.25F, ~0);
			for (int i = 0; i < numHits; i++) {
				RaycastHit hit = _groundRaycastHits[i];

				if (!hit.collider.tag.Equals("Player")) {
					groundingCollidersBuffer.Add(hit.collider);

					if (!_groundingObjectsMap.ContainsKey(hit.collider)) {
						GroundingObject newObj = new GroundingObject(hit.transform, hit.collider);
						newObj.pointOfContact = hit.point;
						newObj.contactNormal  = hit.normal;
						_groundingObjectsMap[hit.collider] = newObj;
					}
					else {
						GroundingObject updatingObj = _groundingObjectsMap[hit.collider];
						updatingObj.pointOfContact = hit.point;
						updatingObj.contactNormal  = hit.normal;
						_groundingObjectsMap[hit.collider] = updatingObj;
					}
				}
			}

			List<GroundingObject> groundingObjRemoveBuffer = null;
			try {
				groundingObjRemoveBuffer = Pool<List<GroundingObject>>.Spawn();

				foreach (var colliderGroundObjPair in _groundingObjectsMap) {
					var groundingObject = colliderGroundObjPair.Value;

					if (!groundingCollidersBuffer.Contains(groundingObject.collider)) {
						groundingObjRemoveBuffer.Add(groundingObject);
					}
				}

				foreach (var toRemove in groundingObjRemoveBuffer) {
					_groundingObjectsMap.Remove(toRemove.collider);
				}
			}
			finally {
				if (groundingObjRemoveBuffer != null) {
					groundingObjRemoveBuffer.Clear();
					Pool<List<GroundingObject>>.Recycle(groundingObjRemoveBuffer);
				}
			}
		}
		finally {
			if (groundingCollidersBuffer != null) {
				groundingCollidersBuffer.Clear();
				Pool<HashSet<Collider>>.Recycle(groundingCollidersBuffer);
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

	public Action<Vector3> OnReferenceFrameTranslated = (v) => { };

	void fixedUpdateAction() {
		// Moving due to moving objects beneath us.
		Vector3    totalDeltaPosition = Vector3.zero;
		foreach (var collObjPair in _groundingObjectsMap) {
			var groundingObject = collObjPair.Value;

			Vector3 deltaPosition;
			groundingObject.GetPointMovementFromMovementLastFrame(groundCapsule.GetSegmentA(),
																					 									out deltaPosition);
      totalDeltaPosition += deltaPosition;

			groundingObject.UpdateLastFramePositionRotation();
		}

		rigidbody.position += totalDeltaPosition;
		OnReferenceFrameTranslated(totalDeltaPosition);

		// Moving physics
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

		// (Set linear velocity.)
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

		// (Set angular velocity.)
		rigidbody.angularVelocity = Vector3.Lerp(rigidbody.angularVelocity, targetAngularVelocity, 20F * Time.fixedDeltaTime);
	}

	private void onPostPhysics() {

	}

	#endregion

}
