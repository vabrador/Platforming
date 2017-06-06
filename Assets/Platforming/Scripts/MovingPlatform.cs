using UnityEngine;

public class MovingPlatform : MonoBehaviour {
	
	[SerializeField]
  private Rigidbody _rigidbody;
	public
	#if UNITY_EDITOR
	new
	#endif
	Rigidbody rigidbody { get { return _rigidbody; } }
	
	private Transform _origTarget;
	public Transform moveTarget;

	public AnimationCurve moveAnim = new AnimationCurve();

	public float speedMultiplier = 1F;

	public float initTime = 0F;

	public bool clampAnim01 = true;
	
	private float _animTime = 0F;

	void Start() {
		if (_rigidbody == null) _rigidbody = GetComponent<Rigidbody>();
		rigidbody.isKinematic = true;
		rigidbody.useGravity = false;

		GameObject origTargetObj = new GameObject("__Original Target Transform__");
		_origTarget = origTargetObj.transform;
		_origTarget.transform.parent = this.transform;
		_origTarget.localPosition = Vector3.zero;
		_origTarget.localRotation = Quaternion.identity;
		_origTarget.localScale = Vector3.one;

		_animTime = initTime;
	}

	void FixedUpdate() {
		_animTime += Time.fixedDeltaTime * speedMultiplier;
		float animPos = moveAnim.Evaluate(_animTime % moveAnim.keys[moveAnim.length-1].time);

		if (clampAnim01) { animPos = Mathf.Clamp01(animPos); }

		Vector3 origTargetPosPreMove = _origTarget.position;
		Quaternion origTargetRotPreMove = _origTarget.rotation;
		Vector3 moveTargetPosPreMove = moveTarget.position;
		Quaternion moveTargetRotPreMove = moveTarget.rotation;

		Vector3    newPos = Vector3.Lerp(_origTarget.position, moveTarget.position, animPos);
		Quaternion newRot = Quaternion.SlerpUnclamped(_origTarget.rotation, moveTarget.rotation, animPos);

		this.transform.position = newPos;
		this.transform.rotation = newRot;

		this.rigidbody.position = newPos;
		this.rigidbody.rotation = newRot;

		_origTarget.position = origTargetPosPreMove;
		_origTarget.rotation = origTargetRotPreMove;
		moveTarget.position = moveTargetPosPreMove;
		moveTarget.rotation = moveTargetRotPreMove;
	}

}
