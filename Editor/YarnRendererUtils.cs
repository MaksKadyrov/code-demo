using UGI.CoreUtils.Reflection;
using UnityEngine;

namespace UGI.Puzzle
{
    public static class YarnRendererUtils
    {
        private const float _gap = .01f;

        private static DissolveDirectionCalculator _dissolveDirectionCalculator = new();
        
        public static void CalculateDissolveRadius(YarnRenderer target)
        {
            if (target == null)
            {
                Debug.LogError("[Yarn] CalculateDissolveRadius: renderer is null.");
                return;
            }

            var dissolve = ReflectionHelper.GetFieldValue<YarnRenderer.DissolveData>(target, "_dissolveData");
            dissolve.DirectionRadiusMultiplier = dissolve.Remap.y - dissolve.Remap.x - _gap * 2;
            UnityEditor.EditorUtility.SetDirty(target);
        }

        public static void CalculateRemap(YarnPart yarnPart)
        {
            var meshFilter = yarnPart.GetComponent<MeshFilter>();
            if (meshFilter == null || meshFilter.sharedMesh == null)
            {
                Debug.LogError($"[Yarn] CalculateRemap: part '{yarnPart.name}' has no mesh.", yarnPart);
                return;
            }

            var mesh = meshFilter.sharedMesh;
            var dissolveData = ReflectionHelper.GetFieldValue<YarnRenderer.DissolveData>(yarnPart.Renderer, "_dissolveData");
            var directionLocal = dissolveData.Direction;
            var directionNormalized = directionLocal.normalized;
            var minProjection = float.PositiveInfinity;
            var maxProjection = float.NegativeInfinity;
            var vertexMin = Vector3.zero;
            var vertexMax = Vector3.zero;
            
            var vertices = mesh.vertices;
            foreach (var vertex in vertices)
            {
                var vertexProjection = Vector3.Dot(vertex, directionNormalized);
                if (vertexProjection < minProjection)
                {
                    minProjection = vertexProjection;
                    vertexMin = vertex;
                }

                if (vertexProjection > maxProjection)
                {
                    maxProjection = vertexProjection;
                    vertexMax = vertex;
                }
            }

            var scaledProjectionMin = (minProjection + 1f) * 0.5f;
            var scaledProjectionMax = (maxProjection + 1f) * 0.5f;
            var remap = new Vector2(scaledProjectionMin - _gap, scaledProjectionMax + _gap);
            dissolveData.Remap = remap;
            dissolveData.ApplyScale = true;

            dissolveData.CenterOffset = (vertexMin + vertexMax) / 2;
            UnityEditor.EditorUtility.SetDirty(yarnPart.Renderer);
        }
        
        public static void CalculateDissolveDirection(YarnPart yarnPart)
        {
            _dissolveDirectionCalculator.CalculateDissolveDirection(yarnPart);
        }
    }
}