using UnityEngine;
using System.Collections.Generic;
using RealtimeCSG;
using System;

namespace InternalRealtimeCSG
{
	[DisallowMultipleComponent]
	[ExecuteInEditMode]
	[SelectionBase]
	[System.Reflection.Obfuscation(Exclude = true)]
	public sealed class GeneratedMeshes : MonoBehaviour
	{
		[HideInInspector] public float Version = 1.00f;
#if UNITY_EDITOR
		public CSGModel				owner;

		[NonSerialized] [HideInInspector] public Rigidbody		CachedRigidBody;

		[System.NonSerialized]
		public readonly Dictionary<MeshInstanceKey, GeneratedMeshInstance> meshInstances = new Dictionary<MeshInstanceKey, GeneratedMeshInstance>();


		void Awake()
		{
			// cannot change visibility since this might have an effect on exporter
			this.hideFlags |= HideFlags.DontSaveInBuild;
			if (CSGSceneManagerRedirector.Interface != null)
				CSGSceneManagerRedirector.Interface.OnCreated(this);
		} 

		void OnDestroy()
		{
			if (CSGSceneManagerRedirector.Interface != null)
				CSGSceneManagerRedirector.Interface.OnDestroyed(this);
		}
#else
		void Awake()
		{
			//this.hideFlags = HideFlags.DontSaveInBuild;
			this.gameObject.tag = "EditorOnly"; 
		}
#endif
	}
}
