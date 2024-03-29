﻿using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using InternalRealtimeCSG;

namespace RealtimeCSG
{
	internal sealed class CylinderGenerator : ExtrudedGeneratorBase, IBrushGenerator
	{
		[NonSerialized] Vector3		worldPosition;
		[NonSerialized] Vector3		prevWorldPosition;
		[NonSerialized] bool		onLastPoint			= false;
		[NonSerialized] CSGPlane	geometryPlane		= new CSGPlane(0, 1, 0, 0);
		[NonSerialized] CSGPlane?	hoverDefaultPlane;
		
		[NonSerialized] CSGPlane[]	firstSnappedPlanes	= null;
		[NonSerialized] Vector3[]	firstSnappedEdges	= null;
		[NonSerialized] CSGBrush	firstSnappedBrush	= null;

		// cylinder specific
		[SerializeField] CylinderSettings		settings		= new CylinderSettings();

		protected override IShapeSettings ShapeSettings { get { return settings; } }

		public int		CircleSides
		{
			get { return settings.circleSides; }
			set { if (settings.circleSides == value) return; Undo.RecordObject(this, "Modified Circle Sides"); settings.circleSides = value; UpdateBaseShape(); }
		}

		public float     CircleOffset
		{ 
			get { return settings.circleOffset; }
			set { if (settings.circleOffset == value) return; Undo.RecordObject(this, "Modified Circle Offset"); settings.circleOffset = value; UpdateBaseShape(); }
		}

		public bool		CircleSmoothShading
		{
			get { return settings.circleSmoothShading; }
			set { if (settings.circleSmoothShading == value) return; Undo.RecordObject(this, "Modified Circle Smoothing"); settings.circleSmoothShading = value; UpdateBaseShape(); }
		}

		public bool		CircleSingleSurfaceEnds
		{
			get { return settings.circleSingleSurfaceEnds; }
			set { if (settings.circleSingleSurfaceEnds == value) return; Undo.RecordObject(this, "Modified Circle Single Surface Ends"); settings.circleSingleSurfaceEnds = value; UpdateBaseShape(); }
		}

		public bool		CircleDistanceToSide
		{
			get { return settings.circleDistanceToSide; }
			set { if (settings.circleDistanceToSide == value) return; Undo.RecordObject(this, "Modified Circle Distance To Side"); settings.circleDistanceToSide = value; UpdateBaseShape(); }
		}

		public bool		CircleRecenter
		{
			get { return settings.circleRecenter; }
			set { if (settings.circleRecenter == value) return; Undo.RecordObject(this, "Modified Circle Recenter"); settings.circleRecenter = value; UpdateBaseShape(); }
		}

		public float	RadiusA
		{
			get { return settings.RadiusA; }
			set { if (settings.RadiusA == value) return; Undo.RecordObject(this, "Modified Circle Radius A"); settings.RadiusA = value; UpdateBaseShape(); }
		}

		public float	RadiusB
		{
			get { return settings.RadiusB; }
			set { if (settings.RadiusB == value) return; Undo.RecordObject(this, "Modified Circle Radius B"); settings.RadiusB = value; UpdateBaseShape(); }
		}

		public override void PerformDeselectAll() { Cancel(); }
		public override void PerformDelete() { Cancel(); }

		public override void Init()
		{
			base.Init();
			Reset();
		}

		public override void Reset() 
		{
			settings.Reset();
			base.Reset();
			onLastPoint			= false;
			hoverDefaultPlane	= null;
			firstSnappedPlanes	= null;
			firstSnappedEdges	= null;
			firstSnappedBrush	= null;
		}

		public bool HotKeyReleased()
		{
			ResetVisuals();
			switch (editMode)
			{
				default:
				{
					return true;
				}
				case EditMode.CreateShape:
				{
					if (settings.vertices.Length == 1)
					{
						settings.AddPoint(worldPosition);
					}
					
					if (RadiusA <= MathConstants.EqualityEpsilon)
					{
						Cancel();
						return false;
					}
					StartEditMode();
					return true;
				}
				case EditMode.CreatePlane:
				{
					Cancel();
					return false;
				}
			}
		}

		protected override void MoveShape(Vector3 offset)
		{
			settings.MoveShape(offset);
		}

		void IBrushGenerator.OnDefaultMaterialModified()
		{
			if (generatedBrushes == null)
				return;
			
			var defaultMaterial = CSGSettings.DefaultMaterial;
			for (int i = 0; i < generatedBrushes.Length; i++)
			{
				var brush = generatedBrushes[i];
				if (!brush)
					continue;
				
				InternalCSGModelManager.UnregisterMaterials(parentModel, brush.Shape, false);

				var shape = brush.Shape;
				for (var m = 0; m < shape.Materials.Length; m++)
					shape.Materials[m] = defaultMaterial;
				
				InternalCSGModelManager.RegisterMaterials(parentModel, brush.Shape, true);
				
				if (brush.ControlMesh != null)
					brush.ControlMesh.SetDirty();
			}
			
			InternalCSGModelManager.UpdateMaterialCount(parentModel);
		}

		internal override bool UpdateBaseShape(bool registerUndo = true)
		{
			List<ShapePolygon> newPolygons = null;
			var outlineVertices = settings.GetVertices(buildPlane, worldPosition, base.gridTangent, base.gridBinormal, out shapeIsValid);
			if (shapeIsValid)
			{
				newPolygons = ShapePolygonUtility.CreateCleanPolygonsFromVertices(outlineVertices, brushPosition, buildPlane);
				shapeIsValid = newPolygons != null;
			}
			//CSGBrushEditorManager.ResetMessage();
			
			if (shapeIsValid && newPolygons != null)
				UpdatePolygons(outlineVertices, newPolygons.ToArray());

			if (editMode != EditMode.EditShape &&
				editMode != EditMode.ExtrudeShape)
				return false;

			if (!shapeIsValid || !HaveExtrusion)
				return true;

			GenerateBrushesFromPolygons(inGridSpace: false);
			UpdateExtrudedShape();
			return true;
		}

		bool BuildPlaneIsReversed
		{
			get
			{
				var realPlane = geometryPlane;
				//if (!planeOnGeometry)
				//	realPlane = Grid.CurrentGridPlane;

				if (Vector3.Dot(realPlane.normal, buildPlane.normal) < 0)
					return true;
				return false;
			}
		}

		internal override bool StartExtrudeMode(bool showErrorMessage = true)
		{						
			// reverse buildPlane if it's different
			if (BuildPlaneIsReversed)
			{
				buildPlane = buildPlane.Negated();
			}
			
			var outlineVertices	= settings.GetVertices(buildPlane, worldPosition, base.gridTangent, base.gridBinormal, out shapeIsValid);
			if (!shapeIsValid)
			{
				shapeIsValid = false;
				ClearPolygons();
				//if (showErrorMessage)
				//	CSGBrushEditorManager.ShowMessage("Could not create brush from given 2D shape");
				HideGenerateBrushes();
				return false;
			}
			var newPolygons		= ShapePolygonUtility.CreateCleanPolygonsFromVertices(outlineVertices, brushPosition, buildPlane);
			if (newPolygons == null)
			{
				shapeIsValid = false;
				ClearPolygons();
				//if (showErrorMessage)
				//	CSGBrushEditorManager.ShowMessage("Could not create brush from given 2D shape");
				HideGenerateBrushes();
				return false;
			}
			shapeIsValid = true;
			//CSGBrushEditorManager.ResetMessage();
			UpdatePolygons(outlineVertices, newPolygons.ToArray());
			GenerateBrushesFromPolygons();
			return true;
		}
		
		internal override bool CreateControlMeshForBrushIndex(CSGModel parentModel, CSGBrush brush, ShapePolygon polygon, Matrix4x4 localToWorld, float height, out ControlMesh newControlMesh, out Shape newShape)
		{
			bool smooth				= settings.circleSmoothShading;
			bool singleSurfaceEnds	= settings.circleSingleSurfaceEnds;
			var direction = haveForcedDirection ? forcedDirection : buildPlane.normal;
			if (!ShapePolygonUtility.GenerateControlMeshFromVertices(polygon,
																	 localToWorld,
																	 GeometryUtility.RotatePointIntoPlaneSpace(buildPlane, direction),
																	 height,

																	 null,
																	 new TexGen(), 
																			   
																	 smooth, 
																	 singleSurfaceEnds,
																	 out newControlMesh,
																	 out newShape))
			{
				return false;
			}
			
			if (brush.Shape != null)
				InternalCSGModelManager.UnregisterMaterials(parentModel, brush.Shape, false);
			brush.Shape = newShape;
			brush.ControlMesh = newControlMesh;
			InternalCSGModelManager.ValidateBrush(brush, true);
			InternalCSGModelManager.RegisterMaterials(parentModel, brush.Shape, true);
			ControlMeshUtility.RebuildShape(brush);
			
			var vertices = polygon.Vertices;
			float circumference = 0.0f;
			for (int j = vertices.Length - 1, i = 0; i < vertices.Length; j = i, i++)
			{
				circumference += (vertices[j] - vertices[i]).magnitude;
			}

			var shape = brush.Shape;

			float desiredTextureLength = Mathf.Round(circumference);
			float scalar = desiredTextureLength / circumference;

			shape.TexGens[0].Scale.x = scalar;
			shape.TexGens[0].Scale.y = shape.TexGens[0].Scale.y;
			shape.TexGens[0].Translation.x = 0;

			var count = vertices.Length;
			if (!singleSurfaceEnds)
			{
				GeometryUtility.ContinueTexGenFromSurfaceToSurface(brush, 0, count);
				GeometryUtility.ContinueTexGenFromSurfaceToSurface(brush, 0, count + count);
				for (int j = 0, i = 1; i < count; j = i, i++)
				{
					GeometryUtility.ContinueTexGenFromSurfaceToSurface(brush, j, i);
					GeometryUtility.ContinueTexGenFromSurfaceToSurface(brush, i, i + count);
					GeometryUtility.ContinueTexGenFromSurfaceToSurface(brush, i, i + count + count);
				}
			} else
			{
				for (int j = 0, i = 1; i < count; j = i, i++)
				{
					GeometryUtility.ContinueTexGenFromSurfaceToSurface(brush, j, i);
				}
			}

			return true;
		}


		void PaintRadiusMessage()
		{
			if (settings.vertices == null || settings.vertices.Length == 0)
				return;

			var endPosition = settings.vertices.Length == 1 ? worldPosition : settings.vertices[1];
			var centerPoint	= settings.vertices[0];
			var delta		= (endPosition - centerPoint).normalized;

			PaintUtility.DrawLength("radius A: ", HandleUtility.GetHandleSize(centerPoint), Matrix4x4.identity, Vector3.Cross(buildPlane.normal, delta), centerPoint, endPosition, Color.white);
			/*
			PaintUtility.DrawDottedLine(endPosition, settings.vertices[0], ColorSettings.BoundsEdgeHover, 4.0f);


			Vector3 radiusPoint = endPosition;
			float radius = (endPosition - centerPoint).magnitude;
			if (float.IsNaN(radius) || float.IsInfinity(radius))
				radiusPoint = centerPoint;

			var edgeCenter = (centerPoint + radiusPoint) * 0.5f;
			
			var textCenter2DA = HandleUtility.WorldToGUIPoint(centerPoint);
			var textCenter2DB = HandleUtility.WorldToGUIPoint(radiusPoint);
			var textCenter2DC = HandleUtility.WorldToGUIPoint(edgeCenter);
			var normal2D = (textCenter2DB - textCenter2DA).normalized;
			var temp = normal2D;
			normal2D.x = -temp.y;
			normal2D.y = temp.x;

			var textCenter2D = textCenter2DC;
			textCenter2D += normal2D * (hover_text_distance * 2);

			var textCenterRay = HandleUtility.GUIPointToWorldRay(textCenter2D);
			var textCenter = textCenterRay.origin + textCenterRay.direction * ((Camera.current.farClipPlane + Camera.current.nearClipPlane) * 0.5f);
			
			PaintUtility.DrawLine(edgeCenter, textCenter, Color.black);
			PaintUtility.DrawDottedLine(edgeCenter, textCenter, ColorSettings.SnappedEdges);
			
			PaintUtility.DrawScreenText(textCenter2D,
				"radius " + Units.ToRoundedDistanceString(radius));*/
		}

		void PaintSquare()
		{
			var wireframeColor = ColorSettings.BoundsOutlines;

			if (HaveHeight)
			{
				var camera = Camera.current;
				
				var localBounds = GetShapeBounds(toGridQuaternion);

				var volume = new Vector3[8];
				BoundsUtilities.GetBoundsVertices(localBounds, volume);

				PaintUtility.DrawDottedLines(Matrix4x4.TRS(Vector3.zero, fromGridQuaternion, Vector3.one), volume, BoundsUtilities.AABBLineIndices, wireframeColor, 4.0f);

				PaintUtility.RenderBoundsSizes(toGridQuaternion, fromGridQuaternion, camera, volume, Color.white, Color.white, Color.white, true, true, true);
			}
		}

		void PaintCircle(int id)
		{
			var temp		= Handles.color;
			var origMatrix	= Handles.matrix;
					
			Handles.matrix = MathConstants.identityMatrix;
			var rotation = Camera.current.transform.rotation;

			bool isValid;
			var realVertices = settings.GetVertices(buildPlane, worldPosition, gridTangent, gridBinormal, out isValid);
			if (editMode == EditMode.EditShape)
				shapeIsValid = isValid;
			if (realVertices != null && realVertices.Length >= 3)
			{
				var wireframeColor = ColorSettings.WireframeOutline;
				if (!shapeIsValid || !isValid)
					wireframeColor = Color.red;
				for (int i = 1; i < realVertices.Length; i++)
				{
					PaintUtility.DrawLine(realVertices[i - 1], realVertices[i], ToolConstants.oldLineScale, wireframeColor);
					PaintUtility.DrawDottedLine(realVertices[i - 1], realVertices[i], wireframeColor, 4.0f);
				}

				PaintUtility.DrawLine(realVertices[realVertices.Length - 1], realVertices[0], ToolConstants.oldLineScale, wireframeColor);
				PaintUtility.DrawDottedLine(realVertices[realVertices.Length - 1], realVertices[0], wireframeColor, 4.0f);
				
				if (realVertices.Length >= 3)
				{
					var color = ColorSettings.ShapeDrawingFill;
					PaintUtility.DrawPolygon(MathConstants.identityMatrix, realVertices, color);
				}

				PaintSquare();
			}



			if (settings.vertices != null && settings.vertices.Length > 0)
			{
				Handles.color = ColorSettings.PointInnerStateColor[0];
				for (int i = 0; i < settings.vertices.Length; i++)
				{
					float handleSize = GUIStyleUtility.GetHandleSize(settings.vertices[i]);
					float scaledHandleSize = handleSize * ToolConstants.handleScale;
					PaintUtility.SquareDotCap(id, settings.vertices[i], rotation, scaledHandleSize);
				}
				PaintRadiusMessage();
			}
						
			Handles.color = ColorSettings.PointInnerStateColor[3];
			{
				float handleSize = GUIStyleUtility.GetHandleSize(worldPosition);
				float scaledHandleSize = handleSize * ToolConstants.handleScale;
				PaintUtility.SquareDotCap(id, worldPosition, rotation, scaledHandleSize);
			}

			Handles.matrix = origMatrix;
			Handles.color = temp;
		}

		protected override void CreateControlIDs()
		{
			base.CreateControlIDs();

			if (settings.vertices.Length > 0)
			{
				if (settings.vertexIDs == null ||
					settings.vertexIDs.Length != settings.vertices.Length)
					settings.vertexIDs = new int[settings.vertices.Length];
				for (int i = 0; i < settings.vertices.Length; i++)
				{
					settings.vertexIDs[i] = GUIUtility.GetControlID(ShapeBuilderPointHash, FocusType.Passive);
				}
			}			
		}
		
		void CreateSnappedPlanes()
		{
			firstSnappedPlanes = new CSGPlane[firstSnappedEdges.Length / 2];

			for (int i = 0; i < firstSnappedEdges.Length; i += 2)
			{
				var point0 = firstSnappedEdges[i + 0];
				var point1 = firstSnappedEdges[i + 1];

				var binormal = (point1 - point0).normalized;
				var tangent  = buildPlane.normal;
				var normal	 = Vector3.Cross(binormal, tangent);

				var worldPlane	= new CSGPlane(normal, point0);
				// note, we use 'inverse' of the worldToLocalMatrix because to transform a plane we'd need to do an inverse, 
				// and using the already inversed matrix we don't need to do a costly inverse.
				var localPlane		= GeometryUtility.InverseTransformPlane(firstSnappedBrush.transform.localToWorldMatrix, worldPlane);
				var	vertices		= firstSnappedBrush.ControlMesh.Vertices;
				var planeIsInversed	= false;
				for (int v = 0; v < vertices.Length; v++)
				{
					if (localPlane.Distance(vertices[v]) > MathConstants.DistanceEpsilon)
					{
						planeIsInversed = true;
						break;
					}
				}
				if (planeIsInversed)
					firstSnappedPlanes[i / 2] = worldPlane.Negated();
				else
					firstSnappedPlanes[i / 2] = worldPlane;
			}
		}
		
		protected override void HandleCreateShapeEvents(Rect sceneRect)
		{/*
			if (settings.vertices.Length < 2)
			{
				if (editMode == EditMode.Extrude2DShape ||
					editMode == EditMode.EditVertices)
					editMode = EditMode.CreatePlane;
			}
			*/
			bool		pointOnEdge			= false;
			bool		havePlane			= false;
			bool		vertexOnGeometry	= false;
			CSGBrush	vertexOnBrush		= null;
			
			CSGPlane	hoverBuildPlane		= buildPlane;
			var sceneView = (SceneView.currentDrawingSceneView != null) ? SceneView.currentDrawingSceneView : SceneView.lastActiveSceneView;
			var camera = sceneView.camera;
			if (camera != null &&
				camera.pixelRect.Contains(Event.current.mousePosition))
			{
				if (!hoverDefaultPlane.HasValue ||
					settings.vertices.Length == 0)
				{
					bool forceGrid = Grid.ForceGrid;
					Grid.ForceGrid = false;
					hoverDefaultPlane = Grid.CurrentGridPlane;
					Grid.ForceGrid = forceGrid;
					firstSnappedEdges = null;
					firstSnappedBrush = null;
					firstSnappedPlanes = null;
					base.geometryModel = null;
				}
				if (editMode == EditMode.CreatePlane)
				{
					BrushIntersection intersection;
					if (!camera.orthographic && !havePlane &&
						SceneQueryUtility.FindWorldIntersection(Event.current.mousePosition, out intersection, MathConstants.GrowBrushFactor))
					{
						worldPosition = intersection.worldIntersection;
						if (intersection.surfaceInverted)
							hoverBuildPlane = intersection.plane.Negated();
						else
							hoverBuildPlane = intersection.plane;
						vertexOnBrush = intersection.brush;

						vertexOnGeometry = true;
					} else
					{
						hoverBuildPlane = hoverDefaultPlane.Value;
						vertexOnBrush = null;

						var mouseRay = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
						worldPosition = hoverBuildPlane.Intersection(mouseRay);
						vertexOnGeometry = false;
					}
					
					ResetVisuals();
					if (snapFunction != null)
					{
						CSGBrush snappedOnBrush;
						worldPosition = snapFunction(worldPosition, hoverBuildPlane, ref visualSnappedEdges, out snappedOnBrush, generatedBrushes);
						if (snappedOnBrush != null)
						{
							pointOnEdge = (visualSnappedEdges != null &&
									  visualSnappedEdges.Count > 0);
							vertexOnBrush = snappedOnBrush;
							vertexOnGeometry = true;
						}
					}

					if (settings.vertices.Length == 1)
					{
						if (hoverBuildPlane.normal != MathConstants.zeroVector3)
						{
							editMode = EditMode.CreateShape;
							havePlane = true;
						}
					} else
					if (settings.vertices.Length == 2)
					{
						onLastPoint = true;
					}
				} else
				{
					var mouseRay = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
					worldPosition = hoverBuildPlane.Intersection(mouseRay);

					ResetVisuals();
					if (snapFunction != null)
					{
						CSGBrush snappedOnBrush;
						worldPosition = snapFunction(worldPosition, hoverBuildPlane, ref visualSnappedEdges, out snappedOnBrush, generatedBrushes);
						if (snappedOnBrush != null)
						{
							pointOnEdge = (visualSnappedEdges != null &&
											visualSnappedEdges.Count > 0);
							vertexOnBrush = snappedOnBrush;
						}
					}
				}

				if (geometryModel == null && vertexOnBrush != null)
				{
					var brush_cache = InternalCSGModelManager.GetBrushCache(vertexOnBrush);
					if (brush_cache != null && brush_cache.childData != null && brush_cache.childData.Model)
						geometryModel = brush_cache.childData.Model;
				}

				if (worldPosition != prevWorldPosition)
				{
					prevWorldPosition = worldPosition;
					if (Event.current.type != EventType.Repaint)
						SceneView.RepaintAll();
				}
				
				visualSnappedGrid = Grid.FindAllGridEdgesThatTouchPoint(worldPosition);
				visualSnappedBrush = vertexOnBrush;
			}
			
			Grid.SetForcedGrid(hoverBuildPlane);
			

			if (!SceneTools.IsDraggingObjectInScene &&
				Event.current.type == EventType.Repaint)
			{
				PaintSnapVisualisation();
				PaintCircle(base.shapeId);
			}
			

			var type = Event.current.GetTypeForControl(base.shapeId);
			switch (type)
			{
				case EventType.layout:
				{
					return;
				}

				case EventType.ValidateCommand:
				case EventType.keyDown:
				{
					if (GUIUtility.hotControl == base.shapeId)
					{
						if (Keys.PerformActionKey.IsKeyPressed() ||
							Keys.DeleteSelectionKey.IsKeyPressed() ||
							Keys.CancelActionKey.IsKeyPressed())
						{
							Event.current.Use();
						}
					}
					return;
				}
				case EventType.KeyUp:
				{
					if (GUIUtility.hotControl == base.shapeId)
					{
						if (Keys.CylinderBuilderMode.IsKeyPressed() ||
							Keys.PerformActionKey.IsKeyPressed())
						{
							HotKeyReleased(); 
							Event.current.Use();
							return;
						}
						if (Keys.DeleteSelectionKey.IsKeyPressed() ||
							Keys.CancelActionKey.IsKeyPressed())
						{
							Cancel();
							Event.current.Use();
							return;
						}
					}
					return;
				}

				case EventType.MouseDown:
				{
					if (!sceneRect.Contains(Event.current.mousePosition))
						break;
					if (Tools.viewTool != ViewTool.None && Tools.viewTool != ViewTool.Pan)
						return;
					if ((GUIUtility.hotControl != 0 && GUIUtility.hotControl != shapeEditId && GUIUtility.hotControl != base.shapeId) ||
						Event.current.button != 0)
						return;
					
					Event.current.Use();
					if (settings.vertices.Length == 0)
					{
						if ((GUIUtility.hotControl == 0 ||
							GUIUtility.hotControl == base.shapeEditId) && base.shapeId != -1)
						{
							base.CalculateWorldSpaceTangents();
							GUIUtility.hotControl = base.shapeId;
							GUIUtility.keyboardControl = base.shapeId;
							EditorGUIUtility.editingTextField = false; 
						}
					}

					if (GUIUtility.hotControl == base.shapeId && settings.vertices.Length < 2)
					{
						if (!float.IsNaN(worldPosition.x) && !float.IsInfinity(worldPosition.x) &&
							!float.IsNaN(worldPosition.y) && !float.IsInfinity(worldPosition.y) &&
							!float.IsNaN(worldPosition.z) && !float.IsInfinity(worldPosition.z))
						{
							if (hoverBuildPlane.normal.sqrMagnitude != 0)
								buildPlane = hoverBuildPlane;
							CalculateWorldSpaceTangents();

							if (settings.vertices.Length == 0)
							{
								if (pointOnEdge)
								{
									firstSnappedEdges = visualSnappedEdges.ToArray();
									firstSnappedBrush = visualSnappedBrush;
									firstSnappedPlanes = null;
								} else
								{
									firstSnappedBrush = null;
									firstSnappedEdges = null;
									firstSnappedPlanes = null;
								}
								geometryPlane	= buildPlane;
								planeOnGeometry = vertexOnGeometry;
							} else
							{
								if (firstSnappedEdges != null)
								{
									if (firstSnappedPlanes == null)
										CreateSnappedPlanes();

									bool outside = true;
									for (int i = 0; i < firstSnappedPlanes.Length; i++)
									{
										if (firstSnappedPlanes[i].Distance(worldPosition) <= MathConstants.DistanceEpsilon)
										{
											outside = false;
											break;
										}
									}

									planeOnGeometry = !outside;
								}

								if (vertexOnGeometry)
								{
									var plane = hoverDefaultPlane.Value;
									var distance = plane.Distance(worldPosition);
									plane.d += distance;
									hoverDefaultPlane = plane;

									for (int i = 0; i < settings.vertices.Length; i++)
									{
										if (!settings.onGeometryVertices[i])
										{
											settings.vertices[i] = GeometryUtility.ProjectPointOnPlane(plane, settings.vertices[i]);
											settings.onGeometryVertices[i] = true;
										}
									}
								}
							}
							ArrayUtility.Add(ref settings.onGeometryVertices, vertexOnGeometry);
							settings.AddPoint(worldPosition);
							SceneView.RepaintAll();
							if (settings.vertices.Length == 2)
							{
								HotKeyReleased();
							}
						}
					}
					return;
				}
				case EventType.MouseDrag:
				{
					if (Tools.viewTool != ViewTool.None && Tools.viewTool != ViewTool.Pan)
						break;
					if (GUIUtility.hotControl == base.shapeId && Event.current.button == 0)
					{
						Event.current.Use();
					}
					return;
				}
				case EventType.MouseUp:
				{
					if (GUIUtility.hotControl != base.shapeId)
						return;
					if (Tools.viewTool != ViewTool.None && Tools.viewTool != ViewTool.Pan)
						return;
					if (Event.current.button == 0)
					{
						Event.current.Use(); 

						ResetVisuals();
						if (onLastPoint)
						{
							GUIUtility.hotControl = 0;
							GUIUtility.keyboardControl = 0;
							EditorGUIUtility.editingTextField = false;

							editMode = EditMode.CreateShape;
							HotKeyReleased();
						}
					}
					return;
				}
			}
		}



		internal override void BeginExtrusion()
		{
			settings.CopyBackupVertices();
		}

		internal override void EndExtrusion()
		{

		}

		protected override void HandleEditShapeEvents(Rect sceneRect)
		{
			if (!SceneTools.IsDraggingObjectInScene &&
				Event.current.type == EventType.Repaint)
			{			
				if (visualSnappedEdges != null)
					PaintUtility.DrawLines(visualSnappedEdges.ToArray(), ToolConstants.oldThickLineScale, ColorSettings.SnappedEdges);
				
				if (visualSnappedGrid != null)
				{
					var _origMatrix = Handles.matrix;
					Handles.matrix = MathConstants.identityMatrix;
					PaintUtility.DrawDottedLines(visualSnappedGrid.ToArray(), ColorSettings.SnappedEdges);
					Handles.matrix = _origMatrix;
				}
					
				if (visualSnappedBrush != null)
				{
					var brush_cache = InternalCSGModelManager.GetBrushCache(visualSnappedBrush);
					if (brush_cache != null &&
						brush_cache.compareTransformation != null &&
						brush_cache.childData != null &&
						brush_cache.childData.ModelTransform != null &&
						brush_cache.childData.ModelTransform)
					{
						var brush_translation = brush_cache.compareTransformation.modelLocalPosition + brush_cache.childData.ModelTransform.position;
						CSGRenderer.DrawOutlines(visualSnappedBrush.brushID, brush_translation, ColorSettings.SelectedOutlines, ColorSettings.SelectedOutlines, ColorSettings.SelectedOutlines, ColorSettings.SelectedOutlines);
					}						
				}
				
				var origMatrix	= Handles.matrix;
				Handles.matrix = MathConstants.identityMatrix;
				/*
				bool isValid;
				var realVertices		= settings.GetVertices(buildPlane, worldPosition, gridTangent, gridBinormal, out isValid);
				if (editMode == EditMode.EditShape)
					shapeIsValid = isValid;
				
				if (realVertices.Length > 0)*/
				{
					PaintSquare();
					PaintRadiusMessage();
				}
				
				Handles.matrix = origMatrix;
			}
			
			HandleHeightHandles(sceneRect, false);
			for (int i = 1; i < settings.vertices.Length; i++)
			{
				var id = settings.vertexIDs[i];
				var point_type = Event.current.GetTypeForControl(id);
				switch (point_type)
				{
					case EventType.Repaint:
					{
						if (SceneTools.IsDraggingObjectInScene)
							break;

						bool isSelected = id == GUIUtility.keyboardControl;
						var temp		= Handles.color;
						var origMatrix	= Handles.matrix;
					
						Handles.matrix = MathConstants.identityMatrix;
						var rotation = Camera.current.transform.rotation;


						if (isSelected)
						{
							Handles.color = ColorSettings.PointInnerStateColor[3];
						} else
						if (HandleUtility.nearestControl == id)
						{
							Handles.color = ColorSettings.PointInnerStateColor[1];
						} else						
						{
							Handles.color = ColorSettings.PointInnerStateColor[0];
						}

						float handleSize = GUIStyleUtility.GetHandleSize(settings.vertices[i]);
						float scaledHandleSize = handleSize * ToolConstants.handleScale;
						PaintUtility.SquareDotCap(id, settings.vertices[i], rotation, scaledHandleSize);
						
						Handles.matrix = origMatrix;
						Handles.color = temp;
						break;
					}

					case EventType.layout:
					{
						if ((Tools.viewTool != ViewTool.None && Tools.viewTool != ViewTool.Pan))
							break;

						var origMatrix = Handles.matrix;
						Handles.matrix = MathConstants.identityMatrix;
						float handleSize = GUIStyleUtility.GetHandleSize(settings.vertices[i]);
						float scaledHandleSize = handleSize * ToolConstants.handleScale;
						HandleUtility.AddControl(id, HandleUtility.DistanceToCircle(settings.vertices[i], scaledHandleSize));
						Handles.matrix = origMatrix;						
					
						break;
					}

					case EventType.ValidateCommand:
					case EventType.keyDown:
					{
						if (GUIUtility.hotControl == id)
						{
							if (Keys.CancelActionKey.IsKeyPressed())
							{
								Event.current.Use(); 
								break;
							}
						}
						break;
					}
					case EventType.keyUp:
					{
						if (GUIUtility.hotControl == id)
						{
							if (Keys.CancelActionKey.IsKeyPressed())
							{
								GUIUtility.hotControl = 0;
								GUIUtility.keyboardControl = 0;
								EditorGUIUtility.editingTextField = false;
								Event.current.Use(); 
								break;
							}
						}
						break;
					}

					case EventType.MouseDown:
					{
						if (!sceneRect.Contains(Event.current.mousePosition))
							break;
						if (Tools.viewTool != ViewTool.None && Tools.viewTool != ViewTool.Pan)
							break;
						if (GUIUtility.hotControl == 0 && HandleUtility.nearestControl == id && Event.current.button == 0)
						{
							GUIUtility.hotControl = id;
							GUIUtility.keyboardControl = id;
							EditorGUIUtility.editingTextField = false; 
							EditorGUIUtility.SetWantsMouseJumping(1);
							Event.current.Use(); 
							break;
						}
						break;
					}
					case EventType.MouseDrag:
					{
						if (Tools.viewTool != ViewTool.None && Tools.viewTool != ViewTool.Pan)
							break;
						if (GUIUtility.hotControl == id && Event.current.button == 0)
						{
							Undo.RecordObject(this, "Modify shape");

							var mouseRay		= HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
							var alignedPlane	= new CSGPlane(Grid.CurrentWorkGridPlane.normal, settings.vertices[0]);
							var worldPosition	= buildPlane.Project(alignedPlane.Intersection(mouseRay));
							if (float.IsInfinity(worldPosition.x) || float.IsNaN(worldPosition.x) ||
								float.IsInfinity(worldPosition.y) || float.IsNaN(worldPosition.y) ||
								float.IsInfinity(worldPosition.z) || float.IsNaN(worldPosition.z))
								worldPosition = settings.vertices[i];

							ResetVisuals();
							if (snapFunction != null)
							{
								CSGBrush snappedOnBrush;
								worldPosition = snapFunction(worldPosition, buildPlane, ref base.visualSnappedEdges, out snappedOnBrush, generatedBrushes);
							}
								
							base.visualSnappedGrid = Grid.FindAllGridEdgesThatTouchPoint(worldPosition);

							settings.vertices[i] = worldPosition;
							if (editMode == EditMode.ExtrudeShape)
							{
								StartExtrudeMode();
							}
							UpdateBaseShape();
							UpdateExtrudedShape();

							GUI.changed = true;
							Event.current.Use(); 
							break;
						}
						break;
					}
					case EventType.MouseUp:
					{
						if (GUIUtility.hotControl != id)
							break;
						if (Tools.viewTool != ViewTool.None && Tools.viewTool != ViewTool.Pan)
							break;
						if (Event.current.button == 0)
						{
							GUIUtility.hotControl = 0;
							GUIUtility.keyboardControl = 0;
							EditorGUIUtility.editingTextField = false;
							EditorGUIUtility.SetWantsMouseJumping(0);
							Event.current.Use(); 

							ResetVisuals();
							if (RadiusA == 0)
							{
								Cancel();
							}
							break;
						}
						break;
					}
				}
				
			}
		}
		
		public bool OnShowGUI(bool isSceneGUI)
		{
			return CylinderGeneratorGUI.OnShowGUI(this, isSceneGUI);
		}
	}
}
