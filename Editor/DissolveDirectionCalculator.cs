using UGI.CoreUtils.Reflection;
using UnityEngine;

namespace UGI.Puzzle
{
    public class DissolveDirectionCalculator
    {
        private Vector3[] _potentialDirections =
        {
            new(1, 0, 0),
            new(0, 1, 0),
            new(0, 0, 1),
        };
        
        public void CalculateDissolveDirection(YarnPart yarnPart)
        {
            var meshFilter = yarnPart.GetComponent<MeshFilter>();
            if (meshFilter == null || meshFilter.sharedMesh == null)
            {
                Debug.LogError($"[Yarn] CalculateDissolveDirection: part '{yarnPart.name}' has no mesh.", yarnPart);
                return;
            }

            var mesh = meshFilter.sharedMesh;

            var maxDistance = float.MinValue;
            var vertices = mesh.vertices;

            var selectedDirection = 0;
            for (int i = 0; i < _potentialDirections.Length; i++)
            {
                var direction = _potentialDirections[i];

                if (!IsOnBitangent(direction, mesh))
                {
                    continue;
                }

                var directionNormalized = direction.normalized;
                var minProjection = float.PositiveInfinity;
                var maxProjection = float.NegativeInfinity;
                foreach (var vertex in vertices)
                {
                    var vertexProjection = Vector3.Dot(vertex, directionNormalized);
                    if (vertexProjection < minProjection) minProjection = vertexProjection;
                    if (vertexProjection > maxProjection) maxProjection = vertexProjection;
                }

                var distance = maxProjection - minProjection;
                if (distance > maxDistance)
                {
                    maxDistance = distance;
                    selectedDirection = i;
                }
            }
            
            var dissolveData = ReflectionHelper.GetFieldValue<YarnRenderer.DissolveData>(yarnPart.Renderer, "_dissolveData");
            dissolveData.Direction = _potentialDirections[selectedDirection];
            
            UnityEditor.EditorUtility.SetDirty(yarnPart.Renderer);
        }
        
        private bool IsOnBitangent(Vector3 direction, Mesh mesh, float maxDelta = 0.005f)
        {
            var normals = mesh.normals;
            var tangents = mesh.tangents;
            var count = Mathf.Min(normals.Length, tangents.Length);
            for (int i = 0; i < count; i++)
            {
                var tangent = new Vector3(tangents[i].x, tangents[i].y, tangents[i].z);
                var bitangent = Vector3.Cross(tangent, normals[i]);
                var dotProduct = Mathf.Abs(Vector3.Dot(bitangent, direction.normalized));

                if (dotProduct <= maxDelta)
                {
                    return true;
                }
            }

            return false;
        }
    }
}