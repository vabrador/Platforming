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
	internal class RotationCircle
	{
		public bool		HaveRotateStartAngle = false;
		public Vector3	RotateStartVector;
		public Vector3  RotateCenterPoint;
		public Vector3	RotateSurfaceNormal;
		public Vector3	RotateSurfaceTangent;
		public float	RotateRadius;
		public float	RotateOriginalAngle;
		public float	RotateCurrentStartAngle;
		public float	RotateCurrentSnappedAngle;
		
		public void Clear()
		{			
			HaveRotateStartAngle		= false;
			RotateRadius				= 0;
			RotateCurrentStartAngle		= 0;
			RotateCurrentSnappedAngle	= 0;
		}

		public void Render()
		{
			var endAngle = RotateCurrentStartAngle + RotateCurrentSnappedAngle;
			if (HaveRotateStartAngle)
			{
				PaintUtility.DrawRotateCirclePie(RotateCenterPoint, RotateSurfaceNormal, RotateSurfaceTangent, RotateRadius,
												RotateOriginalAngle, RotateCurrentStartAngle, endAngle,
												ColorSettings.RotateCircleOutline);//, RotateCirclePieFill, ColorSettings.RotateCirclePieOutline);
			}
			PaintUtility.DrawRotateCircle(RotateCenterPoint, RotateSurfaceNormal, RotateSurfaceTangent, RotateRadius,
											RotateOriginalAngle, RotateCurrentStartAngle, endAngle,
											ColorSettings.RotateCircleOutline);//, ColorSettings.RotateCircleHatches);
		}

		public void Initialize(Transform transform, Surface surface, Vector3 centerPoint)
		{
			RotateSurfaceNormal		= transform.TransformVector(surface.Plane.normal);
			RotateSurfaceTangent	= transform.TransformVector(surface.Tangent);
			RotateCenterPoint		= centerPoint;
			Clear();
		}

		public bool UpdateRadius(TexGen surfaceTexGen, Vector3 currentSurfacePoint, bool toggleSnapping)
		{
			var handleSize		= GUIStyleUtility.GetHandleSize(this.RotateCenterPoint);				
			var vectorToCenter	= currentSurfacePoint - this.RotateCenterPoint;
			this.RotateRadius	= vectorToCenter.magnitude;
			vectorToCenter.Normalize();

			var rotateCurrentAngle = 0.0f;
			if (!this.HaveRotateStartAngle)
			{
				if (this.RotateRadius > handleSize * ToolConstants.minRotateRadius)
				{
					this.HaveRotateStartAngle		= true;
					this.RotateOriginalAngle		= surfaceTexGen.RotationAngle;					
					this.RotateStartVector			= vectorToCenter;
					this.RotateCurrentStartAngle	= GeometryUtility.SignedAngle(this.RotateSurfaceTangent, vectorToCenter, this.RotateSurfaceNormal);
				}
				this.RotateRadius = Math.Max(this.RotateRadius, handleSize * ToolConstants.minRotateRadius);
			} else
			{ 
				rotateCurrentAngle		= GeometryUtility.SignedAngle(this.RotateStartVector, vectorToCenter, this.RotateSurfaceNormal);
				var minSize				= handleSize * ToolConstants.minRotateRadius * 2;
				var radiusStepSize		= minSize;
				this.RotateRadius = (Mathf.CeilToInt(((this.RotateRadius - minSize) / radiusStepSize) - 0.5f) * radiusStepSize) + minSize;
			}
							
			// snap texture coordinates in world/local space
			this.RotateCurrentSnappedAngle	= GridUtility.SnappedAngle(this.RotateOriginalAngle + rotateCurrentAngle, 
															toggleSnapping) - this.RotateOriginalAngle;

			return this.HaveRotateStartAngle;
		}
	}
}
