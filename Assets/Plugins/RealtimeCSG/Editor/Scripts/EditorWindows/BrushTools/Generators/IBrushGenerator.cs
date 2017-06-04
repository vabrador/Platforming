using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RealtimeCSG
{
	internal interface IBrushGenerator
	{
		bool HaveBrushes { get; }
		bool CanCommit { get; }
		CSGOperationType CurrentCSGOperationType { get; set; }
		bool OnShowGUI(bool isSceneGUI);
		void Init();
		bool HotKeyReleased();
		bool UndoRedoPerformed();
		void PerformDeselectAll();
		void HandleEvents(Rect sceneRect);
		
		// unity bug workaround
		void StartGUI();
		void DoCancel();
		void DoCommit();
		void FinishGUI();
		void OnDefaultMaterialModified();
	}
}
