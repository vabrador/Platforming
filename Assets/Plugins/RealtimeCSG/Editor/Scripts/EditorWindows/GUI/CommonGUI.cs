using System;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using InternalRealtimeCSG;
using RealtimeCSG.Helpers;
using RealtimeCSG;

namespace InternalRealtimeCSG
{
	internal sealed class CommonGUI
	{
		private static GUIContent UpdateLightmapUVContent = new GUIContent("Update lightmap UVs");

		public static void GenerateLightmapUVButton(CSGModel[] models)
		{
			GUIStyleUtility.InitStyles();

			bool needLightmapUVUpdate = false;
			for (int m = 0; m < models.Length && !needLightmapUVUpdate; m++)
			{
				if (!models[m])
					continue;

				needLightmapUVUpdate = MeshInstanceManager.NeedToGenerateLightmapUVsForModel(models[m]) || needLightmapUVUpdate;
			}

			if (needLightmapUVUpdate)
			{
				if (GUILayout.Button(UpdateLightmapUVContent, GUIStyleUtility.redButton))
				{
					for (int m = 0; m < models.Length; m++)
					{
						if (!models[m] || models[m].AutoRebuildUVs)
							continue;

						if (!MeshInstanceManager.NeedToGenerateLightmapUVsForModel(models[m]))
							continue;

						MeshInstanceManager.GenerateLightmapUVsForModel(models[m]);
					}
				}
				GUILayout.Space(10);
			}
		}

		private static readonly GUIContent	ContentVisibleSurfaces		= new GUIContent("Visible");
		private static readonly GUIContent	ContentShadowOnlySurfaces	= new GUIContent("Shadow-Only");
		private static readonly GUIContent	ContentDiscardSurfaces		= new GUIContent("Discard");
		private static readonly ToolTip		ToolTipVisibleSurfaces		= new ToolTip("Visible", "Make every selected surface a visible part of final mesh again");
		private static readonly ToolTip		ToolTipShadowOnlySurfaces	= new ToolTip("Shadow-Only", "Make every selected surface be invisible, but still cast shadows");
		private static readonly ToolTip		ToolTipDiscardSurfaces		= new ToolTip("Discard", "Discard the selected surfaces from the final mesh");


		public static void OnSurfaceFlagButtons(SelectedBrushSurface[] selectedBrushSurfaces, bool isSceneGUI = false)
		{
			var leftStyle	= isSceneGUI ? EditorStyles.miniButtonLeft  : GUI.skin.button;
			var middleStyle	= isSceneGUI ? EditorStyles.miniButtonMid   : GUI.skin.button;
			var rightStyle	= isSceneGUI ? EditorStyles.miniButtonRight : GUI.skin.button;
			
			var canDiscard			= false;
			var canMakeShadowOnly	= false;
			var canMakeVisible		= false;
			
			if (selectedBrushSurfaces.Length > 0)
			{
				for (var i = 0; i < selectedBrushSurfaces.Length; i++)
				{
					var brush			= selectedBrushSurfaces[i].brush;
					var surfaceIndex	= selectedBrushSurfaces[i].surfaceIndex;
					if (surfaceIndex >= brush.Shape.Surfaces.Length)
					{
						Debug.Log("surface_index >= brush.Shape.Surfaces.Length");
						continue; 
					}
					var texGenIndex	= brush.Shape.Surfaces[surfaceIndex].TexGenIndex;
					if (texGenIndex >= brush.Shape.TexGens.Length)
					{
						Debug.Log("texGen_index >= brush.Shape.TexGens.Length");
						continue;
					}

					var texGenFlags = brush.Shape.TexGenFlags[texGenIndex];
					if ((texGenFlags & TexGenFlags.Discarded) == TexGenFlags.Discarded)
					{
						canMakeVisible = true;
						canMakeShadowOnly = true;
					} else
					if ((texGenFlags & TexGenFlags.ShadowOnly) == TexGenFlags.ShadowOnly)
					{
						canDiscard = true;
						canMakeVisible = true;
					} else
					{
						canDiscard = true;
						canMakeShadowOnly = true;
					}
				}
			}


			GUILayout.BeginHorizontal(GUIStyleUtility.ContentEmpty);
			{
				EditorGUI.BeginDisabledGroup(!canDiscard);
				{
					if (GUILayout.Button(ContentDiscardSurfaces, leftStyle)) { SurfaceUtility.DiscardSurfaces(selectedBrushSurfaces); }
					TooltipUtility.SetToolTip(ToolTipDiscardSurfaces);
				}
				EditorGUI.EndDisabledGroup();
				EditorGUI.BeginDisabledGroup(!canMakeShadowOnly);
				{
					if (GUILayout.Button(ContentShadowOnlySurfaces, middleStyle)) { SurfaceUtility.MakeSurfacesShadowOnly(selectedBrushSurfaces); }
					TooltipUtility.SetToolTip(ToolTipShadowOnlySurfaces);
				}
				EditorGUI.EndDisabledGroup();
				EditorGUI.BeginDisabledGroup(!canMakeVisible);
				{
					if (GUILayout.Button(ContentVisibleSurfaces, rightStyle)) { SurfaceUtility.MakeSurfacesVisible(selectedBrushSurfaces); }
					TooltipUtility.SetToolTip(ToolTipVisibleSurfaces);
				}
				EditorGUI.EndDisabledGroup();
			}
			GUILayout.EndHorizontal();
		}

		public static TexGenFlags OnSurfaceFlagButtons(TexGenFlags texGenFlags, bool isSceneGUI = false)
		{
			var leftStyle	= isSceneGUI ? EditorStyles.miniButtonLeft : GUI.skin.button;
			var middleStyle = isSceneGUI ? EditorStyles.miniButtonMid : GUI.skin.button;
			var rightStyle	= isSceneGUI ? EditorStyles.miniButtonRight : GUI.skin.button;

			var canDiscard			= (texGenFlags & TexGenFlags.Discarded) != TexGenFlags.Discarded;
			var canMakeShadowOnly	= (texGenFlags & TexGenFlags.ShadowOnly) != TexGenFlags.ShadowOnly;
			var canMakeVisible		= !canDiscard || !canMakeShadowOnly;
			
			GUILayout.BeginHorizontal(GUIStyleUtility.ContentEmpty);
			{
				EditorGUI.BeginDisabledGroup(!canDiscard);
				{
					if (GUILayout.Button(ContentDiscardSurfaces, leftStyle))
					{
						texGenFlags &= ~TexGenFlags.ShadowOnly;
						texGenFlags |= TexGenFlags.Discarded;
						GUI.changed = true;
					}
					TooltipUtility.SetToolTip(ToolTipDiscardSurfaces);
				}
				EditorGUI.EndDisabledGroup();
				EditorGUI.BeginDisabledGroup(!canMakeShadowOnly);
				{
					if (GUILayout.Button(ContentShadowOnlySurfaces, middleStyle))
					{
						texGenFlags &= ~TexGenFlags.Discarded;
						texGenFlags |= TexGenFlags.ShadowOnly;
						GUI.changed = true;
					}
					TooltipUtility.SetToolTip(ToolTipShadowOnlySurfaces);
				}
				EditorGUI.EndDisabledGroup();
				EditorGUI.BeginDisabledGroup(!canMakeVisible);
				{
					if (GUILayout.Button(ContentVisibleSurfaces, rightStyle))
					{
						texGenFlags &= ~(TexGenFlags.Discarded | TexGenFlags.ShadowOnly);
						GUI.changed = true;
					}
					TooltipUtility.SetToolTip(ToolTipVisibleSurfaces);
				}
				EditorGUI.EndDisabledGroup();
			}
			GUILayout.EndHorizontal();
			return texGenFlags;
		}


		public static void StartToolGUI()
		{
			GenerateLightmapUVButton(InternalCSGModelManager.Models);
		}
	}
}
