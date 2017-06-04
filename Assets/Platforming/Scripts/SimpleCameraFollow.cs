using UnityEngine;

public class SimpleCameraFollow : MonoBehaviour {
	
	public Transform target;

	private float _targetRelativeAltitude;
	private float _targetMaxRelativeForwardOffset;

	private float _minRelativeForwardOffset = 3F;

	void Awake() {
		_targetRelativeAltitude = getRelativeAltitudeToTarget();
		_targetMaxRelativeForwardOffset = getRelativeForwardOffsetToTarget();
	}

	private const float HORIZONTAL_LOOK_SENSITIVITY = 10F;

	private float _manualTwistVelocity = 0F;

	void Update() {
		// Manual camera orbit
		float manualTwistPower = Input.GetAxis("Horizontal Look");
		_manualTwistVelocity = Mathf.Lerp(_manualTwistVelocity, manualTwistPower * 30F * HORIZONTAL_LOOK_SENSITIVITY, 5F * Time.deltaTime);
		this.transform.RotateAround(target.transform.position, Vector3.down, _manualTwistVelocity * Time.deltaTime);
		
		// Automatic look-at-target rotation
		this.transform.rotation = Quaternion.LookRotation(target.transform.position - this.transform.position, Vector3.up);

		// Height correction
		float currentRelativeAltitude = getRelativeAltitudeToTarget();
		float currentToTargetAltitude = _targetRelativeAltitude - currentRelativeAltitude;
		Vector3 altitudeCorrection = altitudeDirection * currentToTargetAltitude;
		this.transform.position = Vector3.Lerp(this.transform.position, this.transform.position + altitudeCorrection, 0.4F * Time.deltaTime);

		// Forward offset correction
		float currentForwardOffset = getRelativeForwardOffsetToTarget();
		Vector3 forwardCorrection = Vector3.zero;
		if (currentForwardOffset > _targetMaxRelativeForwardOffset || currentForwardOffset < _minRelativeForwardOffset) {
      float currentToTargetForward = _targetMaxRelativeForwardOffset - currentForwardOffset;
			forwardCorrection = forwardDirection * currentToTargetForward;
		}
		this.transform.position = Vector3.Lerp(this.transform.position, this.transform.position + forwardCorrection, 1F * Time.deltaTime);
	}

	private Vector3 altitudeDirection {
		get { return Vector3.up; }
	}

	private Vector3 forwardDirection {
	  get { return Vector3.Cross(-this.transform.right, Vector3.up).normalized; }
	}

	private float getRelativeAltitudeToTarget() {
		return Vector3.Dot(altitudeDirection, (this.transform.position - target.position));
	}

	private float getRelativeForwardOffsetToTarget() {
		return Vector3.Dot(forwardDirection, (this.transform.position - target.position));
	}

}
