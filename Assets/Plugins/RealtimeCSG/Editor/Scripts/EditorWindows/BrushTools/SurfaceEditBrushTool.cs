﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEditor;
using UnityEngine;
using InternalRealtimeCSG;
using UnityEngine.SceneManagement;

namespace RealtimeCSG
{
	[Serializable]
	internal sealed class SurfaceEditBrushTool : ScriptableObject, IBrushTool
	{
		static readonly int surfaceSelectPaintControlToolHash	= "surfaceSelectPaintControl".GetHashCode();
		static readonly int textureCopyDragControlToolHash		= "textureCopyDragControl".GetHashCode();
		static readonly int textureSurfaceControlToolHash		= "textureSurfaceControl".GetHashCode();
		//static readonly int surfaceEditBrushToolHash			= "surfaceEditBrushTool".GetHashCode();
		
		public bool UsesUnitySelection	{ get { return false; } }
		public bool IgnoreUnityRect		{ get { return true; } }

		public bool UndoRedoPerformed() { forceOutlineUpdate = true; return false; }

		
		enum EditMode
		{
			None,
			TranslateSurface,
			RotateTexture,
			ScaleTexture
		};

		enum DragMode
		{
			None,
			TextureCopy,
			SurfaceSelectAdd,
			SurfaceSelectRemove,
			SurfaceSelectToggle
		};


		[NonSerialized] EditMode		editMode	= EditMode.None;
		[NonSerialized] DragMode		dragMode	= DragMode.None;
		
		[NonSerialized] bool			isEnabled			= false;
		[NonSerialized] bool			hideTool			= false;
		[NonSerialized] bool			forceOutlineUpdate	= false;

		
		[NonSerialized] int				surfaceSelectPaintControl	= -1;		
		[NonSerialized] int				textureCopyDragControlID	= -1;

		[NonSerialized] int				hoverOnSurfaceIndex			= -1;
		[NonSerialized] int				hoverOnTarget				= -1;
		[NonSerialized] int				currentControl				= -1;
		[NonSerialized] int				hoverOnlyTarget				= -1;
		[NonSerialized] int				undoGroupIndex				= -1;
				
		[NonSerialized] RotationCircle	rotationCircle = new RotationCircle();
		
		[NonSerialized] Vector2			deltaMovement;
		[NonSerialized] Vector3			originalSurfacePoint;
		[NonSerialized] Vector3			snappingOffset;
		[NonSerialized] Vector3			snappingOffset2;
		
		[NonSerialized] SelectedBrushSurface	lastSelectedSurface;

		[SerializeField] CSGBrush[]				brushTargets		= new CSGBrush[0];
		[SerializeField] TexGenState[]			surfaceStates		= new TexGenState[0];
		[SerializeField] SelectedBrushSurface[]	selectedSurfaces	= new SelectedBrushSurface[0];
				
		#region Selection
		public void SetTargets(FilteredSelection filteredSelection)
		{
			if (brushTargets == null)
			{
				brushTargets	= new CSGBrush[0];
				surfaceStates	= new TexGenState[0];
				hoverOnTarget	= -1;
			}

			var foundBrushes = filteredSelection.GetAllContainedBrushes();
			for (int i = surfaceStates.Length - 1; i >= 0; i--)
			{
				if (foundBrushes.Contains(brushTargets[i]))
					continue;
				
				ArrayUtility.RemoveAt(ref surfaceStates, i);
				ArrayUtility.RemoveAt(ref brushTargets, i);
				if (hoverOnTarget > i)
					hoverOnTarget--;
				else if (hoverOnTarget == i)
				{
					hoverOnTarget = -1;
				}
			}
			
			foreach(var brush in foundBrushes)
			{
				var index = ArrayUtility.IndexOf(brushTargets, brush);
				if (index != -1)
					continue;
				
				ArrayUtility.Add(ref brushTargets, brush);
				var texGenState = new TexGenState(brush);
				texGenState.SelectAll();
				ArrayUtility.Add(ref surfaceStates, texGenState);
			}
			
			hideTool = filteredSelection.NodeTargets.Length > 0;
			if (isEnabled)
				Tools.hidden = hideTool;
			
			UpdateSelectedSurfaces();
			UpdateLayouts();
			forceOutlineUpdate = true;
		}

		public void SelectSurfaces(SelectedBrushSurface[] brushSurfaces, HashSet<GameObject> gameObjects, bool selectAllSurfacesOnBrushes)
		{
			if (brushTargets != null &&
				brushTargets.Length > 0)
			{
				for (int i = surfaceStates.Length - 1; i >= 0; i--)
				{
					if (gameObjects == null ||
						!gameObjects.Contains(brushTargets[i].gameObject))
					{
						ArrayUtility.RemoveAt(ref surfaceStates, i);
						ArrayUtility.RemoveAt(ref brushTargets, i);
						if		(hoverOnTarget > i) hoverOnTarget--;
						else if (hoverOnTarget == i) hoverOnTarget = -1;
						continue;
					}
					
					surfaceStates[i].DeselectAll();
				}
			} else
			{
				brushTargets  = new CSGBrush[0];
				surfaceStates = new TexGenState[0];
				hoverOnTarget	= -1;
			}
			
			if (brushSurfaces != null && 
				brushSurfaces.Length > 0)
			{
				for (int j = 0; j < brushSurfaces.Length; j++)
				{
					var index = ArrayUtility.IndexOf(brushTargets, brushSurfaces[j].brush);
					if (index != -1)
						continue;

					ArrayUtility.Add(ref brushTargets, brushSurfaces[j].brush);
					ArrayUtility.Add(ref surfaceStates, new TexGenState(brushSurfaces[j].brush));
				}

				for (int i = surfaceStates.Length - 1; i >= 0; i--)
				{
					var brushTarget = brushTargets[i];
					var surfaceState = surfaceStates[i];
					for (int j = 0; j < brushSurfaces.Length; j++)
					{
						if (brushTarget != brushSurfaces[j].brush)
							continue;

						if (selectAllSurfacesOnBrushes)
						{
							for (int k = 0; k < surfaceState.surfaceSelectState.Length; k++)
							{
								surfaceState.surfaceSelectState[k] = SelectState.Selected;
							}
						} else
						{
							var surfaceIndex = brushSurfaces[j].surfaceIndex;
							if (surfaceIndex >= 0)
							{
								surfaceState.surfaceSelectState[surfaceIndex] = SelectState.Selected;
							}
						}
					}
				}
				lastSelectedSurface	= brushSurfaces[0];
			} else
				lastSelectedSurface	= null;

			if (gameObjects == null)
				Selection.activeTransform = null;
			else
				Selection.objects = gameObjects.ToArray();
			
			UpdateSelectedSurfaces();
			UpdateLayouts();
			forceOutlineUpdate = true;
		}

		void UpdateLayouts()
		{
			for (int t = 0; t < brushTargets.Length; t++)
			{
				var surfaceState = surfaceStates[t];
				surfaceState.UpdateLayout(brushTargets[t]);
			}
		}

		
		GameObject[] GetSelectedGameObjects()
		{
			var gameObjects = new GameObject[brushTargets.Length];
			for (int i = 0; i < brushTargets.Length; i++)
			{
				if (surfaceStates[i].HaveSurfaceSelection)
					gameObjects[i] = brushTargets[i].gameObject;
			}
			return gameObjects;
		}
		
		void UpdateSelectedSurfaces()
		{
			if (surfaceStates == null ||
				surfaceStates.Length == 0)
			{
				selectedSurfaces = new SelectedBrushSurface[0];
				return;
			}
			
			var selected_surfaces = new List<SelectedBrushSurface>();
			for (int b = 0; b < surfaceStates.Length; b++)
			{
				var brush = brushTargets[b];
				surfaceStates[b].UpdateTexGenState(brush);
				for (int s = 0; s < surfaceStates[b].surfaceSelectState.Length; s++)
				{
					if ((surfaceStates[b].surfaceSelectState[s] & SelectState.Selected) == SelectState.Selected)
					{
						selected_surfaces.Add(new SelectedBrushSurface(brushTargets[b], s));
					}
				}
			}
			selectedSurfaces = selected_surfaces.ToArray();
			forceOutlineUpdate = true;
		}

		public SelectedBrushSurface[] GetSelectedSurfaces()
		{
			return selectedSurfaces;
		}
		
		bool IsSelected(BrushIntersection intersection)
		{
			if (intersection.gameObject == null ||
				intersection.surfaceIndex == -1)
				return false;

			var brush = intersection.gameObject.GetComponent<CSGBrush>();
			int brushIndex = ArrayUtility.IndexOf(brushTargets, brush);
			if (brushIndex == -1)
				return false;
			
			if ((surfaceStates[brushIndex].surfaceSelectState[intersection.surfaceIndex] & SelectState.Selected) == SelectState.Selected)
				return true;
			return false;
		}
					
		bool RemoveSurfaceSelection(BrushIntersection intersection)
		{
			var brush		= intersection.gameObject.GetComponent<CSGBrush>();
			int brushIndex	= ArrayUtility.IndexOf(brushTargets, brush);
			if (brushIndex == -1)
				return false;			
			
			Undo.RecordObject(this, "Deselect surface selection");
			surfaceStates[brushIndex].surfaceSelectState[intersection.surfaceIndex] &= ~SelectState.Selected;
			if (brushTargets == null || brushTargets.Length == 0)
				Selection.activeTransform = null;
			else
				Selection.objects = GetSelectedGameObjects();
						
			UpdateSelectedSurfaces();
			return true;
		}
		
		bool AddSurfaceSelection(BrushIntersection intersection)
		{			
			if (intersection.gameObject == null ||
				intersection.surfaceIndex == -1)
				return false;

			var brush = intersection.gameObject.GetComponent<CSGBrush>();

			int brushIndex = ArrayUtility.IndexOf(brushTargets, brush);
			if (brushIndex == -1)
			{
				ArrayUtility.Add(ref brushTargets, brush);
				TexGenState newState = new TexGenState(brush);
				ArrayUtility.Add(ref surfaceStates, newState);
				return false;
			}
			
			Undo.RecordObject(this, "Add surface selection");
			var surfaceState = surfaceStates[brushIndex];
			surfaceState.surfaceSelectState[intersection.surfaceIndex] |= SelectState.Selected;
			
			if (brushTargets == null || brushTargets.Length == 0)
				Selection.activeTransform = null;
			else
				Selection.objects = GetSelectedGameObjects();
						
			UpdateSelectedSurfaces();
			return true;
		}
		
		bool UpdateSelection(bool allowSubstraction = true)
		{
			if (hoverOnTarget == -1)
				return false;

			var surfaceState	= surfaceStates[hoverOnTarget];
			var selectionType	= SelectionUtility.GetEventSelectionType();

			if (!allowSubstraction)
			{
				if (selectionType == SelectionType.Toggle ||
					selectionType == SelectionType.Subtractive)
					selectionType = SelectionType.Additive;

				if (surfaceState.IsSurfaceSelected(hoverOnSurfaceIndex))
					selectionType = SelectionType.Additive;
			}

			if (selectionType == SelectionType.Replace ||
				selectionType == SelectionType.Additive)
			{
			//	if (surfaceState.IsSurfaceSelected(hoverOnSurfaceIndex))
			//		return false;
			}
			
			Undo.RecordObject(this, "Update selection");
			if (selectionType == SelectionType.Replace)
			{
				for (int t = 0; t < brushTargets.Length; t++)
					surfaceStates[t].DeselectAll();
			}

			bool repaint = false;
			for (int s = 0; s < surfaceState.surfaceSelectState.Length; s++)
			{
				bool modified = surfaceState.SelectSurface(s, selectionType);
				repaint = modified || repaint;
				if (modified && (surfaceState.surfaceSelectState[s] & SelectState.Selected) == SelectState.Selected)
				{
					lastSelectedSurface = new SelectedBrushSurface(brushTargets[hoverOnTarget], s);
				}
			}

			if (brushTargets == null || brushTargets.Length == 0)
				Selection.activeTransform = null;
			else
				Selection.objects = GetSelectedGameObjects();
			
			UpdateSelectedSurfaces();
			return repaint;
		}
				
		public bool DeselectAll()
		{
			Selection.activeTransform = null;
			return true;
		}
		#endregion

		public void OnEnableTool ()
		{
			isEnabled = true;
			Tools.hidden = hideTool;
			ResetTool();
			UpdateSelectedSurfaces();
			UpdateLayouts();
		}
		public void OnDisableTool() { isEnabled = false; Tools.hidden = false; ResetTool(); }

		void ResetTool()
		{
			outlineGeneration			= -1;
			hoverOnlyTarget				= -1;
			rotationCircle.Clear();
			mouseIsDown					= false;
			mouseIsDragging				= false;
			firstDrag					= false;
			firstMove					= false;
			dragMode					= DragMode.None;
			textureCopyDragControlID	= -1;
			surfaceSelectPaintControl	= -1;
			lastSelectedSurface			= null;
		}
		
		void CopyBackupTexgens()
		{
			for (int t = 0; t < brushTargets.Length; t++)
			{
				var targetBrush	= brushTargets[t];
				var targetShape	= targetBrush.Shape;

				surfaceStates[t].backupTexGens = new TexGen[targetShape.TexGens.Length];
				Array.Copy(targetShape.TexGens, surfaceStates[t].backupTexGens, targetShape.TexGens.Length);
			}
		}

		void RestoreBackupTexgens()
		{
			for (int t = 0; t < this.brushTargets.Length; t++)
			{
				var targetBrush = this.brushTargets[t];
				var targetShape = targetBrush.Shape;
				for (int s = 0; s < targetShape.Surfaces.Length; s++)
				{
					var texGenIndex = targetShape.Surfaces[s].TexGenIndex;
					if (this.surfaceStates.Length <= t)
					{
						Debug.Log("this.surfaceStates.Length <= t");
						continue;
					}
					if (this.surfaceStates[t].backupTexGens.Length <= texGenIndex)
					{
						Debug.Log("this.surfaceStates[t].backupTexGens.Length <= texGenIndex");
						continue;
					}
					if (targetShape.TexGens.Length <= texGenIndex)
					{
						Debug.Log("targetShape.TexGens.Length <= texGenIndex");
						continue;
					}
					targetShape.TexGens[texGenIndex] = this.surfaceStates[t].backupTexGens[texGenIndex];
				}
			}
		}
		
		EditMode SetHoverOn(EditMode editModeType, int target, int index = -1)
		{
			hoverOnTarget	= target;
			if (target == -1)
			{
				hoverOnSurfaceIndex = -1;
				return EditMode.None;
			}
			
			hoverOnSurfaceIndex = index;
			return editModeType;
		}
				
		MouseCursor currentCursor = MouseCursor.Arrow;
		void UpdateMouseCursor()
		{
			if (mouseIsDown)
				return;
			
			switch (SelectionUtility.GetEventSelectionType())
			{
				case SelectionType.Additive:	currentCursor = MouseCursor.ArrowPlus; break;
				case SelectionType.Subtractive: currentCursor = MouseCursor.ArrowMinus; break;
				case SelectionType.Toggle:		currentCursor = MouseCursor.Arrow; break;

				default:						currentCursor = MouseCursor.Arrow; break;
			}
		}

		bool UpdateGrid()
		{
			var brush			= brushTargets[hoverOnTarget];
			var brush_cache		= InternalCSGModelManager.GetBrushCache(brush);
			if (brush_cache == null ||
				brush_cache.childData == null ||
				brush_cache.childData.ModelTransform == null)
				return false;

			var modelTransform		= brush_cache.childData.ModelTransform;
			var modelTranslation	= modelTransform.position;
			var brush_shape			= brush.Shape;

			var hoverOnTexGenIndex	= brush_shape.Surfaces[hoverOnSurfaceIndex].TexGenIndex;

			var old_translation = brush_shape.TexGens[hoverOnTexGenIndex].Translation;
			brush_shape.TexGens[hoverOnTexGenIndex].Translation = MathConstants.zeroVector2;
			InternalCSGModelManager.External.SetBrushTexGen(brush.brushID, hoverOnTexGenIndex, ref brush_shape.TexGens[hoverOnTexGenIndex]);
			var zero_point			= SurfaceUtility.ConvertTextureToWorldCoord(brush.brushID, brush_shape, modelTranslation, hoverOnSurfaceIndex, MathConstants.zeroVector2);
			brush_shape.TexGens[hoverOnTexGenIndex].Translation = old_translation;
			InternalCSGModelManager.External.SetBrushTexGen(brush.brushID, hoverOnTexGenIndex, ref brush_shape.TexGens[hoverOnTexGenIndex]);

			var movePlane		= brush_shape.Surfaces[hoverOnSurfaceIndex].Plane;
			var brush_transform	= brush.GetComponent<Transform>();

			movePlane = GeometryUtility.InverseTransformPlane(brush_transform.worldToLocalMatrix, movePlane);
			movePlane = new CSGPlane(movePlane.normal, zero_point);
			
			Grid.SetForcedGrid(movePlane);
			return true;
		}

		bool DragTextureCopy()
		{				
			BrushIntersection intersection;
			if (SceneQueryUtility.FindWorldIntersection(Event.current.mousePosition, out intersection))
			{
				if (lastSelectedSurface != null && (lastSelectedSurface.brush != intersection.brush || lastSelectedSurface.surfaceIndex != intersection.surfaceIndex))
				{
					SurfaceUtility.CopyLastMaterial(intersection.brush, intersection.surfaceIndex, intersection.surfaceInverted,
													lastSelectedSurface.brush, lastSelectedSurface.surfaceIndex, lastSelectedSurface.surfaceInverted);
					InternalCSGModelManager.RefreshMeshes();
					lastSelectedSurface = null;
				}
				if (lastSelectedSurface == null)
					lastSelectedSurface = new SelectedBrushSurface(intersection.brush, intersection.surfaceIndex, intersection.surfaceInverted);
			}
			return true;
		}

		bool DragSurfaceSelect()
		{
			BrushIntersection intersection;
			if (SceneQueryUtility.FindWorldIntersection(Event.current.mousePosition, out intersection))
			{
				if (IsSelected(intersection))
				{
					if (dragMode == DragMode.SurfaceSelectToggle)
						dragMode = DragMode.SurfaceSelectRemove;

					if (dragMode == DragMode.SurfaceSelectRemove)
					{
						RemoveSurfaceSelection(intersection);
						SceneView.RepaintAll();
					}
				} else
				{
					if (dragMode == DragMode.SurfaceSelectToggle)
						dragMode = DragMode.SurfaceSelectAdd;

					if (dragMode == DragMode.SurfaceSelectAdd)
					{
						AddSurfaceSelection(intersection);
						SceneView.RepaintAll();
					}
				}
			}
				
			return true;
		}

		bool DragTranslateTextureCoordinates()
		{
			if (hoverOnTarget == -1 || hoverOnSurfaceIndex == -1)
			{
				//Debug.Log("hoverOnTarget == -1 || hoverOnSurfaceIndex == -1");
				return false;
			}

			var brush = brushTargets[hoverOnTarget];				
			var brush_cache = InternalCSGModelManager.GetBrushCache(brush);
			if (brush_cache == null ||
				brush_cache.childData == null ||
				brush_cache.childData.ModelTransform == null)
			{
				//Debug.Log("brush_cache == null");
				return false;
			}
			
			var modelTransform	 = brush_cache.childData.ModelTransform;
			var modelTranslation = modelTransform.position;
			
			var toggleSnapping		= (Event.current.modifiers & EventModifiers.Control) == EventModifiers.Control;

			if (firstMove)
			{
				UpdateSelection(allowSubstraction: false);
				CopyBackupTexgens();
				var surfaceState		= surfaceStates[hoverOnTarget];
				originalSurfacePoint	= surfaceState.rayIntersectionWorldPoint;
				snappingOffset2			= surfaceState.rayIntersectionWorldPoint - snappingOffset;
				firstMove = false;
			}
			
			var hoverOnShape		= this.brushTargets[this.hoverOnTarget].Shape;
			var hoverOnTexGenIndex	= hoverOnShape.Surfaces[this.hoverOnSurfaceIndex].TexGenIndex;
			var hoverSurfaceState	= this.surfaceStates[this.hoverOnTarget];
			var backupTexGen		= hoverSurfaceState.backupTexGens[hoverOnTexGenIndex];
			
			BrushIntersection brushIntersection;
			var selectedSurfaces = GetSelectedSurfaces();
			if (selectedSurfaces.Length == 0)
			{
				//Debug.Log("selectedSurfaces.Length == 0");
				return true;
			}

			if (!SceneQueryUtility.FindSurfaceIntersection(brush, modelTranslation, hoverOnSurfaceIndex, Event.current.mousePosition,
															out brushIntersection))
			{
				//Debug.Log("!CSGModelManager.FindSurfaceIntersection");
				return true;
			}
			
			var old_world_position = originalSurfacePoint - snappingOffset2;
			var new_world_position = brushIntersection.worldIntersection - snappingOffset2;
				
			// snap texture coordinates in world/local space
			new_world_position = GridUtility.FixPosition(new_world_position, modelTransform, old_world_position, toggleSnapping);

			using (new UndoGroup(selectedSurfaces, "Translating surface"))
			{
				RestoreBackupTexgens();
				if (SurfaceUtility.TranslateSurfaces(selectedSurfaces, modelTransform, old_world_position, new_world_position))
				{
					deltaMovement = hoverOnShape.TexGens[hoverOnTexGenIndex].Translation - backupTexGen.Translation;
					InternalCSGModelManager.Refresh();
					InternalCSGModelManager.UpdateMeshes();
					MeshInstanceManager.UpdateHelperSurfaceVisibility();
					SceneView.RepaintAll();
				}
			}
			return true;
		}

		bool DragRotateTextureCoordinates()
		{
			if (hoverOnTarget == -1 || hoverOnSurfaceIndex == -1)
				return false;

			var brush = brushTargets[hoverOnTarget];				
			var brush_cache = InternalCSGModelManager.GetBrushCache(brush);
			if (brush_cache == null ||
				brush_cache.childData == null ||
				brush_cache.childData.ModelTransform == null)
				return false;
			
			var modelTransform	 = brush_cache.childData.ModelTransform;
			var modelTranslation = modelTransform.position;
			
			var toggleSnapping		= (Event.current.modifiers & EventModifiers.Control) == EventModifiers.Control;

			if (firstMove)
			{
				UpdateSelection(allowSubstraction: false);
				CopyBackupTexgens();					
				firstMove = false;
			}
			
			var hoverOnShape		= this.brushTargets[this.hoverOnTarget].Shape;
			var hoverOnTexGenIndex	= hoverOnShape.Surfaces[this.hoverOnSurfaceIndex].TexGenIndex;
			var hoverSurfaceState	= this.surfaceStates[this.hoverOnTarget];
			var backupTexGen		= hoverSurfaceState.backupTexGens[hoverOnTexGenIndex];

			BrushIntersection brushIntersection;
			var selectedSurfaces = GetSelectedSurfaces();
			if (selectedSurfaces.Length == 0 ||
				!SceneQueryUtility.FindSurfaceIntersection(brush, modelTranslation, hoverOnSurfaceIndex, Event.current.mousePosition, 
															out brushIntersection) ||
				!rotationCircle.UpdateRadius(backupTexGen, brushIntersection.worldIntersection, toggleSnapping))
				return true;
				
			using (new UndoGroup(selectedSurfaces, "Rotating surface"))
			{
				RestoreBackupTexgens();
				SurfaceUtility.RotateSurfaces(selectedSurfaces, rotationCircle);
			}
			return true;
		}


		[NonSerialized] LineMeshManager		selectedVisibleLineMeshManager		= new LineMeshManager();
		[NonSerialized] LineMeshManager		selectedInvisibleLineMeshManager	= new LineMeshManager();

		[NonSerialized] LineMeshManager		hoverVisibleLineMeshManager			= new LineMeshManager();
		[NonSerialized] LineMeshManager		hoverInvisibleLineMeshManager		= new LineMeshManager();

		[NonSerialized] PolygonMeshManager	hoverVisiblePolygonMeshManager		= new PolygonMeshManager();
		[NonSerialized] PolygonMeshManager	selectedVisiblePolygonMeshManager	= new PolygonMeshManager();

		[NonSerialized] int outlineGeneration = -1;
		[NonSerialized] int prevHoverSurfaceIndex = -1;
		[NonSerialized] int prevHoverBrushIndex = -1;

		void OnDestroy()
		{
			selectedVisibleLineMeshManager		.Destroy();
			selectedInvisibleLineMeshManager	.Destroy();
			selectedVisiblePolygonMeshManager	.Destroy();

			hoverVisibleLineMeshManager			.Destroy();
			hoverInvisibleLineMeshManager		.Destroy();
			hoverVisiblePolygonMeshManager		.Destroy();
		}

		
		void DrawOutlines()
		{
			if (outlineGeneration != InternalCSGModelManager.MeshGeneration)
			{
				outlineGeneration = InternalCSGModelManager.MeshGeneration;
				forceOutlineUpdate = true;
			}


			if (forceOutlineUpdate)
			{
				var visibleInnerColor	= ColorSettings.PointInnerStateColor[(int)SelectState.Selected];
				var visibleOuterColor	= ColorSettings.SurfaceOuterStateColor[(int)SelectState.Selected];
				var visibleOuterline	= ColorSettings.MeshEdgeOutline;
				var invisibleInnerColor = ColorSettings.PointInnerStateColor[(int)SelectState.Selected];
				var invisibleOuterColor = ColorSettings.SurfaceOuterStateColor[(int)SelectState.Selected];
				var invisibleOuterline	= ColorSettings.MeshEdgeOutline;
				var surfaceColor		= ColorSettings.SurfaceTriangleStateColor[(int)SelectState.Selected];

				invisibleOuterline.a = 0.5f;
				visibleOuterline.a   = 0.5f;

				invisibleInnerColor.a = 1.0f;
				invisibleOuterColor.a = 1.0f;
				visibleOuterColor.a = 1.0f;
				visibleInnerColor.a = 1.0f;
				
				surfaceColor.a = 0.25f;

				var foundSelectedSurfaces = new List<SelectedBrushSurface>();
				var foundTranslations = new List<Vector3>();
				for (int t = 0; t < this.brushTargets.Length; t++)
				{
					var brush			= brushTargets[t];
					var surfaceState	= surfaceStates[t];

					var brush_cache = InternalCSGModelManager.GetBrushCache(brush);
					if (brush_cache == null ||
						!brush_cache.childData.ModelTransform)
						continue;
					
					var brush_translation	= brush_cache.compareTransformation.modelLocalPosition + brush_cache.childData.ModelTransform.position;
					var brush_shape			= brush.Shape;

					for (int s = 0; s < surfaceState.surfaceSelectState.Length; s++)
					{
						if (s < 0 || s >= brush_shape.Surfaces.Length)
						{
							//Debug.Log("s(" + s + ") >= brush_shape.Surfaces.Length (" + brush_shape.Surfaces.Length + ")");
							continue;
						}
						var texGen_index = brush_shape.Surfaces[s].TexGenIndex;
						if (texGen_index < 0 || texGen_index >= surfaceState.surfaceSelectState.Length)
						{
							//Debug.Log("texGen_index("+texGen_index+") < 0 || texGen_index >= surfaceState.surfaceSelectState.Length ("+ surfaceState.surfaceSelectState.Length + ") for surface index " +s);
							continue;
						}
						var selected_state = surfaceState.surfaceSelectState[texGen_index];
						if ((selected_state & SelectState.Selected) == SelectState.None)
							continue;

						foundSelectedSurfaces.Add(new SelectedBrushSurface(brush, s));
						foundTranslations.Add(brush_translation);
					}
				}

				GeometryWireframe[] outlines = null;
				if (foundSelectedSurfaces.Count > 0)
				{
					outlines = BrushOutlineManager.GetSurfaceOutlines(foundSelectedSurfaces.ToArray());
					for (int i = outlines.Length - 1; i >= 0; i--)
					{
						if (outlines[i] != null &&
							outlines[i].vertices != null &&
							outlines[i].vertices.Length >= 0)
							continue;
						ArrayUtility.RemoveAt(ref outlines, i);
					}
				}

				if (outlines != null && outlines.Length > 0)
				{
					selectedVisibleLineMeshManager.Begin();
					selectedInvisibleLineMeshManager.Begin();
					selectedVisiblePolygonMeshManager.Begin();
					CSGRenderer.DrawTexGenOutlines(selectedVisibleLineMeshManager, selectedInvisibleLineMeshManager, 
													selectedVisiblePolygonMeshManager,
													outlines, foundTranslations.ToArray(),
													visibleInnerColor, visibleOuterColor, visibleOuterline, 
													invisibleInnerColor, invisibleOuterColor, invisibleOuterline,
													surfaceColor,
													ToolConstants.thickLineScale);
					selectedVisiblePolygonMeshManager.End();
					selectedInvisibleLineMeshManager.End();
					selectedVisibleLineMeshManager.End();
				} else
				{
					selectedVisiblePolygonMeshManager.Clear();
					selectedInvisibleLineMeshManager.Clear();
					selectedVisibleLineMeshManager.Clear();
				}
			}
			if (forceOutlineUpdate ||				
				prevHoverSurfaceIndex != hoverOnSurfaceIndex ||
				prevHoverBrushIndex != hoverOnTarget)
			{
				prevHoverSurfaceIndex = hoverOnSurfaceIndex;
				prevHoverBrushIndex = hoverOnTarget;

				var visibleInnerColor	= ColorSettings.SurfaceInnerStateColor[(int)SelectState.Hovering];
				var visibleOuterColor	= ColorSettings.SurfaceOuterStateColor[(int)SelectState.Hovering];
				var visibleOuterline	= ColorSettings.MeshEdgeOutline;
				var invisibleInnerColor = ColorSettings.SurfaceInnerStateColor[(int)SelectState.Hovering];
				var invisibleOuterColor = ColorSettings.SurfaceOuterStateColor[(int)SelectState.Hovering];
				var invisibleOuterline	= ColorSettings.MeshEdgeOutline;
				var surfaceColor		= ColorSettings.SurfaceTriangleStateColor[(int)SelectState.Selected];

				invisibleOuterline.a = 0.5f;
				visibleOuterline.a   = 0.5f;

				invisibleInnerColor.a = 1.0f;
				invisibleOuterColor.a = 1.0f;
				visibleOuterColor.a = 1.0f;
				visibleInnerColor.a = 1.0f;
				
				//surfaceColor.a = 1.0f;

				var foundSelectedSurfaces = new List<SelectedBrushSurface>();
				var foundTranslations = new List<Vector3>();
				for (int t = 0; t < this.brushTargets.Length; t++)
				{
					var brush			= brushTargets[t];
					var surfaceState	= surfaceStates[t];

					var brush_cache = InternalCSGModelManager.GetBrushCache(brush);
					if (brush_cache == null || !brush_cache.childData.ModelTransform)
						continue;
					
					var brush_translation	= brush_cache.compareTransformation.modelLocalPosition + brush_cache.childData.ModelTransform.position;
					var brush_shape			= brush.Shape;

					for (int s = 0; s < surfaceState.surfaceSelectState.Length; s++)
					{
						if (s >= brush_shape.Surfaces.Length)
						{
							//Debug.Log("s ("+s+") >= brush_shape.Surfaces.Length("+ brush_shape.Surfaces.Length + ")");
							continue;
						}
						var texGen_index = brush_shape.Surfaces[s].TexGenIndex;
						if (texGen_index >= surfaceState.surfaceSelectState.Length)
						{
							//Debug.Log("texGen_index (" + texGen_index + ") >= surfaceState.surfaceSelectState.Length(" + surfaceState.surfaceSelectState.Length + ")");
							continue;
						}
						var selected_state = surfaceState.surfaceSelectState[texGen_index];
						if ((selected_state & SelectState.Hovering) == SelectState.None)
							continue;

						foundSelectedSurfaces.Add(new SelectedBrushSurface(brush, s));
						foundTranslations.Add(brush_translation);
					}
				}
				
				GeometryWireframe[] outlines = null;
				if (foundSelectedSurfaces.Count > 0)
				{
					outlines = BrushOutlineManager.GetSurfaceOutlines(foundSelectedSurfaces.ToArray());
					for (int i = outlines.Length - 1; i >= 0; i--)
					{
						if (outlines[i] != null &&
							outlines[i].vertices != null &&
							outlines[i].vertices.Length != 0)
							continue;
						ArrayUtility.RemoveAt(ref outlines, i);
					}
				}

				if (outlines != null && outlines.Length > 0)
				{
					hoverVisibleLineMeshManager.Begin();
					hoverInvisibleLineMeshManager.Begin();
					hoverVisiblePolygonMeshManager.Begin();
					CSGRenderer.DrawTexGenOutlines(hoverVisibleLineMeshManager, hoverInvisibleLineMeshManager,
													hoverVisiblePolygonMeshManager,
													outlines, foundTranslations.ToArray(),
													visibleInnerColor, visibleOuterColor, visibleOuterline, 
													invisibleInnerColor, invisibleOuterColor, invisibleOuterline,
													surfaceColor,
													ToolConstants.lineScale);
					hoverVisiblePolygonMeshManager.End();
					hoverInvisibleLineMeshManager.End();
					hoverVisibleLineMeshManager.End();
				} else
				{
					hoverVisiblePolygonMeshManager.Clear();
					hoverInvisibleLineMeshManager.Clear();
					hoverVisibleLineMeshManager.Clear();
				}
			}

			var zTestGenericLineMaterial	= MaterialUtility.ZTestGenericLine;
			var noZTestGenericLineMaterial	= MaterialUtility.NoZTestGenericLine;
			var coloredPolygonMaterial		= MaterialUtility.ColoredPolygonMaterial;

			selectedVisiblePolygonMeshManager.Render(coloredPolygonMaterial);
			hoverVisiblePolygonMeshManager.Render(coloredPolygonMaterial);

			MaterialUtility.LineDashMultiplier		= 1.0f;
			MaterialUtility.LineThicknessMultiplier = 1.0f;
			selectedInvisibleLineMeshManager.Render(noZTestGenericLineMaterial);
			selectedVisibleLineMeshManager  .Render(zTestGenericLineMaterial);
			
			MaterialUtility.LineDashMultiplier		= 1.0f;
			MaterialUtility.LineThicknessMultiplier = 1.0f;
			hoverInvisibleLineMeshManager.Render(noZTestGenericLineMaterial);
			hoverVisibleLineMeshManager  .Render(zTestGenericLineMaterial);		

			forceOutlineUpdate = false;
		}


		void CreateControlIDs()
		{
			//int surfaceEditBrushToolID = GUIUtility.GetControlID(surfaceEditBrushToolHash, FocusType.Keyboard);
			//HandleUtility.AddDefaultControl(surfaceEditBrushToolID);
			
			surfaceSelectPaintControl	= GUIUtility.GetControlID(surfaceSelectPaintControlToolHash, FocusType.Keyboard); 
			textureCopyDragControlID	= GUIUtility.GetControlID(textureCopyDragControlToolHash, FocusType.Keyboard);
			
			for (int t = 0; t < brushTargets.Length; t++)
			{
				var surfaceState = surfaceStates[t];
				if (surfaceState == null)
					return;

				// assign IDs to every surface
				for (int s = 0; s < surfaceState.surfaceSelectState.Length; s++)
				{
					surfaceState.surfaceControlID[s] = GUIUtility.GetControlID(textureSurfaceControlToolHash + t, FocusType.Keyboard);
				}
			}

		}
		
		[NonSerialized] bool mouseIsDown	 = false;
		[NonSerialized] bool mouseIsDragging = false;
		[NonSerialized] bool firstMove		 = false;
		[NonSerialized] bool firstDrag		 = false;
		
		[NonSerialized] Vector2 prevMousePos;


		public void HandleEvents(Rect sceneRect)
		{
			var originalEventType = Event.current.type;
			if      (originalEventType == EventType.MouseMove) { mouseIsDragging = false; }
			else if (originalEventType == EventType.MouseDown) { mouseIsDragging = false; mouseIsDown = true; prevMousePos = Event.current.mousePosition; }
			else if (originalEventType == EventType.MouseDrag)
			{
				mouseIsDown = true; 
				if (!mouseIsDragging && (prevMousePos - Event.current.mousePosition).sqrMagnitude > 4.0f)
					mouseIsDragging = true;
			}

			try
			{
				//if (Event.current.type == EventType.Layout)
					CreateControlIDs();
			
				switch (Event.current.type)
				{
					//case EventType.MouseDown:
					case EventType.MouseUp:
					case EventType.MouseDrag:
					{
						if (Event.current.button != 0 ||
							GUIUtility.hotControl != 0)
							break;
						
						if (!sceneRect.Contains(Event.current.mousePosition))
							break;

						var guiArea = GetLastSceneGUIRect();
						GameObject gameObject = null;
						if (guiArea.Contains(Event.current.mousePosition))
							break;

						if (SceneQueryUtility.FindUnityWorldIntersection(Event.current.mousePosition, out gameObject))
						{
							if (gameObject == null &&
								Event.current.type == EventType.MouseUp)
							{
								SelectionUtility.DeselectAll();
							} else
								SelectionUtility.DoSelectionClick();
							break;
						}
						
						if (dragMode == DragMode.TextureCopy)
						{
							if (Event.current.modifiers == EventModifiers.None)
							{ 
								firstDrag = true;
								currentControl = textureCopyDragControlID;
								GUIUtility.hotControl				= textureCopyDragControlID;
								GUIUtility.keyboardControl			= textureCopyDragControlID;
								EditorGUIUtility.editingTextField	= false;
								if (Event.current.type != EventType.MouseUp)
									Event.current.Use();
							}
							//if (Event.current.type != EventType.MouseUp)
								break;
						} else
						{
							var selectionType = SelectionUtility.GetEventSelectionType();
							switch (selectionType)
							{
								case SelectionType.Additive:		dragMode = DragMode.SurfaceSelectAdd; break;
								case SelectionType.Subtractive:		dragMode = DragMode.SurfaceSelectRemove; break;
								case SelectionType.Toggle:			dragMode = DragMode.SurfaceSelectToggle; break;
								case SelectionType.Replace:			dragMode = DragMode.None; break;
							}
							if (dragMode != DragMode.None)
							{
								currentControl = surfaceSelectPaintControl;
								GUIUtility.hotControl				= surfaceSelectPaintControl;
								GUIUtility.keyboardControl			= surfaceSelectPaintControl;
								EditorGUIUtility.editingTextField	= false;
								if (Event.current.type != EventType.MouseUp)
								{
									Event.current.Use();
									break;
								}
							}
						}
						
						if (Event.current.type != EventType.MouseUp &&
							Event.current.modifiers != EventModifiers.None)
							break;

						if (hoverOnTarget == -1 || hoverOnSurfaceIndex == -1)
						{
							if (Event.current.type == EventType.MouseUp &&
								!mouseIsDragging)
							{
								// see if we clicked into empty space
								if (hoverOnTarget == -1 &&
									hoverOnSurfaceIndex == -1)
								{
									SelectionUtility.DeselectAll();
								}
							}
							break;
						}

						int newControlID = surfaceStates[hoverOnTarget].surfaceControlID[hoverOnSurfaceIndex];
						if (newControlID <= 0)
						{
							break;
						}
						
						currentControl = newControlID;
						GUIUtility.hotControl				= newControlID;
						GUIUtility.keyboardControl			= newControlID;
						EditorGUIUtility.editingTextField	= false; 
						
						if (Event.current.type != EventType.MouseUp)
							Event.current.Use();
						firstMove = true;

						Undo.IncrementCurrentGroup();
						undoGroupIndex = Undo.GetCurrentGroup();

						if (Tools.current == Tool.Rotate)
						{
							var brush				= brushTargets[hoverOnTarget];
							rotationCircle.Initialize(brush.GetComponent<Transform>(), brush.Shape.Surfaces[hoverOnSurfaceIndex], surfaceStates[hoverOnTarget].rayIntersectionWorldPoint);
						} else
						if (Tools.current == Tool.Move)
						{
							var brush				= brushTargets[hoverOnTarget];
							var brush_cache = InternalCSGModelManager.GetBrushCache(brush);
							if (brush_cache == null ||
								brush_cache.childData == null ||
								brush_cache.childData.ModelTransform == null)
								break;
							
							var modelTransform		= brush_cache.childData.ModelTransform;
							var modelTranslation	= modelTransform.position;
							var brushShape			= brush.Shape;
							var hoverOnTexGenIndex	= brushShape.Surfaces[hoverOnSurfaceIndex].TexGenIndex;
			
							InternalCSGModelManager.External.SetBrushTexGen(brush.brushID, hoverOnTexGenIndex, ref brushShape.TexGens[hoverOnTexGenIndex]);
							snappingOffset			= SurfaceUtility.ConvertTextureToWorldCoord(brush.brushID, brushShape, modelTranslation, hoverOnSurfaceIndex, MathConstants.zeroVector2);
						}
			
						break;
					}
					
					case EventType.KeyDown:
					{
						if (Keys.CancelActionKey.IsKeyPressed()) { Event.current.Use(); break; }
						if (Keys.CopyMaterialTexGen.IsKeyPressed()) { if (dragMode == DragMode.None) dragMode = DragMode.TextureCopy; Event.current.Use(); break; }
						if (Keys.HandleSceneKeyDown(CSGBrushEditorManager.CurrentTool, false)) { Event.current.Use(); break; }
						break;
					}

					case EventType.KeyUp:
					{
						if (Keys.CancelActionKey.IsKeyPressed()) { SelectionUtility.DeselectAll(); Event.current.Use(); break; }
						if (Keys.CopyMaterialTexGen.IsKeyPressed()) { if (dragMode == DragMode.TextureCopy) dragMode = DragMode.None; Event.current.Use(); break; }
						if (Keys.HandleSceneKeyUp(CSGBrushEditorManager.CurrentTool, false)) { Event.current.Use(); break; }
						break;
					}

					case EventType.ValidateCommand:
					{
						if (Keys.CancelActionKey    .IsKeyPressed()) { Event.current.Use(); break; }
						if (Keys.CopyMaterialTexGen.IsKeyPressed()) { Event.current.Use(); break; }
						if (Keys.HandleSceneValidate(CSGBrushEditorManager.CurrentTool, false)) { Event.current.Use(); break; }
						break;
					}

					case EventType.Repaint:
					{
						if (SceneTools.IsDraggingObjectInScene)
							break;
					
						var currentSceneView = SceneView.currentDrawingSceneView;
						if (currentSceneView != null)
						{
							var windowRect = new Rect(0, 0, currentSceneView.position.width, currentSceneView.position.height - GUIStyleUtility.BottomToolBarHeight);
							EditorGUIUtility.AddCursorRect(windowRect, currentCursor);
						}
						
						if (currentControl == -1 ||
							currentControl == surfaceSelectPaintControl)
							DrawOutlines();
						break;
					}

					case EventType.Layout:
					{
						UpdateMouseCursor();

						if (currentControl != -1 ||
							Camera.current == null || !Camera.current.pixelRect.Contains(Event.current.mousePosition))
							return;
						
						var repaint = false;
						if (brushTargets == null)
						{
							brushTargets	= new CSGBrush[0];
							surfaceStates	= new TexGenState[0];
						}
						
						Matrix4x4 origMatrix = Handles.matrix;
						Handles.matrix = MathConstants.identityMatrix;
			
						var hoverControl	= -1;
						var hotControl		= GUIUtility.hotControl;
						for (int t = 0; t < brushTargets.Length; t++)
						{
							var brush = brushTargets[t];
							var surfaceState = surfaceStates[t];
							surfaceState.UpdateLayout(brush);				
							for (int s = 0; s < surfaceState.surfaceSelectState.Length; s++)
							{
								// see if this control ID is 'hot'
								if (hotControl == surfaceState.surfaceControlID[s])
									hoverControl = surfaceState.surfaceControlID[s];
							}
						}

						// check if we already have a controlID, otherwise try to see if we're hovering over a surface
						if (hoverControl == -1)
						{ 
							hoverOnSurfaceIndex = -1;
							hoverOnTarget		= -1;
							Grid.ForceGrid		= false;

							int closest_brush	= -1;
							int closest_surface	= -1;

							var world_ray		= HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
							var ray_start		= world_ray.origin;
							var ray_vector		= world_ray.direction * Camera.current.farClipPlane;
							var ray_end			= ray_start + ray_vector;
							
							var guiArea = GetLastSceneGUIRect();
							var sceneView = SceneView.currentDrawingSceneView;
							var wireframeShown = RealtimeCSG.CSGSettings.IsWireframeShown(sceneView);
							BrushIntersection intersection = null;
							if (!guiArea.Contains(Event.current.mousePosition) &&
								SceneQueryUtility.FindWorldIntersection(ray_start, ray_end, out intersection, ignoreInvisible: true, ignoreUnrenderables: !wireframeShown))
							{
								var targets = brushTargets;
								var brush	= intersection.gameObject.GetComponent<CSGBrush>();
								if (!ArrayUtility.Contains(targets, brush))
								{
									if (hoverOnlyTarget > 0 && hoverOnlyTarget < surfaceStates.Length)
									{
										var	surfaceState	= surfaceStates[hoverOnlyTarget];
										bool have_selected	= false;
										for (int s = 0; s < surfaceState.surfaceSelectState.Length; s++)
										{
											if ((surfaceState.surfaceSelectState[s] & SelectState.Selected) == SelectState.Selected)
												have_selected = true;
										}
										if (have_selected)
											hoverOnlyTarget = -1;
									}
									if (hoverOnlyTarget > 0 && hoverOnlyTarget < surfaceStates.Length)
									{
										targets[hoverOnlyTarget] = brush;
										surfaceStates[hoverOnlyTarget] = new TexGenState(brush);
									}  else
									{
										ArrayUtility.Add(ref targets, brush);
										TexGenState newState = new TexGenState(brush);
										ArrayUtility.Add(ref surfaceStates, newState);
										brushTargets = targets;
										hoverOnlyTarget = targets.Length - 1;
									}
								}
							}

							float min_distance	= float.PositiveInfinity;
							for (int t = 0; t < brushTargets.Length; t++)
							{
								var	brush			= brushTargets[t];
								var	surfaceState	= surfaceStates[t];
				
								var brush_cache = InternalCSGModelManager.GetBrushCache(brush);
								if (brush_cache == null || 
									brush_cache.childData == null ||
									brush_cache.childData.ModelTransform == null)
									continue;
					
								if (intersection == null || intersection.brushID != brush.brushID)
								{
									surfaceState.rayIntersectionWorldPoint   = MathConstants.zeroVector3;
									surfaceState.rayIntersectionSurfacePoint = MathConstants.zeroVector3;
									continue;
								}
					
								var distance = (intersection.worldIntersection - ray_start).magnitude;
								if (distance < min_distance)
								{
									min_distance = distance;
									closest_brush = t;
									closest_surface = intersection.surfaceIndex;
								}
								surfaceState.rayIntersectionWorldPoint   = intersection.worldIntersection;
								surfaceState.rayIntersectionSurfacePoint = intersection.surfaceIntersection;
							}
				
							for (int t = 0; t < brushTargets.Length; t++)
							{
								var surfaceState = surfaceStates[t];

								for (int s = 0; s < surfaceState.surfaceSelectState.Length; s++)
								{
									var surfaceControlID = surfaceState.surfaceControlID[s];						
									HandleUtility.AddControl(surfaceControlID, (closest_brush == t && closest_surface == s) ? 0.0f : float.PositiveInfinity);
								}
							}
						}

						var nearestControl = HandleUtility.nearestControl;
						if (hoverControl != -1)
						{
							nearestControl = hoverControl;
						} else
						if (hotControl != 0)
						{
							nearestControl = -1;
						}

						var oldSurfaceStates = new SelectState[0];
						EditMode newEditMode = EditMode.None;
						for (int t = 0; t < brushTargets.Length; t++)
						{
							var surfaceState = surfaceStates[t];

							if (!repaint)
							{
								if (oldSurfaceStates.Length < surfaceState.surfaceSelectState.Length)
									oldSurfaceStates = new SelectState[surfaceState.surfaceSelectState.Length];
								Array.Copy(surfaceState.surfaceSelectState, oldSurfaceStates, surfaceState.surfaceSelectState.Length);
							}

							if ( nearestControl != -1)
							{
								surfaceState.UnHoverAll();
					
								if (newEditMode == EditMode.None)
								{
									for (int s = 0; s < surfaceState.surfaceSelectState.Length; s++)
									{
										if (surfaceState.surfaceControlID[s] == nearestControl)
										{
											newEditMode = SetHoverOn(EditMode.TranslateSurface, t, s);
											surfaceState.surfaceSelectState[s] |= SelectState.Hovering;
											if ((surfaceState.surfaceSelectState[s] & SelectState.Selected) == SelectState.Selected &&
												currentCursor == MouseCursor.Arrow)
												currentCursor = CursorUtility.GetToolCursor();
											break;
										}
									}
								}					
							}
							{
								if (!repaint)
								{
									for (int p = 0; p < surfaceState.surfaceSelectState.Length; p++)
									{
										if (surfaceState.surfaceSelectState[p] != oldSurfaceStates[p])
											repaint = true;
									}
								}
							}
						}
						editMode = newEditMode;
						Handles.matrix = origMatrix;
			
						if (repaint)
							SceneView.RepaintAll();
			
						if (hoverOnTarget >= 0 && hoverOnSurfaceIndex >= 0 && surfaceStates.Length > hoverOnTarget &&
							surfaceStates[hoverOnTarget].surfaceSelectState.Length > hoverOnSurfaceIndex)
						{
							if ((surfaceStates[hoverOnTarget].surfaceSelectState[hoverOnSurfaceIndex] & SelectState.Selected) != SelectState.Selected)
							{
								if (currentCursor == MouseCursor.Arrow)
									currentCursor = MouseCursor.ArrowPlus;
							}
						}
						break;
					}
				}

				var currentHotControl = GUIUtility.hotControl;
				for (int t = 0; t < surfaceStates.Length; t++)
				{
					var surfaceState	= surfaceStates[t];
					var surfaceControls = surfaceState.surfaceControlID;
					for (int s = 0; s < surfaceControls.Length; s++)
					{
						int controlID = surfaceStates[t].surfaceControlID[s];
						if (currentHotControl != controlID)
							continue;
						var eventType = Event.current.GetTypeForControl(controlID);
						switch (eventType)
						{
							case EventType.MouseDrag:
							{
								if (Event.current.button != 0 ||
									(Event.current.modifiers & ~EventModifiers.Control) != EventModifiers.None ||
									GUIUtility.hotControl != controlID ||
									controlID <= 0)
									break;
								
								if (Tools.current == Tool.Move)
								{
									DragTranslateTextureCoordinates();
								} else
								if (Tools.current == Tool.Rotate)
								{
									DragRotateTextureCoordinates();
								}
								break;
							}
							case EventType.MouseUp:
							{
								if (Event.current.button != 0 ||
									GUIUtility.hotControl != controlID)
									break;
								
								if (!mouseIsDragging)
								{
									// see if we clicked into empty space
									if (hoverOnTarget == -1 &&
										hoverOnSurfaceIndex == -1)
									{
										SelectionUtility.DeselectAll();
									} else
									{
										UpdateSelection();
									}
								} else
								{
									Undo.CollapseUndoOperations(undoGroupIndex);
									Undo.IncrementCurrentGroup();
									Undo.FlushUndoRecordObjects();
								}
							
								GUIUtility.hotControl = 0;
								GUIUtility.keyboardControl = 0;
								EditorGUIUtility.editingTextField = false;
								currentControl = -1;
								Event.current.Use();
								break;
							}
							case EventType.Repaint:
							{
								if (GUIUtility.hotControl != controlID)
									break;
								if (Tools.current == Tool.Rotate)
								{
									if (dragMode == DragMode.None)
									{
										if (mouseIsDown &&
											hoverOnTarget != -1 &&
											hoverOnSurfaceIndex != -1)
										{
											rotationCircle.Render();
										}
									}
								} else
								if (Tools.current == Tool.Move)
								{
									if (editMode == EditMode.TranslateSurface &&
										mouseIsDragging)
									{
										var textCenter2D = Event.current.mousePosition;
										textCenter2D.x += 25;
										textCenter2D.y += 60;

										PaintUtility.DrawScreenText(textCenter2D, 
											Units.ToRoundedPixelsString(deltaMovement));
									}
								}
								break;
							}
						}
					}

				}

				var type = Event.current.GetTypeForControl(textureCopyDragControlID);
				switch (type)
				{
					case EventType.MouseDrag:
					{
						if (Event.current.button != 0 ||
							Event.current.modifiers != EventModifiers.None ||
							GUIUtility.hotControl != textureCopyDragControlID ||
							textureCopyDragControlID <= 0)
							break;

						if (dragMode == DragMode.TextureCopy)
						{
							if (firstDrag)
							{
								lastSelectedSurface = null;
								firstDrag = false;
							}

							DragTextureCopy();
						}
						Event.current.Use();
						break;
					}
					case EventType.MouseUp:
					{
						if (Event.current.button != 0 ||
							GUIUtility.hotControl != textureCopyDragControlID)
							break;
						
						if (dragMode == DragMode.TextureCopy)
						{
							if (mouseIsDragging)
							{							
								lastSelectedSurface = null;
							} else
								DragTextureCopy();
						}
					
						if (!mouseIsDragging)
						{
							// see if we clicked into empty space
							if (hoverOnTarget == -1 &&
								hoverOnSurfaceIndex == -1)
							{
								SelectionUtility.DeselectAll();
							} else
							{
								UpdateSelection();
							}
						}
					
						GUIUtility.hotControl = 0;
						GUIUtility.keyboardControl = 0;
						EditorGUIUtility.editingTextField = false;
						currentControl = -1;
						Event.current.Use();
						firstDrag = false;
						break;
					}
				}
			
				type = Event.current.GetTypeForControl(surfaceSelectPaintControl);
				switch (type)
				{
					case EventType.MouseDrag:
					{
						if (Event.current.button != 0 ||
							GUIUtility.hotControl != surfaceSelectPaintControl ||
							surfaceSelectPaintControl <= 0)
							break;
					
						if (dragMode == DragMode.SurfaceSelectAdd ||
							dragMode == DragMode.SurfaceSelectRemove ||
							dragMode == DragMode.SurfaceSelectToggle)
							DragSurfaceSelect();
						Event.current.Use();
						break;
					}
					case EventType.MouseUp:
					{
						if (Event.current.button != 0 ||
							GUIUtility.hotControl != surfaceSelectPaintControl)
							break;
					
						if (!mouseIsDragging)
						{
							// see if we clicked into empty space
							if (hoverOnTarget == -1 &&
								hoverOnSurfaceIndex == -1)
							{
								SelectionUtility.DeselectAll();
							} else
							{
								UpdateSelection();
							}
						}
					
						GUIUtility.hotControl = 0;
						GUIUtility.keyboardControl = 0;
						EditorGUIUtility.editingTextField = false;
						currentControl = -1;
						Event.current.Use();
						break;
					}
				}
			}
			finally
			{
				if (originalEventType == EventType.MouseUp ||
					originalEventType == EventType.MouseMove) { mouseIsDragging = false; mouseIsDown = false; }
			}
		}

		
		static Vector2 scrollPos;
		public void OnInspectorGUI(EditorWindow window)
		{
			scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
			{
				SurfaceToolGUI.OnInspectorGUI(window);
			}
			EditorGUILayout.EndScrollView();
		}
		
		public Rect GetLastSceneGUIRect()
		{
			return SurfaceToolGUI.GetLastSceneGUIRect(this);
		}

		public bool OnSceneGUI()
		{
			if (brushTargets == null)
				return false;
			
			SurfaceToolGUI.OnSceneGUI(this);
			return true;
		}
	}
}
