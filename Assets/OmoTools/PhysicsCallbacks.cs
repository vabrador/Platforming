using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace OmoTools {

	public class PhysicsCallbacks : MonoBehaviour {
		
		public static Action OnPostPhysics {
			get { return instance.instanceOnPostPhysics; }
			set { instance.instanceOnPostPhysics = value; }
		}

		private static PhysicsCallbacks s_instance = null;
		public static PhysicsCallbacks instance {
			get {
				if (s_instance == null) {
					GameObject instanceObj = new GameObject("__Physics Callbacks__");
					s_instance = instanceObj.AddComponent<PhysicsCallbacks>();
				}
				return s_instance;
			}
		}

		private Action instanceOnPostPhysics = () => { };

		private Rigidbody _trigger0;
		private Rigidbody _trigger1;

		void Awake() {
			this.transform.position = new Vector3(-10000F, -10000F, -10000F);
			_trigger0 = gameObject.AddComponent<Rigidbody>();
			this.gameObject.AddComponent<BoxCollider>().isTrigger = true;

			GameObject trigger1Obj = new GameObject("__Trigger 1__");
			trigger1Obj.transform.position = new Vector3(-10000F, -10000F, -10000F);
			trigger1Obj.transform.parent = this.transform;
			_trigger1 = trigger1Obj.AddComponent<Rigidbody>();
			trigger1Obj.AddComponent<BoxCollider>().isTrigger = true;
		}

		void OnTriggerStay(Collider collider) {
			instanceOnPostPhysics();
		}

	}


}