﻿using UnityEditor;
using UnityEngine;
using InternalRealtimeCSG;

namespace RealtimeCSG
{
	internal static class SnappedPoint
	{
		internal static int SnappedPointHash = "SnappedPoint".GetHashCode();
		private static Vector2 startMousePosition;
		private static CSGPlane    movePlane;
//		private static Vector2 currentMousePosition;
//		private static Vector3 startPosition;
		
		public delegate void	DrawCapFunction	(int controlID, Vector3 position, Quaternion rotation, float size);		
		public delegate Vector3 SnapFunction(Vector2 startMousePosition, Vector2 currentMousePosition);

		public static SnapFunction snapFunction = null;

		public static Vector3 FreeMoveHandle (Vector3 position, Quaternion rotation, float size, DrawCapFunction capFunc = null)
		{
			int id = GUIUtility.GetControlID (SnappedPointHash, FocusType.Keyboard);
			return FreeMoveHandle(id, position, rotation, size, capFunc);
		}

		public static Vector3 FreeMoveHandle(int id, Vector3 position, Quaternion rotation, float size, DrawCapFunction capFunc = null)
		{
			if (capFunc == null)
				capFunc = PaintUtility.SquareDotCap;
			Vector3		worldPosition	= Handles.matrix.MultiplyPoint(position);
			Matrix4x4	origMatrix		= Handles.matrix;
						
			switch (Event.current.GetTypeForControl(id))
			{
				case EventType.layout:
				{ 
					Handles.matrix = MathConstants.identityMatrix;
					HandleUtility.AddControl(id, HandleUtility.DistanceToCircle(worldPosition, size * 1.2f));
					Handles.matrix = origMatrix;
					break;
				}
				case EventType.mouseDown:
				{
					if (GUIUtility.hotControl == 0 &&
						HandleUtility.nearestControl == id && Event.current.button == 0)
					{
						GUIUtility.hotControl = id;
						GUIUtility.keyboardControl = id;
						EditorGUIUtility.editingTextField = false; 

						movePlane = Grid.CurrentGridPlane;
						movePlane = new CSGPlane(movePlane.normal, position);

						startMousePosition = Event.current.mousePosition;
//						currentMousePosition = startMousePosition;
//						startPosition = position;

						Event.current.Use();
						EditorGUIUtility.SetWantsMouseJumping(1);
					}
					break;
				}
				case EventType.mouseDrag:
				{
					if (GUIUtility.hotControl == id)
					{
						if (snapFunction == null)
							break;
						
						position = snapFunction(startMousePosition, Event.current.mousePosition);
						if (Camera.current != null && Camera.current.orthographic)
							position = movePlane.Project(position);

						GUI.changed = true;
						Event.current.Use();
					}
					break;
				}
				case EventType.mouseUp:
				{
					if (GUIUtility.hotControl == id && Event.current.button == 0)
                    {
                        GUIUtility.hotControl = 0;
						GUIUtility.keyboardControl = 0;
						EditorGUIUtility.editingTextField = false;
						Event.current.Use();
						EditorGUIUtility.SetWantsMouseJumping(0);
					}
					break;
				}
				case EventType.repaint:
				{
					bool isSelected = (id == GUIUtility.keyboardControl) || (id == GUIUtility.hotControl);
					Color temp = Handles.color;
					if (isSelected)
						Handles.color = Handles.selectedColor; // Why U not work?
					else
						Handles.color = Color.gray;
					
					Handles.matrix = MathConstants.identityMatrix;
					capFunc(id, worldPosition, Camera.current.transform.rotation, size);
					Handles.matrix = origMatrix;

					if (isSelected)
						Handles.color = temp;
					break;
				}
			}
			return position;
		}
	}
}
