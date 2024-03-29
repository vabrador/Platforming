﻿using UnityEngine;
using UnityEditor;

namespace RealtimeCSG.Helpers
{
	public sealed partial class CSGHandles
	{
		public delegate void InitFunction();
		public delegate void CapFunction(int controlID, Vector3 position, Quaternion rotation, float size, EventType eventType);

		static Vector3[]	s_Vertices				= new Vector3[4];
		static Vector3[]	s_Points				= new Vector3[5];
		static Vector3		s_PlanarHandlesOctant	= Vector3.one;
		
		enum PlaneHandle { xzPlane, xyPlane, yzPlane };

		internal static Color staticColor = new Color(.5f, .5f, .5f, 0f);
		internal static float staticBlend = 0.6f;
		
		internal static int s_xzAxisMoveHandleHash = "xzAxisFreeMoveHandleHash".GetHashCode();
		internal static int s_xyAxisMoveHandleHash = "xyAxisFreeMoveHandleHash".GetHashCode();
		internal static int s_yzAxisMoveHandleHash = "yzAxisFreeMoveHandleHash".GetHashCode();
		
		internal static int s_ScaleSliderHash		= "ScaleSliderHash".GetHashCode();
		internal static int s_ScaleValueHandleHash	= "ScaleValueHandleHash".GetHashCode();
		internal static int s_DiscHash				= "DiscHash".GetHashCode();
		internal static int s_FreeRotateHandleHash	= "FreeRotateHandleHash".GetHashCode();

		internal static int s_SliderHash			= "SliderHash".GetHashCode();

		public static void CubeHandleCap(int controlID, Vector3 position, Quaternion rotation, float size, EventType eventType)
		{
#if UNITY_5_6_OR_NEWER
			Handles.CubeHandleCap(controlID, position, rotation, size, eventType);
#else
			switch (eventType)
			{
				case EventType.Layout:
				{
					HandleUtility.AddControl(controlID, HandleUtility.DistanceToCircle(position, size));
					break;
				}
				case EventType.Repaint:
				{ 
					var direction = rotation * Vector3.forward;
					Handles.CubeCap(controlID, position, Quaternion.LookRotation(direction), size * .2f);
					break;
				}
			}
#endif
		}

		public static void ArrowHandleCap (int controlID, Vector3 position, Quaternion rotation, float size, EventType eventType) 
		{
#if UNITY_5_6_OR_NEWER
			Handles.ArrowHandleCap(controlID, position, rotation, size, eventType);
#else
			switch (eventType)
			{
				case EventType.Layout:
				{
					var direction = rotation * Vector3.forward;
					HandleUtility.AddControl (controlID, HandleUtility.DistanceToLine (position, position + direction * size * .9f));
					HandleUtility.AddControl (controlID, HandleUtility.DistanceToCircle (position + direction * size, size * .2f));
					break;
				}
				case EventType.Repaint:
				{
					var direction = rotation * Vector3.forward;
					ConeHandleCap (controlID, position + direction * size, Quaternion.LookRotation (direction), size * .2f, eventType);
					Handles.DrawLine (position, position + direction * size * .9f);
					break;
				}
			}
#endif
		}

		public static void ConeHandleCap (int controlID, Vector3 position, Quaternion rotation, float size, EventType eventType) 
		{
#if UNITY_5_6_OR_NEWER
			Handles.ConeHandleCap(controlID, position, rotation, size, eventType);
#else
			switch (eventType)
			{
				case (EventType.Layout):
					HandleUtility.AddControl (controlID, HandleUtility.DistanceToCircle (position, size));
					break;
				case (EventType.Repaint):
					Handles.ConeCap(controlID, position, rotation, size);
					break;
			}
#endif
		}

		
		internal static float DistanceToRectangleInternal (Vector3 position, Quaternion rotation, Vector2 size)
		{
			var sideways	= rotation * new Vector3 (size.x, 0, 0);
			var up			= rotation * new Vector3 (0, size.y, 0);
			s_Points[0] = HandleUtility.WorldToGUIPoint (position + sideways + up);
			s_Points[1] = HandleUtility.WorldToGUIPoint (position + sideways - up);
			s_Points[2] = HandleUtility.WorldToGUIPoint (position - sideways - up);
			s_Points[3] = HandleUtility.WorldToGUIPoint (position - sideways + up);
			s_Points[4] = s_Points[0];

			var pos = Event.current.mousePosition;
			var oddNodes = false;
			int j = 4;
			for (int i = 0; i < 5; i++) {
				if ((s_Points[i].y > pos.y) != (s_Points[j].y>pos.y))
				{
					if (pos.x < (s_Points[j].x-s_Points[i].x) * (pos.y-s_Points[i].y) / (s_Points[j].y-s_Points[i].y) + s_Points[i].x)
					{
						oddNodes = !oddNodes;
					}
				}
				j = i;
			}
			if(!oddNodes)
			{
				float dist, closestDist = -1f;
				j = 1;
				for (int i = 0; i < 4; i++) {
					dist = HandleUtility.DistancePointToLineSegment(pos, s_Points[i], s_Points[j++]);
					if(dist < closestDist || closestDist < 0)
						closestDist = dist;
				}
				return closestDist;
			}
			else
				return 0;
		}
	
		internal static bool ContainsStatic (GameObject[] objects)
		{
			if (objects == null || objects.Length == 0)
				return false;
			for (int i = 0; i < objects.Length; i++)
			{
				if (objects[i] != null && objects[i].isStatic)
					return true;
			}
			return false;
		}
		
		
		static Vector3[] s_RectangleHandlePointsCache = new Vector3[5];
		public static void RectangleHandleCap (int controlID, Vector3 position, Quaternion rotation, float size, EventType eventType) 
		{
			RectangleHandleCap(controlID, position, rotation, new Vector2(size, size), eventType);
		}

		internal static void RectangleHandleCap (int controlID, Vector3 position, Quaternion rotation, Vector2 size, EventType eventType) 
		{
			switch (eventType)
			{
				case (EventType.Layout):
					HandleUtility.AddControl (controlID, DistanceToRectangleInternal (position, rotation, size));
					break;
				case (EventType.Repaint):
					var sideways = rotation * new Vector3 (size.x, 0, 0);
					var up = rotation * new Vector3 (0, size.y, 0);
					s_RectangleHandlePointsCache[0] = position + sideways + up;
					s_RectangleHandlePointsCache[1] = position + sideways - up;
					s_RectangleHandlePointsCache[2] = position - sideways - up;
					s_RectangleHandlePointsCache[3] = position - sideways + up;
					s_RectangleHandlePointsCache[4] = position + sideways + up;
					Handles.DrawPolyLine (s_RectangleHandlePointsCache);
					break;
			}
		}

		public static Vector3 Slider (Vector3 position, Vector3 direction, float size, CapFunction capFunction, bool snapping, Vector3[] snapVertices, InitFunction initFunction, InitFunction shutdownFunction) 
		{
			var id = GUIUtility.GetControlID (s_SliderHash, FocusType.Keyboard);
			return CSGSlider1D.Do(id, position, direction, size, capFunction, snapping, snapVertices, initFunction, shutdownFunction);
		}
		
		public static Vector3 Slider2D(int id, Vector3 handlePos, Vector3 offset, Vector3 handleDir, Vector3 slideDir1, Vector3 slideDir2, float handleSize, CapFunction capFunction, bool snapping, Vector3[] snapVertices, InitFunction initFunction, InitFunction shutdownFunction)
		{
			return CSGSlider2D.Do(id, handlePos, offset, handleDir, slideDir1, slideDir2, handleSize, capFunction, snapping, snapVertices, initFunction, shutdownFunction);
		}

		internal static bool disabled = false;

		public static Vector3 PositionHandle(Vector3 position, Quaternion rotation, bool snapping, Vector3[] snapVertices = null, InitFunction initFunction = null, InitFunction shutdownFunction = null)
		{
			var size = HandleUtility.GetHandleSize(position);
			var originalColor = Handles.color;
			var isStatic = (!Tools.hidden && EditorApplication.isPlaying && ContainsStatic(Selection.gameObjects));

			var prevDisabled = CSGHandles.disabled;

			CSGHandles.disabled = prevDisabled || RealtimeCSG.CSGSettings.LockAxisX;
			{
				Handles.color = isStatic ? Color.Lerp(Handles.xAxisColor, staticColor, staticBlend) : Handles.xAxisColor;
				GUI.SetNextControlName("xAxis");
				position = Slider(position, rotation * Vector3.right, size, ArrowHandleCap, snapping, snapVertices, initFunction, shutdownFunction);
			}

			CSGHandles.disabled = prevDisabled || RealtimeCSG.CSGSettings.LockAxisY;
			{
				Handles.color = isStatic ? Color.Lerp(Handles.yAxisColor, staticColor, staticBlend) : Handles.yAxisColor;
				GUI.SetNextControlName("yAxis");
				position = Slider(position, rotation * Vector3.up, size, ArrowHandleCap, snapping, snapVertices, initFunction, shutdownFunction);
			}
			
			CSGHandles.disabled = prevDisabled || RealtimeCSG.CSGSettings.LockAxisZ;
			{
				Handles.color = isStatic ? Color.Lerp(Handles.zAxisColor, staticColor, staticBlend) : Handles.zAxisColor;
				GUI.SetNextControlName("zAxis");
				position = Slider(position, rotation * Vector3.forward, size, ArrowHandleCap, snapping, snapVertices, initFunction, shutdownFunction);
			}

			CSGHandles.disabled = prevDisabled || (RealtimeCSG.CSGSettings.LockAxisX || RealtimeCSG.CSGSettings.LockAxisZ);
			if (!CSGHandles.disabled)
				position = DoPlanarHandle(PlaneHandle.xzPlane, position, rotation, size * 0.25f, snapping, snapVertices, initFunction, shutdownFunction);

			CSGHandles.disabled = prevDisabled || (RealtimeCSG.CSGSettings.LockAxisX || RealtimeCSG.CSGSettings.LockAxisY);
			if (!CSGHandles.disabled)
				position = DoPlanarHandle(PlaneHandle.xyPlane, position, rotation, size * 0.25f, snapping, snapVertices, initFunction, shutdownFunction);

			CSGHandles.disabled = prevDisabled || (RealtimeCSG.CSGSettings.LockAxisY || RealtimeCSG.CSGSettings.LockAxisZ);
			if (!CSGHandles.disabled)
				position = DoPlanarHandle(PlaneHandle.yzPlane, position, rotation, size * 0.25f, snapping, snapVertices, initFunction, shutdownFunction);
			
			CSGHandles.disabled = prevDisabled;
			Handles.color = originalColor;

			return position;
		}

		public static float ScaleSlider(float scale, Vector3 position, Vector3 direction, Quaternion rotation, float size, bool snapping, float snap, CSGHandles.InitFunction initFunction, CSGHandles.InitFunction shutdownFunction)
		{
			int id = GUIUtility.GetControlID(s_ScaleSliderHash, FocusType.Keyboard);
			return CSGScaleSlider.DoAxis(id, scale, position, direction, rotation, size, snapping, snap, initFunction, shutdownFunction);
		}

		public static Vector3 ScaleValueHandle(Vector3 value, Vector3 position, Quaternion rotation, float size, CapFunction capFunction, bool snapping, float snap, CSGHandles.InitFunction initFunction, CSGHandles.InitFunction shutdownFunction)
		{
			int id = GUIUtility.GetControlID(s_ScaleValueHandleHash, FocusType.Keyboard);
			return CSGScaleSlider.DoCenter(id, value, position, rotation, size, capFunction, snapping, snap, initFunction, shutdownFunction);
		}

		public static Vector3 ScaleHandle(Vector3 scale, Vector3 position, Quaternion rotation, bool snapping, CSGHandles.InitFunction initFunction, CSGHandles.InitFunction shutdownFunction)
		{
			var size = HandleUtility.GetHandleSize(position);
			var originalColor = Handles.color;
			var isStatic = (!Tools.hidden && EditorApplication.isPlaying && ContainsStatic(Selection.gameObjects));

			var prevDisabled = CSGHandles.disabled;

			CSGHandles.disabled = prevDisabled || RealtimeCSG.CSGSettings.LockAxisX;
			{
				Handles.color = isStatic ? Color.Lerp(Handles.xAxisColor, staticColor, staticBlend) : Handles.xAxisColor;
				GUI.SetNextControlName("xAxis");
				scale.x = ScaleSlider(scale.x, position, rotation * Vector3.right, rotation, size, snapping, RealtimeCSG.CSGSettings.SnapScale, initFunction, shutdownFunction);
			}

			CSGHandles.disabled = prevDisabled || RealtimeCSG.CSGSettings.LockAxisY;
			{
				Handles.color = isStatic ? Color.Lerp(Handles.yAxisColor, staticColor, staticBlend) : Handles.yAxisColor;
				GUI.SetNextControlName("yAxis");
				scale.y = ScaleSlider(scale.y, position, rotation * Vector3.up, rotation, size, snapping, RealtimeCSG.CSGSettings.SnapScale, initFunction, shutdownFunction);
			}

			CSGHandles.disabled = prevDisabled || RealtimeCSG.CSGSettings.LockAxisZ;
			{
				Handles.color = isStatic ? Color.Lerp(Handles.zAxisColor, staticColor, staticBlend) : Handles.zAxisColor;
				GUI.SetNextControlName("zAxis");
				scale.z = ScaleSlider(scale.z, position, rotation * Vector3.forward, rotation, size, snapping, RealtimeCSG.CSGSettings.SnapScale, initFunction, shutdownFunction);
			}

			CSGHandles.disabled = prevDisabled;
			Handles.color = Handles.centerColor;
			scale = ScaleValueHandle(scale, position, rotation, size, CubeHandleCap, snapping, RealtimeCSG.CSGSettings.SnapScale, initFunction, shutdownFunction);
			

			if (float.IsInfinity(scale.x) || float.IsNaN(scale.x) ||
			    float.IsInfinity(scale.y) || float.IsNaN(scale.y) ||
			    float.IsInfinity(scale.z) || float.IsNaN(scale.z))
				scale = Vector3.zero;

			CSGHandles.disabled = prevDisabled;
			Handles.color = originalColor;
			return scale;
		}

		public static Quaternion Disc(Quaternion rotation, Vector3 position, Vector3 axis, float size, bool cutoffPlane, bool snapping, float snap, CSGHandles.InitFunction initFunction, CSGHandles.InitFunction shutdownFunction = null)
		{
			int id = GUIUtility.GetControlID(s_DiscHash, FocusType.Keyboard);
			return CSGDisc.Do(id, null, rotation, position, axis, size, cutoffPlane, snapping, snap, initFunction, shutdownFunction);
		}

		public static Quaternion FreeRotateHandle(Quaternion rotation, Vector3 position, float size, bool snapping, CSGHandles.InitFunction initFunction, CSGHandles.InitFunction shutdownFunction = null)
		{
			int id = GUIUtility.GetControlID(s_FreeRotateHandleHash, FocusType.Keyboard);
			return CSGFreeRotate.Do(id, rotation, position, size, snapping, initFunction, shutdownFunction);
		}

		public static Quaternion DoRotationHandle(Quaternion rotation, Vector3 position, bool snapping, CSGHandles.InitFunction initFunction, CSGHandles.InitFunction shutdownFunction = null)
		{
			int hotControl = EditorGUIUtility.hotControl;
			int xAxisDiscID = GUIUtility.GetControlID(s_DiscHash, FocusType.Keyboard);
			int yAxisDiscID = GUIUtility.GetControlID(s_DiscHash, FocusType.Keyboard);
			int zAxisDiscID = GUIUtility.GetControlID(s_DiscHash, FocusType.Keyboard);
			int freeDiscID = GUIUtility.GetControlID(s_DiscHash, FocusType.Keyboard);
			int freeRotateID = GUIUtility.GetControlID(s_FreeRotateHandleHash, FocusType.Keyboard);
				
			bool haveControl = (hotControl == xAxisDiscID) ||
							   (hotControl == yAxisDiscID) ||
							   (hotControl == zAxisDiscID) ||
							   (hotControl == freeDiscID) ||
							   (hotControl == freeRotateID);


			float size = HandleUtility.GetHandleSize(position);
			Color temp = Handles.color;

			var isStatic = (!Tools.hidden && EditorApplication.isPlaying && ContainsStatic(Selection.gameObjects));
			Handles.color = isStatic ? Color.Lerp(Handles.xAxisColor, staticColor, staticBlend) : Handles.xAxisColor;
			if (!haveControl || hotControl == xAxisDiscID)
				rotation = CSGDisc.Do(xAxisDiscID, "X", rotation, position, rotation * Vector3.right, size, true, snapping, RealtimeCSG.CSGSettings.SnapRotation, initFunction, shutdownFunction);
			
			Handles.color = isStatic ? Color.Lerp(Handles.yAxisColor, staticColor, staticBlend) : Handles.yAxisColor;
			if (!haveControl || hotControl == yAxisDiscID)
				rotation = CSGDisc.Do(yAxisDiscID, "Y", rotation, position, rotation * Vector3.up, size, true, snapping, RealtimeCSG.CSGSettings.SnapRotation, initFunction, shutdownFunction);
			
			Handles.color = isStatic ? Color.Lerp(Handles.zAxisColor, staticColor, staticBlend) : Handles.zAxisColor;
			if (!haveControl || hotControl == zAxisDiscID)
				rotation = CSGDisc.Do(zAxisDiscID, "Z", rotation, position, rotation * Vector3.forward, size, true, snapping, RealtimeCSG.CSGSettings.SnapRotation, initFunction, shutdownFunction);
			
			if (!isStatic)
			{
				Handles.color = Handles.centerColor;
				if (!haveControl || hotControl == freeDiscID)
					rotation = CSGDisc.Do(freeDiscID, null, rotation, position, Camera.current.transform.forward, size * 1.1f, false, snapping, 0, initFunction, shutdownFunction);
		
				if (!haveControl || hotControl == freeRotateID)
					rotation = CSGFreeRotate.Do(freeRotateID, rotation, position, size, snapping, initFunction, shutdownFunction);
			}

			Handles.color = temp;
			return rotation;
		}

		static Vector3 DoPlanarHandle(PlaneHandle planeID, Vector3 position, Quaternion rotation, float handleSize, bool snapping, Vector3[] snapVertices, CSGHandles.InitFunction initFunction, InitFunction shutdownFunction = null)
		{
			int axis1index = 0;
			int axis2index = 0;
			int moveHandleHash = 0;
			var isStatic = (!Tools.hidden && EditorApplication.isPlaying && ContainsStatic(Selection.gameObjects));
			switch (planeID)
			{
				case PlaneHandle.xzPlane:
				{
					axis1index = 0;
					axis2index = 2;
					Handles.color = isStatic? staticColor : Handles.yAxisColor;
					moveHandleHash = s_xzAxisMoveHandleHash;
					break;
				}
				case PlaneHandle.xyPlane:
				{
					axis1index = 0;
					axis2index = 1;
					Handles.color = isStatic? staticColor : Handles.zAxisColor;
					moveHandleHash = s_xyAxisMoveHandleHash;
					break;
				}
				case PlaneHandle.yzPlane:
				{
					axis1index = 1;
					axis2index = 2;
					Handles.color = isStatic? staticColor : Handles.xAxisColor;
					moveHandleHash = s_yzAxisMoveHandleHash;
					break;
				}
			}
			
			int axisNormalIndex = 3 - axis2index - axis1index;
			var prevColor = Handles.color;

			var handleTransform = Matrix4x4.TRS(position, rotation, Vector3.one);
			var sceneView = SceneView.currentDrawingSceneView;
			var cameraToTransformToolVector = handleTransform.inverse.MultiplyPoint(sceneView.camera.transform.position).normalized;
			
			int id = GUIUtility.GetControlID (moveHandleHash, FocusType.Keyboard);
			
			if (Mathf.Abs (cameraToTransformToolVector[axisNormalIndex]) < 0.05f && GUIUtility.hotControl != id)
			{
				Handles.color = prevColor;
				return position;
			}
			
			if (EditorGUIUtility.hotControl == 0)
			{
				s_PlanarHandlesOctant[axis1index] = (cameraToTransformToolVector[axis1index] < -0.01f ? -1 : 1);
				s_PlanarHandlesOctant[axis2index] = (cameraToTransformToolVector[axis2index] < -0.01f ? -1 : 1);
			}
			var handleOffset = s_PlanarHandlesOctant;
			handleOffset[axisNormalIndex] = 0;
			handleOffset = rotation * (handleOffset * handleSize * 0.5f);

			var axis1 = Vector3.zero;
			var axis2 = Vector3.zero;
			var axisNormal = Vector3.zero;
			axis1[axis1index] = 1;
			axis2[axis2index] = 1;
			axisNormal[axisNormalIndex] = 1;
			axis1 = rotation * axis1;
			axis2 = rotation * axis2;
			axisNormal = rotation * axisNormal;
			
			s_Vertices[0] = position + handleOffset + ( axis1 +axis2) * handleSize * 0.5f;
			s_Vertices[1] = position + handleOffset + (-axis1 +axis2) * handleSize * 0.5f;
			s_Vertices[2] = position + handleOffset + (-axis1 -axis2) * handleSize * 0.5f;
			s_Vertices[3] = position + handleOffset + ( axis1 -axis2) * handleSize * 0.5f;

			var innerColor = Handles.color;
			var outerColor = Color.black;
			innerColor = new Color(innerColor.r, innerColor.g, innerColor.b, 0.1f);
			if (CSGHandles.disabled)
			{
				innerColor = Handles.secondaryColor;
				outerColor = innerColor;
			}
			Handles.DrawSolidRectangleWithOutline(s_Vertices, innerColor, outerColor);
			
			position = Slider2D(id, position, handleOffset, axisNormal, axis1, axis2, handleSize * 0.5f, RectangleHandleCap, snapping, snapVertices, initFunction, shutdownFunction);
			
			Handles.color = prevColor;

			return position;
		}
	}
}
