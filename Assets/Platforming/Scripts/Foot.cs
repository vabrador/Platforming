using OmoTools;
using UnityEngine;

namespace Platforming {

	public class Foot : MonoBehaviour {

    public PlayerController player;
		public Transform hipJoint;

    private Vector3 _baseRelativePosition;
    public Vector3 basePosition { get { return player.transform.TransformPoint(_baseRelativePosition); } }
		public Vector3 liftedPosition { get { return player.transform.TransformPoint(_baseRelativePosition)
																								 + Vector3.up * 0.3F; } }

		private float _maxLegLength;
		public float maxLegLength { get { return _maxLegLength; } }

		public Vector3 hipToFoot { get { return this.transform.position - hipJoint.position; } }
		
		public Collider contactCollider { get; set; }
		public Rigidbody contactBody { get; set; }
		public Ray contact { get; set; }
		public bool hasContact { get { return contactCollider != null; } }
		public Vector3 contactPoint { get { return contact.origin; } }
		public Vector3 contactNormal { get { return contact.direction; } }

		void OnValidate() {
			refreshLegData();
		}

		void Start() {
			refreshLegData();
		}

		private void refreshLegData() {
			_baseRelativePosition = this.transform.position;
			_maxLegLength = hipToFoot.magnitude;
		}

		private RaycastHit[] _hitsBuffer = new RaycastHit[32];

		public bool RefreshStandPoint() {
			this.contactCollider = null;
			this.contact = new Ray();
			this.contactBody = null;
			this.transform.position = liftedPosition;

			Ray ray = new Ray(hipJoint.position, Vector3.down);
			int numHits = Physics.RaycastNonAlloc(ray,
																						_hitsBuffer,
																						maxLegLength,
																						~(1 << this.gameObject.layer));

			for (int i = 0; i < numHits; i++) {
				RaycastHit hit = _hitsBuffer[i];
				
				this.contactCollider = hit.collider;
				this.contact = new Ray(hit.point, hit.normal);
				this.contactBody = hit.rigidbody;
				this.transform.position = contact.origin;

				return true;
			}

			return false;
		}

		void OnDrawGizmosSelected() {
			if (!Application.isPlaying) {
			  refreshLegData();
			}

			Gizmos.color = Color.Lerp(Color.green, Color.white, 0.5F).SetAlpha(0.5F);
			foreach (var orbit in new Orbit(hipJoint.position, hipToFoot, 0.05F, 8)) {
				Gizmos.DrawLine(orbit.position, orbit.position + orbit.axisDir * maxLegLength);
			}
		}

		void OnDrawGizmos() {
			
			if (hasContact) {
				Gizmos.color = Color.blue;
				foreach (var orbit in new Orbit(contactPoint, contactNormal, 0.1F, 8)) {
					Gizmos.DrawLine(orbit.position, orbit.center + orbit.axisDir * 0.3F);
				}
			}

		}

	}

}