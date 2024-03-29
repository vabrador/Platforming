﻿using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using InternalRealtimeCSG;
using UnityEngine.SceneManagement;

namespace RealtimeCSG
{
#if !DEMO
	[CustomEditor(typeof(CSGModelExported))]
	[CanEditMultipleObjects]
	[System.Reflection.Obfuscation(Exclude = true)]
	internal sealed class ExportedModelEditor : Editor
	{
		public override void OnInspectorGUI()
		{
			GUILayout.BeginVertical(GUI.skin.box);
			{
				GUILayout.Label("A hidden CSG model, and all it's brushes and operations, is contained by this object. This will automatically be removed at build time.", GUIStyleUtility.wrapLabel);
				GUILayout.Space(10);
				if (GUILayout.Button("Revert back to CSG model"))
				{
					Undo.IncrementCurrentGroup();
					var groupIndex = Undo.GetCurrentGroup();
					Undo.SetCurrentGroupName("Reverted to former CSG model");
					try
					{
						var selection = new List<UnityEngine.Object>();
						var updateScenes = new HashSet<Scene>();
						foreach (var target in targets)
						{
							var exportedModel = target as CSGModelExported;
							if (!exportedModel)
								continue;

							exportedModel.disarm = true;
							exportedModel.hideFlags = HideFlags.DontSaveInBuild;
							updateScenes.Add(exportedModel.gameObject.scene);
							if (exportedModel.containedModel)
							{
								Undo.RecordObject(exportedModel.containedModel, "Revert model");
								selection.Add(exportedModel.containedModel.gameObject);
								exportedModel.containedModel.transform.SetParent(exportedModel.transform.parent, true);
								exportedModel.containedModel.transform.SetSiblingIndex(exportedModel.transform.GetSiblingIndex());
								exportedModel.containedModel.gameObject.SetActive(true);
								exportedModel.containedModel.gameObject.hideFlags = HideFlags.None;
								EditorUtility.SetDirty(exportedModel.containedModel);
								Undo.DestroyObjectImmediate(exportedModel);
							} else
							{
								MeshInstanceManager.ReverseExport(exportedModel);
								selection.Add(exportedModel.gameObject);
								EditorUtility.SetDirty(exportedModel);
								Undo.DestroyObjectImmediate(exportedModel);
							}
						}
						Selection.objects = selection.ToArray();

						InternalCSGModelManager.skipRefresh = true;

						BrushOutlineManager.ClearOutlines();
						//CSGModelManager.Refresh(forceHierarchyUpdate: true);
						InternalCSGModelManager.Rebuild();
						foreach (var scene in updateScenes)
							UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
					}
					finally
					{
						InternalCSGModelManager.skipRefresh = false;
						Undo.CollapseUndoOperations(groupIndex);
					}
				}
			}
			GUILayout.Space(30);
			if (GUILayout.Button("Remove hidden CSG model"))
			{
				Undo.IncrementCurrentGroup();
				var groupIndex = Undo.GetCurrentGroup();
				Undo.SetCurrentGroupName("Removed hidden CSG model");
				Undo.RecordObject(this, "Removed hidden CSG model");
				foreach (var target in targets)
				{
					var exportedModel = target as CSGModelExported;
					if (!exportedModel)
						continue;
					exportedModel.DestroyModel(undoable: true);
					Undo.DestroyObjectImmediate(exportedModel);
				}
				Undo.CollapseUndoOperations(groupIndex);
			}
			GUILayout.Space(10);
			GUILayout.EndVertical();
		}
	}
#endif
}