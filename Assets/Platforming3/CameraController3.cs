using UnityEngine;

namespace Platforming {

	public class CameraController3 : MonoBehaviour {

    public CustomGravityController gravityController;
		
		public Transform target;

		public float targetRelativeAltitude = 1F;
		public float targetMaxRelativeForwardOffset = 1F;

		public float minRelativeForwardOffset = 0.5F;

    public float heightCorrectionConst = 0.4f;
    public float forwardCorrectionConst = 1f;

		private void onPlayerReferenceFrameTranslated(Vector3 translation) {
			this.transform.position += translation;
		}

		private const float HORIZONTAL_LOOK_SENSITIVITY = 10F;

		private float _manualTwistVelocity = 0F;

		void Update() {
			// Manual camera orbit
			float manualTwistPower = Input.GetAxis("Horizontal Look");
			_manualTwistVelocity = Mathf.Lerp(_manualTwistVelocity, manualTwistPower * 30F * HORIZONTAL_LOOK_SENSITIVITY, 5F * Time.deltaTime);
			this.transform.RotateAround(target.position, Vector3.down, _manualTwistVelocity * Time.deltaTime);
			
			// Automatic look-at-target rotation
			this.transform.rotation = Quaternion.LookRotation(target.position - this.transform.position, Vector3.up);

			// Height correction
			float currentRelativeAltitude = getRelativeAltitudeToTarget();
			float currentToTargetAltitude = targetRelativeAltitude - currentRelativeAltitude;
			Vector3 altitudeCorrection = altitudeDirection * currentToTargetAltitude;
			this.transform.position = Vector3.Lerp(this.transform.position, this.transform.position + altitudeCorrection, heightCorrectionConst * Time.deltaTime);

			// Forward offset correction
			float currentForwardOffset = getRelativeForwardOffsetToTarget();
			Vector3 forwardCorrection = Vector3.zero;
			if (currentForwardOffset > targetMaxRelativeForwardOffset || currentForwardOffset < minRelativeForwardOffset) {
				float currentToTargetForward = targetMaxRelativeForwardOffset - currentForwardOffset;
				forwardCorrection = forwardDirection * currentToTargetForward;
			}
			this.transform.position = Vector3.Lerp(this.transform.position, this.transform.position + forwardCorrection, forwardCorrectionConst * Time.deltaTime);
		}

		private Vector3 altitudeDirection {
			get { return -gravityController.gravity.normalized; }
		}

		private Vector3 forwardDirection {
			get { return Vector3.Cross(-this.transform.right, altitudeDirection).normalized; }
		}

		private float getRelativeAltitudeToTarget() {
			return Vector3.Dot(altitudeDirection, (this.transform.position - target.position));
		}

		private float getRelativeForwardOffsetToTarget() {
			return Vector3.Dot(forwardDirection, (this.transform.position - target.position));
		}

	}

}