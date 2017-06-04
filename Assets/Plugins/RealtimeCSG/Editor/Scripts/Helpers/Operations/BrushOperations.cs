using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using RealtimeCSG;

namespace InternalRealtimeCSG
{
    internal static class BrushOperations
    {
        public static void SnapToGrid(CSGBrush[] brushes)
        {
            var worldDeltaMovement = MathConstants.zeroVector3;

            var transforms = new List<Transform>();
            for (var b = 0; b < brushes.Length; b++)
            {
                if (brushes[b] == null ||
                    !brushes[b])
                {
                    continue;
                }

                var brushTransform = brushes[b].GetComponent<Transform>();
                var brushLocalToWorld = brushTransform.localToWorldMatrix;

                transforms.Add(brushTransform);

                var controlMesh = brushes[b].ControlMesh;
                var points = controlMesh.Vertices;

                var worldPoints = new Vector3[points.Length];
                for (var p = 0; p < points.Length; p++)
                {
                    worldPoints[p] = brushLocalToWorld.MultiplyPoint(points[p]);
                }

                worldDeltaMovement = Grid.SnapDeltaToGrid(worldDeltaMovement, worldPoints.ToArray(), snapToGridPlane: false, snapToSelf: false);
            }

            if (worldDeltaMovement == MathConstants.zeroVector3)
                return;

            for (var b = 0; b < transforms.Count; b++)
            {
                var transform = transforms[b];
                transform.position += worldDeltaMovement;
            }
        }

        public static void Flip(CSGBrush[] brushes, Matrix4x4 flipMatrix, string undoDescription = "Flip brushes")
        {
            var fail = false;
            Undo.IncrementCurrentGroup();
            Undo.RegisterCompleteObjectUndo(brushes.ToArray<UnityEngine.Object>(), undoDescription);

            var isGlobal = Tools.pivotRotation == PivotRotation.Global;

            var centerAll = BoundsUtilities.GetCenter(brushes);
            for (var t = 0; t < brushes.Length; t++)
            {
                var brush = brushes[t];
                var shape = brush.Shape;
                var position = brush.transform.position;

                Matrix4x4 brushFlip;
                Vector3 brushCenter;
                if (isGlobal)
                {
                    brushFlip = brush.transform.localToWorldMatrix *
                                       flipMatrix *
                                       brush.transform.worldToLocalMatrix;
                    brushCenter = brush.transform.InverseTransformPoint(centerAll) - position;
                }
                else
                {
                    brushFlip = flipMatrix;
                    brushCenter = brush.transform.InverseTransformPoint(centerAll);
                }

                for (var s = 0; s < shape.Surfaces.Length; s++)
                {
                    var plane = shape.Surfaces[s].Plane;

                    var normal = brushFlip.MultiplyVector(plane.normal);
                    var biNormal = brushFlip.MultiplyVector(shape.Surfaces[s].BiNormal);
                    var tangent = brushFlip.MultiplyVector(shape.Surfaces[s].Tangent);

                    var pointOnPlane = plane.pointOnPlane;
                    pointOnPlane -= brushCenter;
                    pointOnPlane = brushFlip.MultiplyPoint(pointOnPlane);
                    pointOnPlane += brushCenter;

                    shape.Surfaces[s].Plane = new CSGPlane(normal, pointOnPlane);
                    shape.Surfaces[s].BiNormal = biNormal;
                    shape.Surfaces[s].Tangent = tangent;
                }

                var controlMesh = brush.ControlMesh;
                brush.Shape = shape;

                brush.ControlMesh = ControlMeshUtility.CreateFromShape(shape);
                if (brush.ControlMesh == null)
                {
                    brush.ControlMesh = controlMesh;
                    fail = true;
                    break;
                }

                ShapeUtility.CreateCutter(shape, brush.ControlMesh);
                brush.EnsureInitialized();

                brush.ControlMesh.SetDirty();
                EditorUtility.SetDirty(brush);

                InternalCSGModelManager.CheckSurfaceModifications(brush, true);

                ControlMeshUtility.RebuildShape(brush);
            }
            if (fail)
            {
                Debug.Log("Failed to perform operation");
                Undo.RevertAllInCurrentGroup();
            }
            InternalCSGModelManager.Refresh();
        }

        public static void FlipX(CSGBrush[] flipBrushes, string undoDescription = "Flip brushes on X Axis") { var flipMatrix = MathConstants.identityMatrix; flipMatrix.m00 = -1; Flip(flipBrushes, flipMatrix, "Flip brushes on X Axis"); }
        public static void FlipY(CSGBrush[] flipBrushes, string undoDescription = "Flip brushes on Y Axis") { var flipMatrix = MathConstants.identityMatrix; flipMatrix.m11 = -1; Flip(flipBrushes, flipMatrix, "Flip brushes on Y Axis"); }
        public static void FlipZ(CSGBrush[] flipBrushes, string undoDescription = "Flip brushes on Z Axis") { var flipMatrix = MathConstants.identityMatrix; flipMatrix.m22 = -1; Flip(flipBrushes, flipMatrix, "Flip brushes on Z Axis"); }

    }
}
