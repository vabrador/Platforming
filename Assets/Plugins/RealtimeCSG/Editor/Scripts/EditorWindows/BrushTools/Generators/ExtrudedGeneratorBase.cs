﻿using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using InternalRealtimeCSG;
using System.Linq;

namespace RealtimeCSG
{
	[Serializable]
	internal struct ExtrusionPoint
	{
		[NonSerialized]
		public int ID;
		public Vector3 Position;
	}

	internal abstract class ExtrudedGeneratorBase : GeneratorBase//, IBrushGenerator
	{
		protected const float				handle_extension		= 1.2f;

		const CSGOperationType				invalidCSGOperationType = (CSGOperationType)99;

		[SerializeField] Vector3[]			outlineVertices;
		[SerializeField] ShapePolygon[]		polygons;
		[SerializeField] ShapeEdge[]		shapeEdges;
		[SerializeField] ExtrusionPoint[]	extrusionPoints			= new ExtrusionPoint[2];
		

		[SerializeField] internal bool		haveForcedDirection		= false;
		[SerializeField] internal Vector3	forcedDirection;

		
		[SerializeField] internal bool		smearTextures			= true;
		[NonSerialized] internal bool		forceDragHandle;

		[NonSerialized] CSGPlane			movePlane;
		[NonSerialized] Vector3				movePolygonDirection;
		
		[NonSerialized] bool				firstClick				= false;
        [NonSerialized] Vector3				dragPositionStart		= Vector3.zero;
        [NonSerialized] Vector3				heightHandleOffset		= Vector3.zero;
		[NonSerialized] Vector2				heightPosition			= Vector2.zero;
 
		
		public float	DefaultHeight
		{
			get { return RealtimeCSG.CSGSettings.DefaultShapeHeight; }
			set { RealtimeCSG.CSGSettings.DefaultShapeHeight = value; }
		}

		// used by GUI to decide to show Height
		public bool HaveHeight
		{
			get
			{
				return (editMode == EditMode.ExtrudeShape ||
						editMode == EditMode.EditShape) && extrusionPoints.Length <= 2;
			}
		}

		public bool HaveExtrusion
		{
			get
			{
				return (extrusionPoints.Length > 2) ||
					((extrusionPoints.Length == 2) && 
						(extrusionPoints[0].Position - extrusionPoints[1].Position).sqrMagnitude > MathConstants.EqualityEpsilonSqr);
			}
		}

		static float GetSegmentLength(Vector3 point1, Vector3 point2, Vector3 direction)
		{
			var height		= (point1 - point2).magnitude;
			var distance	= new CSGPlane(direction, point1).Distance(point2);
			if (float.IsInfinity(distance) || float.IsNaN(distance))
				distance = 1.0f;
			height *= Mathf.Sign(distance);
			return GeometryUtility.CleanLength(height);
		}

		public float	Height
		{
			get
			{
				if (!HaveHeight)
					return 0;
				
				var direction	= haveForcedDirection ? forcedDirection : buildPlane.normal;
				return GetSegmentLength(extrusionPoints[0].Position, extrusionPoints[1].Position, direction);
			}
			set
			{
				if (!HaveHeight)
					return;
				
				var direction	= haveForcedDirection ? forcedDirection : buildPlane.normal;
				var height		= GetSegmentLength(extrusionPoints[0].Position, extrusionPoints[1].Position, direction);
				var cleanValue	= GeometryUtility.CleanLength(value);
				if (height == cleanValue)
					return; 

				if (editMode == EditMode.EditShape)
				{
					if (value == 0)
						return;
					StartExtrudeMode();
				}
				
				Undo.RecordObject(this, "Modified Shape Height");
				extrusionPoints[1].Position = extrusionPoints[0].Position + (direction * cleanValue);
				UpdateBaseShape();
			}
		}

		public override void Reset() 
		{
			base.Reset();
			extrusionPoints = new ExtrusionPoint[2];
			extrusionPoints[0].Position = MathConstants.zeroVector3;
			extrusionPoints[1].Position = MathConstants.zeroVector3;
			haveForcedDirection = false;

			smearTextures = true;
			polygons	= null;
			shapeEdges	= null;
		}

		protected override bool StartEditMode()
		{
			if (!base.StartEditMode())
				return false;

			extrusionPoints = new ExtrusionPoint[2];
			extrusionPoints[1].Position =
			extrusionPoints[0].Position = brushPosition;
			return true;
		}

		protected override void CreateControlIDs()
		{
			for (int i=0;i<extrusionPoints.Length;i++)
				extrusionPoints[i].ID = GUIUtility.GetControlID(ShapeBuilderCenterHash, FocusType.Passive);
		}

		public override bool Commit()
		{
			isFinished  = true;
			CleanupGrid();
								
			return Commit(useDefaultHeight: true);
		}

		bool Commit(bool useDefaultHeight)
		{
			if (polygons == null || polygons.Length == 0)
			{
				Cancel();
				return false;
			}
			if (!HaveExtrusion)
			{
				if (editMode != EditMode.ExtrudeShape)
				{
					if (!StartExtrudeMode())
					{
						Cancel();
						return false;
					}
					UpdateBaseShape(registerUndo: false);
					extrusionPoints[1].Position = extrusionPoints[0].Position + (buildPlane.normal.normalized * DefaultHeight);
				}
			}
			if (!UpdateExtrudedShape())
			{
				Cancel();
				return false;
			}

			if (extrusionPoints.Length == 2)
				DefaultHeight = Height;

			EndCommit();
			return true;
		}

		protected void ClearPolygons()
		{
			polygons = null;
			shapeEdges = null;
		}

		protected void UpdatePolygons(Vector3[] vertices, ShapePolygon[] newPolygons)
		{
			this.outlineVertices = vertices.ToArray();
			if (vertices != null)
			{
				for (int i = 0; i < outlineVertices.Length; i++)
					outlineVertices[i] -= brushPosition;
			}
			this.polygons = newPolygons;
		}

		protected void GenerateBrushesFromPolygons(ShapeEdge[] shapeEdges = null, bool inGridSpace = true)
		{
			this.shapeEdges = shapeEdges;
			editMode = EditMode.ExtrudeShape;
            GenerateBrushObjects(polygons.Length * (extrusionPoints.Length - 1), inGridSpace);
		}

		protected void UpdateBrushOperation()
		{
			if (forceCurrentCSGOperationType == invalidCSGOperationType)
			{
				if (HaveHeight)
				{
					bool invertedWorld = parentModel != null && (parentModel.Settings & ModelSettingsFlags.InvertedWorld) == ModelSettingsFlags.InvertedWorld;
					if ((Height > 0 || !planeOnGeometry) ^ invertedWorld)
					{
						if (currentCSGOperationType != CSGOperationType.Additive)
						{
							currentCSGOperationType = CSGOperationType.Additive;
							UpdateOperationType(currentCSGOperationType);
						}
					} else
					{
						if (currentCSGOperationType != CSGOperationType.Subtractive)
						{
							currentCSGOperationType = CSGOperationType.Subtractive;
							UpdateOperationType(currentCSGOperationType);
						}
					}
				}
			}
		}

		protected bool UpdateExtrudedShape(bool registerUndo = true)
		{
			if (polygons == null || polygons.Length == 0)
				return false;

#if DEMO
			if (CSGBindings.BrushesAvailable() < polygons.Length)
			{
				Debug.Log("Demo brush limit hit (" + CSGBindings.BrushesAvailable() + " available, " + polygons.Length + " required), for the ability to create more brushes please purchase Realtime - CSG");
				return false;
			}
#endif
			
			bool failures = false;
			bool modifiedHierarchy = false;
			if (HaveExtrusion)
			{
				UpdateBrushOperation();

				if (generatedGameObjects != null && generatedGameObjects.Length > 0)
				{
					for(int i=generatedGameObjects.Length-1;i>=0;i--)
					{
						if (generatedGameObjects[i])
							continue;
						ArrayUtility.RemoveAt(ref generatedGameObjects, i);
					}
				}
				if (generatedGameObjects == null || generatedGameObjects.Length == 0)
				{
					Cancel();
					return false;
				}
				if (generatedGameObjects != null && generatedGameObjects.Length > 0)
				{
					if (registerUndo)
						Undo.RecordObjects(generatedGameObjects, "Extruded shape");

					int brushIndex = 0;
					for (int slice = 0; slice < extrusionPoints.Length - 1; slice++)
					{
						for (int p = 0; p < polygons.Length; p++)
						{
							var brush = generatedBrushes[brushIndex]; brushIndex++;

							if (!brush || !brush.gameObject)
								continue;
							
							var direction	= haveForcedDirection ? forcedDirection : buildPlane.normal;
							var distance	= new CSGPlane(direction, extrusionPoints[slice].Position).Distance(extrusionPoints[slice + 1].Position);
							if (float.IsInfinity(distance) || float.IsNaN(distance))
								distance = 1.0f;
							
							var poly2dToWorldMatrix = brush.transform.worldToLocalMatrix *
								Matrix4x4.TRS(extrusionPoints[slice].Position, Quaternion.FromToRotation(MathConstants.upVector3, buildPlane.normal),
																		 Vector3.one);// * parentModel.transform.localToWorldMatrix;


							ControlMesh newControlMesh;
							Shape newShape;
							if (!CreateControlMeshForBrushIndex(parentModel, brush, polygons[p], poly2dToWorldMatrix, distance, out newControlMesh, out newShape))
							{
								failures = true;
								if (brush.gameObject.activeSelf)
								{
									modifiedHierarchy = true;
									brush.gameObject.SetActive(false);
								}
								continue;
							}

							if (!brush.gameObject.activeSelf)
							{
								modifiedHierarchy = true;
								brush.gameObject.SetActive(true);
							}

							brush.ControlMesh.SetDirty();
							if (registerUndo)
								EditorUtility.SetDirty(brush);
						}
					}
				}
			} else
			{
				if (generatedGameObjects != null)
				{
					if (registerUndo)
						Undo.RecordObjects(generatedGameObjects, "Extruded brush");
					InternalCSGModelManager.skipRefresh = false;
					int brushIndex = 0;
					for (int slice = 0; slice < extrusionPoints.Length - 1; slice++)
					{
						for (int p = 0; p < polygons.Length; p++)
						{
							if (p >= generatedBrushes.Length)
								continue;
							var brush = generatedBrushes[brushIndex];
							brushIndex++;
							brush.ControlMesh.SetDirty();
							if (registerUndo)
								EditorUtility.SetDirty(brush);
						}
					}
					HideGenerateBrushes();
				}
			}

			try
			{
				InternalCSGModelManager.skipRefresh = true;
				if (registerUndo)
					EditorUtility.SetDirty(this);
				//CSGModelManager.External.ForceModelUpdate(parentModel.modelID); 
				InternalCSGModelManager.Refresh(forceHierarchyUpdate: modifiedHierarchy);
			}
			finally
			{
				InternalCSGModelManager.skipRefresh = false;
			}

			if (shapeEdges != null && smearTextures)
			{
				CSGBrush lastBrush = null;
				int lastSurfaceIndex = -1;
				for (int slice = 0; slice < extrusionPoints.Length - 1; slice++)
				{
					for (int se = 0; se < shapeEdges.Length; se++)
					{
						var brushIndex		= shapeEdges[se].PolygonIndex + (slice * shapeEdges.Length);
						var surfaceIndex	= shapeEdges[se].EdgeIndex;
					
						if (brushIndex < 0 ||
							brushIndex >= generatedBrushes.Length ||
							surfaceIndex == -1)
							continue;
					
						var brush = generatedBrushes[brushIndex];
						if (brush && brush.brushID != -1)
						{
							if (lastBrush && lastBrush.brushID != -1)
							{
								SurfaceUtility.CopyLastMaterial(brush, surfaceIndex, false,
																lastBrush, lastSurfaceIndex, false,
																registerUndo = false);
							} else
							{
								brush.Shape.TexGens[surfaceIndex].Translation = Vector3.zero;
								brush.Shape.TexGens[surfaceIndex].Scale = Vector2.one;
								brush.Shape.TexGens[surfaceIndex].RotationAngle = 0;
							}
							lastBrush = brush;
							lastSurfaceIndex = surfaceIndex;
						}
					}
				}
			}
			InternalCSGModelManager.RefreshMeshes();

			return !failures;
		}
		
		protected void PaintHeightMessage(Vector3 start, Vector3 end, Vector3 normal, float distance)
		{
			if (Mathf.Abs(distance) <= MathConstants.EqualityEpsilon)
				return;

			Vector3 middlePoint = (end + start) * 0.5f;
			
			var textCenter2DA = HandleUtility.WorldToGUIPoint(middlePoint + normal * 10.0f);
			var textCenter2DB = HandleUtility.WorldToGUIPoint(middlePoint);
			var normal2D = (textCenter2DB - textCenter2DA).normalized;

			var textCenter2D = textCenter2DB;
			textCenter2D += normal2D * (hover_text_distance * 2);
					
			var textCenterRay	= HandleUtility.GUIPointToWorldRay(textCenter2D);
			var textCenter		= textCenterRay.origin + textCenterRay.direction * ((Camera.current.farClipPlane + Camera.current.nearClipPlane) * 0.5f);
			
			PaintUtility.DrawLine(middlePoint, textCenter, Color.black);
			PaintUtility.DrawDottedLine(middlePoint, textCenter, ColorSettings.SnappedEdges);

			PaintUtility.DrawScreenText(textCenter2D, "Y: " + Units.ToRoundedDistanceString(Mathf.Abs(distance)));
		}

		
		public override AABB GetShapeBounds()
		{
			return GetShapeBounds(Quaternion.identity);
		}


		public AABB GetShapeBounds(Quaternion rotation)
		{
			var bounds = ShapeSettings.CalculateBounds(rotation, gridTangent, gridBinormal);
			var direction = haveForcedDirection ? forcedDirection : buildPlane.normal;
			if (editMode == EditMode.ExtrudeShape)
				bounds.Extrude(rotation * (direction * Height));
			return bounds;
		}

		Vector3 GetHeightHandlePosition(Vector3 point)
		{
			var mouseRay = HandleUtility.GUIPointToWorldRay(heightPosition);

			var alignedPlane = new CSGPlane(Camera.current.transform.forward, point);
			var planePosition = alignedPlane.Intersection(mouseRay);// buildPlane.Project() - grabOffset;
			var worldPosition = GeometryUtility.ProjectPointOnInfiniteLine(planePosition, brushPosition, movePolygonDirection);
			return worldPosition;
		}

		public bool IsMouseOverShapePolygons(Matrix4x4 matrix)
		{
			var inverseMatrix = matrix.inverse;
			var mouseRay = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
			mouseRay.origin = inverseMatrix.MultiplyPoint(mouseRay.origin);
			mouseRay.direction = inverseMatrix.MultiplyVector(mouseRay.direction);

			if (polygons == null)
				return false;
			
			for (int i = 0; i < polygons.Length; i++)
			{
				if (ShapePolygonUtility.IntersectsWithShapePolygon2D(polygons[i], mouseRay))
					return true;
			}
			return false;
		}

		protected void GrabHeightHandle(int index, bool ignoreFirstMouseUp = false)
		{	
			var camera = SceneView.currentDrawingSceneView.camera;
			if (camera == null)
				return;		
			
			firstClick = ignoreFirstMouseUp;
			editMode = EditMode.ExtrudeShape;
			GUIUtility.hotControl		= extrusionPoints[index].ID;
			GUIUtility.keyboardControl	= extrusionPoints[index].ID;
			EditorGUIUtility.editingTextField = false; 
			EditorGUIUtility.SetWantsMouseJumping(1);
			
			var surfaceDirection	= buildPlane.normal;
			var closestAxisForward	= GeometryUtility.SnapToClosestAxis(-camera.transform.forward);
			var closestAxisArrow	= GeometryUtility.SnapToClosestAxis(surfaceDirection);
			Vector3 tangent, normal;
			float dot = Mathf.Abs(Vector3.Dot(closestAxisForward, closestAxisArrow));
			if (dot != 1)
			{
				Vector3 v1, v2;
				if (closestAxisForward.x == 0 && closestAxisForward.y == 0)
				{
					v1 = new Vector3(1, 0, 0);
					v2 = new Vector3(0, 1, 0);
				} else
				if (closestAxisForward.x == 0 && closestAxisForward.z == 0)
				{
					v1 = new Vector3(1, 0, 0);
					v2 = new Vector3(0, 0, 1);
				} else
				//if (closestAxisForward.y == 0 && closestAxisForward.z == 0)
				{
					v1 = new Vector3(0, 1, 0);
					v2 = new Vector3(0, 0, 1);
				}

				var backward = -camera.transform.forward;
				float dot1 = Vector3.Dot(backward, v1);
				float dot2 = Vector3.Dot(backward, v2);
				if (dot1 < dot2)
				{
					tangent = v1;
				} else
				{
					tangent = v2;
				}
			} else
			{
				tangent = GeometryUtility.SnapToClosestAxis(Vector3.Cross(surfaceDirection, -camera.transform.forward));
			}

			normal = Vector3.Cross(surfaceDirection, tangent);

			if (camera.orthographic)
			{
				normal = -camera.transform.forward;
			}

			if (normal == MathConstants.zeroVector3)
			{
				normal = GeometryUtility.SnapToClosestAxis(-camera.transform.forward);
			}

			movePlane = new CSGPlane(normal, extrusionPoints[index].Position);
			if (!camera.orthographic && Mathf.Abs(movePlane.Distance(camera.transform.position)) < 2.0f)
			{
				var new_tangent = Vector3.Cross(normal, closestAxisForward);
				if (new_tangent != MathConstants.zeroVector3)
				{
					tangent = new_tangent;
					normal = Vector3.Cross(surfaceDirection, tangent);
					movePlane = new CSGPlane(normal, extrusionPoints[index].Position);
				}
			}
			
			movePolygonDirection = haveForcedDirection ? forcedDirection : buildPlane.normal;

			if (!isFinished)
			{
				Grid.SetForcedGrid(movePlane);
			}

			var plane = new CSGPlane(buildPlane.normal, extrusionPoints[index].Position);
			heightPosition = Event.current.mousePosition;
            
			heightHandleOffset = (plane.Distance(GetHeightHandlePosition(extrusionPoints[index].Position)) * movePolygonDirection);

			if (float.IsInfinity(heightHandleOffset.x) || float.IsNaN(heightHandleOffset.x) ||
				float.IsInfinity(heightHandleOffset.y) || float.IsNaN(heightHandleOffset.y) ||
				float.IsInfinity(heightHandleOffset.z) || float.IsNaN(heightHandleOffset.z))
				heightHandleOffset = Vector3.zero;
			
		}

		protected void CenterExtrusionPoints(CSGPlane buildPlane)
		{
			if (!HaveHeight)
				return;

			var center = GetShapeBounds().Center;
			brushPosition = buildPlane.Project(center);
			var height0 = buildPlane.Distance(extrusionPoints[0].Position);
			var height1 = buildPlane.Distance(extrusionPoints[1].Position);
			extrusionPoints[0].Position = brushPosition + (buildPlane.normal * height0);
			extrusionPoints[1].Position = brushPosition + (buildPlane.normal * height1);
		}

		protected void HandleHeightHandles(Rect sceneRect, bool showHeightValue)
		{
			for (int p = 0; p < extrusionPoints.Length; p++)
			{
			    var type = Event.current.GetTypeForControl(extrusionPoints[p].ID);
				switch (type)
				{
					case EventType.Repaint:
					{
						if (SceneTools.IsDraggingObjectInScene)
							break;

						bool isSelected = extrusionPoints[p].ID == GUIUtility.keyboardControl;
						var temp		= Handles.color;
						var origMatrix	= Handles.matrix;

						Handles.matrix = MathConstants.identityMatrix;
						var rotation = Camera.current.transform.rotation;
						

						var state = SelectState.None;
						if (isSelected)
						{
							state |= SelectState.Selected;
							state |= SelectState.Hovering;
						} else
						if (HandleUtility.nearestControl == extrusionPoints[p].ID)
						{
							state |= SelectState.Hovering;
						}


						if (polygons != null && outlineVertices.Length >= 3)
						{
							var wireframeColor		= ColorSettings.WireframeOutline;
							var topWireframeColor	= ColorSettings.WireframeOutline;

							if (!shapeIsValid)
								wireframeColor = Color.red;

							var surfaceColor = ColorSettings.ShapeDrawingFill;
							if (GUIUtility.hotControl == extrusionPoints[p].ID)
							{
								topWireframeColor = ColorSettings.BoundsEdgeHover;
								surfaceColor = ColorSettings.PolygonInnerStateColor[(int)(SelectState.Selected | SelectState.Hovering)];
								surfaceColor.a *= 0.5f;
							} else
							if (GUIUtility.hotControl == 0 && HandleUtility.nearestControl == extrusionPoints[p].ID)
							{
								topWireframeColor = ColorSettings.BoundsEdgeHover;
								surfaceColor = ColorSettings.PolygonInnerStateColor[(int)(SelectState.Selected)];
								surfaceColor.a *= 0.5f;
							}
							

							var poly2dToWorldMatrix = Matrix4x4.TRS(extrusionPoints[p].Position, Quaternion.FromToRotation(MathConstants.upVector3, buildPlane.normal), MathConstants.oneVector3);
							for (int i = 0; i < polygons.Length; i++)
								PaintUtility.DrawPolygon(poly2dToWorldMatrix, polygons[i].Vertices, surfaceColor);
							
							poly2dToWorldMatrix = Matrix4x4.TRS(extrusionPoints[p].Position, Quaternion.identity, MathConstants.oneVector3);
							for (int i = 1; i < outlineVertices.Length; i++)
							{
								PaintUtility.DrawLine(poly2dToWorldMatrix, outlineVertices[i - 1], outlineVertices[i], ToolConstants.oldLineScale, topWireframeColor);
								PaintUtility.DrawDottedLine(poly2dToWorldMatrix, outlineVertices[i - 1], outlineVertices[i], topWireframeColor, 4.0f);
							}
							PaintUtility.DrawLine(poly2dToWorldMatrix, outlineVertices[outlineVertices.Length - 1], outlineVertices[0], ToolConstants.oldLineScale, topWireframeColor);
							PaintUtility.DrawDottedLine(poly2dToWorldMatrix, outlineVertices[outlineVertices.Length - 1], outlineVertices[0], topWireframeColor, 4.0f);

							if (p > 0)
							{
								var prevOffset = extrusionPoints[p - 1].Position;
								var prevPoly2dToWorldMatrix = Matrix4x4.TRS(prevOffset, Quaternion.identity, MathConstants.oneVector3);
								for (int i = 0; i < outlineVertices.Length; i++)
								{
									var from = prevPoly2dToWorldMatrix.MultiplyPoint(outlineVertices[i]);
									var to   = poly2dToWorldMatrix.MultiplyPoint(outlineVertices[i]);
									PaintUtility.DrawLine(from, to, ToolConstants.oldLineScale, wireframeColor);
									PaintUtility.DrawDottedLine(from, to, wireframeColor, 4.0f);
								}
							}
						}

						var color = ColorSettings.PolygonInnerStateColor[(int)state];
						if (!shapeIsValid)
							color = Color.red;

						var handleSize			= GUIStyleUtility.GetHandleSize(extrusionPoints[p].Position);
						var scaledHandleSize	= handleSize * ToolConstants.handleScale;
						if (p > 0)
						{
							PaintUtility.DrawDottedLine(extrusionPoints[p - 1].Position, extrusionPoints[p].Position, color, 4.0f);
						}

						Handles.color = color;
						PaintUtility.SquareDotCap(extrusionPoints[p].ID, extrusionPoints[p].Position, rotation, scaledHandleSize);
						
						var direction = haveForcedDirection ? forcedDirection : buildPlane.normal;

						var distance = new CSGPlane(direction, extrusionPoints[p].Position).Distance(extrusionPoints[1 - p].Position);
						if (distance <= MathConstants.DistanceEpsilon)
							PaintUtility.DrawArrowCap(extrusionPoints[p].Position, direction, HandleUtility.GetHandleSize(extrusionPoints[p].Position));
						if (distance >  -MathConstants.DistanceEpsilon)
							PaintUtility.DrawArrowCap(extrusionPoints[p].Position, -direction, HandleUtility.GetHandleSize(extrusionPoints[p].Position));

						Handles.matrix = origMatrix;
						Handles.color = temp;

						if (p > 0 && showHeightValue)
						{							
							var length = GetSegmentLength(extrusionPoints[p].Position, extrusionPoints[p - 1].Position, direction);
							PaintHeightMessage(extrusionPoints[p-1].Position, extrusionPoints[p].Position, gridTangent, length);
						}
						break;
					}

					case EventType.layout:
					{
						if ((Tools.viewTool != ViewTool.None && Tools.viewTool != ViewTool.Pan))
							break;

						var poly2dToWorldMatrix = Matrix4x4.TRS(extrusionPoints[p].Position, Quaternion.FromToRotation(MathConstants.upVector3, buildPlane.normal), MathConstants.oneVector3);
						var forceOverHandle = IsMouseOverShapePolygons(poly2dToWorldMatrix);
						HandleUtility.AddControl(extrusionPoints[p].ID, forceOverHandle ? 3.0f : float.PositiveInfinity);
							
						var origMatrix = Handles.matrix;
						Handles.matrix = MathConstants.identityMatrix;
						float handleSize = GUIStyleUtility.GetHandleSize(extrusionPoints[p].Position);
						float scaledHandleSize = handleSize * ToolConstants.handleScale * handle_extension;
					    float distanceToCircle = HandleUtility.DistanceToCircle(extrusionPoints[p].Position, scaledHandleSize);

						HandleUtility.AddControl(extrusionPoints[p].ID, distanceToCircle);
						
						var direction = haveForcedDirection ? forcedDirection : buildPlane.normal;

						var distance = new CSGPlane(direction, extrusionPoints[p].Position).Distance(extrusionPoints[1 - p].Position);
						if (distance <= MathConstants.DistanceEpsilon)
							PaintUtility.AddArrowCapControl(extrusionPoints[p].ID, extrusionPoints[p].Position, direction, HandleUtility.GetHandleSize(extrusionPoints[p].Position));
						if (distance >  -MathConstants.DistanceEpsilon)
							PaintUtility.AddArrowCapControl(extrusionPoints[p].ID, extrusionPoints[p].Position, -direction, HandleUtility.GetHandleSize(extrusionPoints[p].Position));

						if (generatedGameObjects != null && generatedGameObjects.Length > 0)
						{
							for (int g = generatedGameObjects.Length - 1; g >= 0; g--)
							{
								if (generatedGameObjects[g])
									continue;
								ArrayUtility.RemoveAt(ref generatedGameObjects, g);
							}

							if (generatedGameObjects == null || generatedGameObjects.Length == 0)
							{
								Cancel();
							}
						}

						Handles.matrix = origMatrix;
						break;
					}

					case EventType.MouseDown:
					{
						if (!sceneRect.Contains(Event.current.mousePosition))
							break;
						if ((Tools.viewTool != ViewTool.None && Tools.viewTool != ViewTool.Pan) ||
							Event.current.modifiers != EventModifiers.None)
							break;
						if (GUIUtility.hotControl == 0 &&
							HandleUtility.nearestControl == extrusionPoints[p].ID && Event.current.button == 0)
						{
							if (editMode != EditMode.ExtrudeShape &&
								!StartExtrudeMode())
							{
								Cancel();
							} else
							{
								UpdateBaseShape(registerUndo: false);
							    dragPositionStart   = extrusionPoints[p].Position;
								GrabHeightHandle(p);
							    BeginExtrusion();
								Event.current.Use();
							}
						}
						break;
					}

					case EventType.MouseDrag:
					case EventType.MouseMove:
					{
						if (Tools.viewTool != ViewTool.None && Tools.viewTool != ViewTool.Pan)
							break;
						if (GUIUtility.hotControl == extrusionPoints[p].ID)// && Event.current.button == 0)
						{
							Undo.RecordObject(this, "Extrude shape");
							heightPosition += Event.current.delta;
							Vector3 worldPosition = GetHeightHandlePosition(extrusionPoints[p].Position) - heightHandleOffset;
							if (float.IsInfinity(worldPosition.x) || float.IsNaN(worldPosition.x) ||
								float.IsInfinity(worldPosition.y) || float.IsNaN(worldPosition.y) ||
								float.IsInfinity(worldPosition.z) || float.IsNaN(worldPosition.z))
								worldPosition = extrusionPoints[p].Position;

							ResetVisuals();
							if (raySnapFunction != null)
							{
								CSGBrush snappedOnBrush = null;
                                worldPosition = raySnapFunction(worldPosition, new Ray(brushPosition, movePolygonDirection), ref visualSnappedEdges, out snappedOnBrush);
								visualSnappedBrush = snappedOnBrush;
							}

							visualSnappedGrid = Grid.FindAllGridEdgesThatTouchPoint(worldPosition);

							extrusionPoints[p].Position = GeometryUtility.ProjectPointOnInfiniteLine(worldPosition, brushPosition, movePolygonDirection);

							if (p == 0)
							{
								MoveShape(extrusionPoints[0].Position - dragPositionStart);
								UpdateBaseShape();
							}
							
							UpdateBrushPosition();
							UpdateExtrudedShape();

							GUI.changed = true;
							Event.current.Use();
							break;
						}
						break;
					}
					case EventType.MouseUp:
					{
						forceDragHandle = false;
						if (GUIUtility.hotControl == extrusionPoints[p].ID &&
							Event.current.button == 0 &&
							(Tools.viewTool == ViewTool.None || Tools.viewTool == ViewTool.Pan))
                        {
                            EndExtrusion();
                            if (firstClick)
							{
								firstClick = false;
								break;
							}

							GUIUtility.hotControl = 0;
							GUIUtility.keyboardControl = 0;
							EditorGUIUtility.editingTextField = false;
							Event.current.Use();

							ResetVisuals();
							CleanupGrid();

							if (!HaveExtrusion)
							{
								RevertToEditVertices();
							}
							break;
						}
						break;
					}
				}
			}
			
			var shapeType = Event.current.GetTypeForControl(shapeId);
			HandleKeyboard(shapeType);
		}

		public override void HandleEvents(Rect sceneRect)
		{
			base.HandleEvents(sceneRect);

			//if (editMode != EditMode.EditShape)
			//	return;
				
			if (GUIUtility.hotControl == 0 && 
				Event.current.type == EventType.MouseUp &&
				Event.current.modifiers == EventModifiers.None &&
				!mouseIsDragging && Event.current.button == 0 &&
				(Tools.viewTool == ViewTool.None || Tools.viewTool == ViewTool.Pan))
			{
				ResetVisuals();
				Event.current.Use();
				if (editMode == EditMode.ExtrudeShape)
				{
					Commit(useDefaultHeight: false);
				} else
				{
					PerformDeselectAll();
				}
			}
		}

	    internal virtual void BeginExtrusion() {}
        internal virtual void EndExtrusion() {}
        internal abstract bool StartExtrudeMode(bool showErrorMessage = true);
		internal abstract bool CreateControlMeshForBrushIndex(CSGModel parentModel, CSGBrush brush, ShapePolygon polygon, Matrix4x4 localToWorld, float height, out ControlMesh newControlMesh, out Shape newShape);
	}
}
