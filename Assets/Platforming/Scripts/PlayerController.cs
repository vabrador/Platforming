using System;
using System.Collections.Generic;
using OmoTools;
using UnityEngine;

namespace Platforming {

	public class PlayerController : MonoBehaviour {

		public CapsuleCollider wallCapsule;

		[Header("Body")]
		[SerializeField]
		private Foot[] _feet;
		private Foot[] feet { get { return _feet; } }
		
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

		private bool _intendingMovement = false;
		public bool intendingMovement { get { return _intendingMovement; } }

		private Vector3 _targetForwardRun;
		public Vector3 targetRunVector { get {return _targetForwardRun; } }

		private bool _jumpJustPressed = false;
		private float _lastJumpTimer = 0F;
		private float _minLastJumpTime = 0.35F;
		public bool intendingJump { get { return _lastJumpTimer > _minLastJumpTime && _jumpJustPressed; } }

		private void updateIntention() {
			float forwardIntention = Input.GetAxis("Vertical");
			float sidewaysIntention = Input.GetAxis("Horizontal");

			_intendingMovement = Mathf.Abs(forwardIntention) > 0.01F || Mathf.Abs(sidewaysIntention) > 0.01F;

			_targetFacing = forwardIntention  * Vector3.Cross(currentUpward, -playerCameraTransform.right  ).normalized
										+ sidewaysIntention * Vector3.Cross(currentUpward,  playerCameraTransform.forward).normalized;

			_targetForwardRun = Vector3.Dot(_targetFacing, currentFacing).Map(0F, 1F, 0.8F, 1F) * _targetFacing;

			_jumpJustPressed |= Input.GetButtonDown("Jump");
			if (_lastJumpTimer < _minLastJumpTime) {
				_lastJumpTimer += Time.deltaTime;
			}
		}

		#endregion

		#region State

		public Vector3 currentUpward { get { return rigidbody.rotation * Vector3.up; } }
		public Vector3 currentFacing { get { return rigidbody.rotation * Vector3.forward; } }

		private void fixedUpdateState() {
			
			// Standing
			foreach (var foot in feet) {
				foot.RefreshStandPoint();
			}

		}

		#endregion

		#region Action

		public Action<Vector3> OnReferenceFrameTranslated = (v) => { };

		private void fixedUpdateAction() {

			Vector3 targetPos = this.rigidbody.position;

			// If we have two feet on the ground, counter gravity.
			List<Foot> feetOnGround = Pool<List<Foot>>.Spawn();
			foreach (var foot in feet) {
				if (foot.hasContact) feetOnGround.Add(foot);;
			}
			if (feetOnGround.Count >= 2) {
				Vector3 footPosAverage = Vector3.zero;
				foreach (var foot in feet) {
					footPosAverage += foot.transform.position;
				}
				footPosAverage /= feetOnGround.Count;

				targetPos = footPosAverage + Vector3.up * 0.7F;
			}

			Vector3 targetVelocity = (targetPos - this.rigidbody.position) / Time.fixedDeltaTime;
			Vector3 curVelToTargetVel = targetVelocity - this.rigidbody.velocity;
			
			this.rigidbody.AddForce(curVelToTargetVel * 0.1F, ForceMode.VelocityChange);
		}

		#endregion

		// #region State



		// private float _timeSinceLastGrounded;
		// private float _maxTimeSinceLastGroundedForJump = 0.080F;
		// public bool isGrounded { get { return _groundingObjectsMap.Count > 0; } }

		// public class GroundingObject {
			
		// 	public Transform  transform;
		// 	public Collider   collider;
		// 	public Ray				contact;
		// 	public Ray  		  contactLastFrame;
		// 	public Vector3    positionLastFrame;
		// 	public Quaternion rotationLastFrame;

		// 	public bool       canMove   { get { return rigidbody != null; } }
		// 	public Rigidbody  rigidbody { get { return collider.attachedRigidbody; } }
		// 	public Vector3    position  { get { return rigidbody != null ? rigidbody.position 
		// 																															 : transform.position; } }
		//   public Quaternion rotation  { get { return rigidbody != null ? rigidbody.rotation
		// 																															 : transform.rotation; } }

		//   public Vector3 contactPoint { get { return contact.origin; } }
		// 	public Vector3 contactNormal { get { return contact.direction; } }
		// 	public Vector3 contactPointLastFrame { get { return contactLastFrame.origin; } }
		// 	public Vector3 contactNormalLastFrame { get { return contactLastFrame.direction; } }

		//   public void Initialize(Transform transform,
		// 												 Collider collider,
		// 												 Ray contact) {
		// 		this.transform = transform;
		// 		this.collider = collider;
		// 		this.contact = contact;
		// 		this.contactLastFrame = contact;
		// 		positionLastFrame = Vector3.zero;
		// 		rotationLastFrame = Quaternion.identity;

		// 		positionLastFrame = position;
		// 		rotationLastFrame = rotation;
		// 	}

		// 	public GroundingObject() { }

		// 	public void Clear() {
		// 		this.transform = null;
		// 		this.collider = null;
		// 		this.contact = new Ray();
		// 	}

		// 	public Vector3 GetPointMovementFromMovementLastFrame(Vector3 point) {
		// 		Vector3    curPos = this.position;
		// 		Quaternion curRot = this.rotation;

		// 		Vector3    lastPos = this.positionLastFrame;
		// 		Quaternion lastRot = this.rotationLastFrame;

		// 		Vector3 pointMovement = Vector3.zero;

		// 		// Due to translation
		// 		pointMovement += curPos - lastPos;

		// 		// Due to rotation
		// 		Quaternion   deltaRot    = curRot * Quaternion.Inverse(lastRot);
		// 		Vector3 relativePoint    = point - curPos;
		// 		Vector3 relPointAfterRot = deltaRot * relativePoint;
		// 		Vector3 movementDueToRot = relPointAfterRot - relativePoint;
		// 		pointMovement += movementDueToRot;

		// 		return pointMovement;
		// 	}

		// 	public void UpdateLastFramePositionRotation() {
		// 		this.positionLastFrame = this.position;
		// 		this.rotationLastFrame = this.rotation;
		// 	}

		// 	public Vector3 GetContactDisplacementSinceLastFrame() {
		// 		return contact.origin - contactLastFrame.origin;
		// 	}

		// }

		// private RaycastHit[] _groundRaycastHits = new RaycastHit[32];
		// private Dictionary<Collider, GroundingObject> _groundingObjectsMap = new Dictionary<Collider, GroundingObject>();
		// private Utils.DictionaryValueEnumerator<Collider, GroundingObject> groundingObjects {
		// 	get {
		// 		return _groundingObjectsMap.GetValuesNonAlloc();
		// 	}
		// }

		// private void fixedUpdateState() {
		// 	fixedUpdateGroundedState();

		// 	// Grounded state delay
		// 	if (isGrounded) {
		// 		_timeSinceLastGrounded = 0F;
		// 	}
		// 	else {
		// 		if (_timeSinceLastGrounded < _maxTimeSinceLastGroundedForJump) {
		// 		  _timeSinceLastGrounded += Time.fixedDeltaTime;
		// 		}
		// 	}
		// }

		// private void fixedUpdateGroundedState() {
		// 	HashSet<Collider> groundingCollidersBuffer = null;
		// 	try {
		// 		groundingCollidersBuffer = Pool<HashSet<Collider>>.Spawn();

		// 		float radius = groundCapsule.GetWorldRadius();
		// 		int numHits = Physics.SphereCastNonAlloc(groundCapsule.GetSegmentA(),
		// 																						radius * 0.80F, Vector3.down,
		// 																						_groundRaycastHits,
		// 																						radius * 0.50F, ~0);
		// 		for (int i = 0; i < numHits; i++) {
		// 			RaycastHit hit = _groundRaycastHits[i];

		// 			if (!hit.collider.tag.Equals("Player")) {
		// 				groundingCollidersBuffer.Add(hit.collider);
						
		// 				if (hit.point == Vector3.zero) {
		// 					// Collider was already overlapping the capsule. Calculate a
		// 					// depenetration ray.
		// 					Ray depenetrationRay;
		// 					depenetrationRay = groundCapsule.CalculateDepenetration(hit.collider);
		//           hit.point = depenetrationRay.origin;
		// 					hit.normal = depenetrationRay.direction.normalized;
		// 				}

		// 				GroundingObject groundingObj;
		// 				if (!_groundingObjectsMap.ContainsKey(hit.collider)) {
		// 					groundingObj = Pool<GroundingObject>.Spawn();
		// 					groundingObj.Initialize(hit.transform,
		// 																	hit.collider,
		// 																	new Ray(hit.point, hit.normal));
		// 				}
		// 				else {
		// 					groundingObj = _groundingObjectsMap[hit.collider];
		// 				}
		// 				groundingObj.contactLastFrame = groundingObj.contact;
		// 				groundingObj.contact = new Ray(hit.point, hit.normal);
		// 				_groundingObjectsMap[hit.collider] = groundingObj;
		// 			}
		// 		}

		// 		List<GroundingObject> groundingObjRemoveBuffer = null;
		// 		try {
		// 			groundingObjRemoveBuffer = Pool<List<GroundingObject>>.Spawn();

		// 			foreach (var groundingObj in _groundingObjectsMap.GetValuesNonAlloc()) {
		// 				if (!groundingCollidersBuffer.Contains(groundingObj.collider)) {
		// 					groundingObjRemoveBuffer.Add(groundingObj);
		// 				}
		// 			}

		// 			foreach (var toRemove in groundingObjRemoveBuffer) {
		// 				_groundingObjectsMap.Remove(toRemove.collider);

		// 				toRemove.Clear();
		// 				Pool<GroundingObject>.Recycle(toRemove);
		// 			}
		// 		}
		// 		finally {
		// 			if (groundingObjRemoveBuffer != null) {
		// 				groundingObjRemoveBuffer.Clear();
		// 				Pool<List<GroundingObject>>.Recycle(groundingObjRemoveBuffer);
		// 			}
		// 		}
		// 	}
		// 	finally {
		// 		if (groundingCollidersBuffer != null) {
		// 			groundingCollidersBuffer.Clear();
		// 			Pool<HashSet<Collider>>.Recycle(groundingCollidersBuffer);
		// 		}
		// 	}
		// }

		// #endregion

		// #region Action

		// private const float RUN_SPEED  = 12F;
		// private const float RUN_POWER  = 30F;
		// private const float STOP_POWER = 40F;

		// private const float JUMP_POWER = 10F;

		// private const float TURN_POWER = 5F;

		// private Dictionary<GroundingObject, Ray> _objContactPointsLastFrame = new Dictionary<GroundingObject, Ray>();

		// void fixedUpdateAction() {
		//   // Linear Velocity //

		// 	// By default, the goal is to resist the displacement of the contact point
		// 	// on each grounding object by following the movement of each contact point.
		// 	foreach (var groundingObj in groundingObjects) {
		// 		Vector3 contactDisplacement = groundingObj.GetContactDisplacementSinceLastFrame();

		// 		Vector3 contactFollowVector = new Vector3(-contactDisplacement.x,
		// 																							contactDisplacement.y,
		// 																							-contactDisplacement.z);

		//     Vector3 curVelocityAtContact = rigidbody.GetPointVelocity(groundingObj.contactPointLastFrame);
		// 		Vector3 targetVelocityAtContact = contactFollowVector / Time.fixedDeltaTime;

		//     // this.rigidbody.AddForceAtPosition(targetVelocityAtContact - curVelocityAtContact,
		// 		// 																	groundingObj.contactPointLastFrame,
		// 		// 											            ForceMode.VelocityChange);

		//     this.rigidbody.AddForce(targetVelocityAtContact - curVelocityAtContact,
		// 													  ForceMode.);
		// 	}

			
			
		// 	// If we're intending movement, note its target (additional) velocity.
		// 	Vector3 targetIntendedMovementVector = Vector3.zero;
		// 	if (hasIntendedMovement) {
		// 	  targetIntendedMovementVector += targetRunVector * Time.fixedDeltaTime * 30F * RUN_SPEED;
		// 	}

		// 	// Splitting forces amongst the available contact points equally,
		// 	// project target forces

		// 	// Vector3 curVelToTargetVel = targetLinearVelocity - rigidbody.velocity;
		// 	// curVelToTargetVel = Vector3.Scale(curVelToTargetVel, new Vector3(1F, 0F, 1F));
		// 	// Vector3 selfVelocityDelta = curVelToTargetVel * Time.fixedDeltaTime * 0.2F
		// 	// 									        * (hasIntendedMovement ? RUN_POWER : STOP_POWER);

		// 	// // Reckoning forces affecting the player.

		// 	// // Custom movement calculations based on objects the player is standing on.
		// 	// Vector3 totalDeltaPositionDueToGroundMovement = Vector3.zero;
		// 	// Vector3 deltaPosDueToGroundMovement;
		// 	// Vector3 capsuleSegmentA = groundCapsule.GetSegmentA();
		// 	// foreach (var groundingObj in _groundingObjectsMap.GetValuesNonAlloc()) {
		// 	//   groundingObj.GetPointMovementFromMovementLastFrame(capsuleSegmentA,
		// 	//   																		 							 out deltaPosDueToGroundMovement);
		//   //   if (groundingObj.rigidbody != null
		// 	//       && !groundingObj.rigidbody.isKinematic) {
		// 	//   	deltaPosDueToGroundMovement *= groundingObj.rigidbody.mass.Map(0F, this.rigidbody.mass * 2F, 0F, 1F);
		// 	//   }
		//   //   totalDeltaPositionDueToGroundMovement += deltaPosDueToGroundMovement;
		// 	//   groundingObj.UpdateLastFramePositionRotation();
		
		// 	//   Vector3 groundMovementDeltaV = deltaPosDueToGroundMovement / Time.fixedDeltaTime;
		// 	// }

		// 	// Dictionary<GroundingObject, Ray> gravCounterSlides = Pool<Dictionary<GroundingObject, Ray>>.Spawn();
		// 	// foreach (var groundingObj in _groundingObjectsMap.GetValuesNonAlloc()) {
		// 	// 	// The player has a frictionless base, so apply a force to counter
		// 	// 	// sliding due to gravity on skewed surfaces.
		// 	// 	Vector3 gravityDeltaV = Physics.gravity * Time.fixedDeltaTime;
		// 	// 	Vector3 normal = groundingObj.contactNormal;
		// 	// 	Vector3 slopeDir = Vector3.Cross(groundingObj.contactNormal,
		// 	// 																	 Vector3.Cross(gravityDeltaV, groundingObj.contactNormal)).normalized;
		// 	// 	Vector3 slideDueToGravity = Vector3.Dot(gravityDeltaV, slopeDir) * slopeDir;
		// 	// 	gravCounterSlides[groundingObj] = new Ray() { origin = groundingObj.contactPoint,
		// 	// 													          						direction = slideDueToGravity };
		// 	// }
		// 	// foreach (var counterSlide in gravCounterSlides.GetValuesNonAlloc()) {
		// 	// 	this.rigidbody.AddForceAtPosition(counterSlide.origin,
		// 	// 																		counterSlide.direction,
		// 	// 																		ForceMode.VelocityChange);
		// 	// }

		// 	// // Apply sheer forces against nonkinematic rigidbodies currently grounding us
		// 	// // to conserve momentum.
		// 	// foreach (var groundingObj in groundingObjects) {
		// 	// 	// Self movement
		// 	// 	Ray selfMovementSheer = new Ray();
		// 	// 	selfMovementSheer.origin = groundingObj.contactNormal;
		// 	// 	selfMovementSheer.direction = selfVelocityDelta / groundingObjects.Count;

		// 	// 	// Resisting slides due to gravity
		// 	// 	Ray gravCounterSlide = gravCounterSlides[groundingObj];

		// 	// 	// 
		// 	// }


																					
		// 	// 	// Just before setting linear velocity, report it to our grounding objects,
		// 	// 	// applying sheer force in the opposite direction (to account for conservation of momentum)
		// 	// 	// transverse to the contact normal -- this only applies when the player
		// 	// 	// is grounded on a nonkinematic rigidbody.
		// 	// 	// TODO: This should not be force that is copied to each grounding object.
		// 	// 	if (groundingObj.rigidbody != null && !groundingObj.rigidbody.isKinematic) {
		// 	// 	  Vector3 contactNormal = groundingObj.contactNormal;
		// 	// 	  Vector3 playerVelocityDelta = curVelToTargetVel
		// 	// 															  + counterSlideDeltaV
		// 	// 														    + deltaPosDueToGroundMovement / Time.fixedDeltaTime;
		// 	// 	  Vector3 transverseDir = Vector3.Cross(contactNormal, Vector3.Cross(playerVelocityDelta,
		// 	// 	  																																	 contactNormal)).normalized;
		// 	// 	  Vector3 transverseDelta = Vector3.Dot(transverseDir, playerVelocityDelta) * transverseDir;

		// 	// 	  Vector3 playerMomentumDelta = transverseDelta * this.rigidbody.mass;

		// 	// 		Vector3 velocityTransfer = playerMomentumDelta / groundingObj.rigidbody.mass;

		// 	// 		// Cap maximum possible velocity transfer.
		// 	// 		float transferMag = velocityTransfer.magnitude;
		// 	// 		float maxVelocityTransfer = 1F;
		// 	// 		if (transferMag > maxVelocityTransfer) {
		// 	// 			float transferCappingMultiplier = (maxVelocityTransfer / transferMag);
		// 	// 			velocityTransfer *= transferCappingMultiplier;
		// 	// 		}

		// 	// 		groundingObj.rigidbody.AddForceAtPosition(-velocityTransfer,
		// 	// 																							groundingObj.contactPoint,
		// 	// 																							ForceMode.VelocityChange);

		// 	// }

		// 	// this.rigidbody.position += totalDeltaPositionDueToGroundMovement;
		// 	// OnReferenceFrameTranslated(totalDeltaPositionDueToGroundMovement);

		// 	// gravCounterSlides.Clear();
		// 	// Pool<Dictionary<GroundingObject, Ray>>.Recycle(gravCounterSlides);

		// 	// setLinearVelocity += selfVelocityDelta;

		// 	// Jumping.
		// 	if (intendingJump && (isGrounded || _timeSinceLastGrounded < _maxTimeSinceLastGroundedForJump)) {
		// 		_lastJumpTimer = 0F;

		// 		if (rigidbody.velocity.y < 0F) {
		// 			Vector3 v = new Vector3(rigidbody.velocity.x, 0F, rigidbody.velocity.z);
		// 			rigidbody.velocity = v;
		// 		}

		// 		rigidbody.velocity += Vector3.up * 0.5F
		// 						            * JUMP_POWER;
		// 	}
		// 	// (Consume the jump press.)
		//   if (_jumpJustPressed) { _jumpJustPressed = false; }

		//   // Angular Velocity //

		// 	// Turning
		// 	Quaternion uprightingRotation = Quaternion.FromToRotation(rigidbody.rotation * Vector3.up, Vector3.up);
		// 	rigidbody.rotation = uprightingRotation * rigidbody.rotation;

		// 	Vector3 targetAngularVelocity = Vector3.zero;

		// 	if (hasIntendedMovement) {
		//     Quaternion targetRot = Quaternion.LookRotation(targetFacing, Vector3.up);
		// 		Quaternion curRotToTargetRot = targetRot * Quaternion.Inverse(rigidbody.rotation);

		// 		targetAngularVelocity = curRotToTargetRot.eulerAngles.FlipAnglesAbove180()
		// 													* Time.fixedDeltaTime
		// 													* TURN_POWER;
		// 	}

		// 	// Set angular velocity.
		// 	Vector3 deltaAngularVelocity = Vector3.Lerp(rigidbody.angularVelocity,
		// 																					 targetAngularVelocity,
		// 																					 20F * Time.fixedDeltaTime)
		// 																 - rigidbody.angularVelocity;
		// 	rigidbody.angularVelocity += deltaAngularVelocity;
		// }

		// private void onPostPhysics() {

		// }

		// #endregion

		#region Gizmos

		void OnDrawGizmos() {
			// Target facing
			// Gizmos.color = Color.blue;
			// Gizmos.DrawLine(this.transform.position, this.transform.position + targetFacing);
			// Gizmos.DrawCube(this.transform.position + targetFacing, Vector3.one * 0.2F);

			// // Current facing
			// Gizmos.DrawCube(this.transform.position + currentFacing * 0.7F, Vector3.one * 0.22F);

			// // Target run vector
			// Gizmos.color = Color.red;
			// Gizmos.DrawCube(this.transform.position + targetRunVector, Vector3.one * 0.25F);

			// Ground normals
			// Gizmos.color = Color.blue;
			// if (!isGrounded) Gizmos.color = Color.red;
			// foreach (var groundingObj in _groundingObjectsMap.GetValuesNonAlloc()) {
			// 	foreach (var orbit in groundingObj.contactPoint.Orbit(axis: groundingObj.contactNormal,
			// 																												radius: 0.05F,
			// 																												resolution: 16)) {
			// 		Gizmos.DrawRay(orbit.position, orbit.axisDir * 3F);
			// 	}
			// }

			// Test on collision enter
			// Gizmos.color = Color.Lerp(Color.green, Color.black, 0.5F);
			// foreach (var orbit in _testPos.Orbit(_testVector, 0.05F, 16)) {
			// 	Gizmos.DrawRay(orbit.position, orbit.axisDir * 3F);
			// }
		}

		#endregion

	}

}
