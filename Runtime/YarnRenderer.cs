using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using EditorAttributes;
using Plawius.NonConvexCollider;
using UGI.CoreUtils;
using UGI.CoreUtils.Math;
using UGI.CoreUtils.Reflection;
using UGI.Structures;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace UGI.Puzzle
{
    [ExecuteAlways]
    [RequireComponent(typeof(Renderer))]
    public class YarnRenderer : MonoBehaviour
    {
        public static readonly int ColorIdPropertyId = Shader.PropertyToID("_BaseColor");
        public static readonly int DissolvePropertyId = Shader.PropertyToID("_DissolveValue");
        public static readonly int DissolveMinPropertyId = Shader.PropertyToID("_ClipMin");
        public static readonly int DissolveMaxPropertyId = Shader.PropertyToID("_ClipMax");
        public static readonly int DissolveDirectionPropertyId = Shader.PropertyToID("_DissolveDirection");
        public static readonly int DissolveMaskPropertyId = Shader.PropertyToID("_DissolveMask");

        public Renderer Renderer => _renderer;
        [SerializeField] private Renderer _renderer;
        [SerializeField] private bool _dissolveEnabled = true;

        private MaterialPropertyBlock? _propertyBlock;

        [SerializeField, Range(0f, 1f)] private float _dissolveValue;
        [field: SerializeField] public float PointRotation { get; set; }

        private Vector3 _dissolveNormal;

        [SerializeField] private DissolveData _dissolveData;
        
        private readonly GizmosRenderer _gizmosRenderer = new();
        private Texture2D? _dissolveMask;

        public float DissolveValue
        {
            get => _dissolveValue;
            set
            {
                _dissolveValue = value;
                UpdateDissolve();
            }
        }
        
        public Vector3 Center => _renderer.bounds.center + transform.TransformVector(_dissolveData.CenterOffset);

        private void OnEnable()
        {
            DissolveValue = 0;
            _propertyBlock ??= new MaterialPropertyBlock();
        }

#if UNITY_EDITOR
        private void Reset()
        {
            UpdateFields();
        }

        public void UpdateFields()
        {
            gameObject.GetOrInitField(ref _renderer);
            var colorsLibrary = ColorsLibrary.Colors.OrderByDescending(c => c.Name.Value.Length);
            
            if (_renderer.sharedMaterial.shader.name != ColorsLibrary.YarnPartMaterial.shader.name)
            {
                var isColorSet = false;
                var currentMaterialName = _renderer.sharedMaterial.name;
                currentMaterialName = currentMaterialName.Replace('_', ' ').ToLower();
                
                foreach (var colorPreset in colorsLibrary)
                {
                    if (currentMaterialName.Contains(colorPreset.Name.Value.Replace('_', ' ').ToLower()))
                    {
                        _renderer.sharedMaterial = colorPreset.Material;
                        isColorSet = true;
                        break;
                    }
                }

                if (!isColorSet)
                {
                    Debug.LogError($"Can't find material for {currentMaterialName}");
                    _renderer.sharedMaterial = ColorsLibrary.YarnPartMaterial;
                }
            }

            if (gameObject.TryGetComponent<YarnPart>(out var part))
            {
                ReflectionHelper.SetFieldValue(part, "_yarnRenderer", this);
            }
        }
#endif
        
        private void OnDrawGizmosSelected()
        {
            TryCalculateDissolvePoint(out _);
            _gizmosRenderer.Render();
        }

        public Vector3 GetUnravelDirection()
        {
            var direction = transform.TransformDirection(_dissolveData.Direction.normalized);

            if (_dissolveData.ApplyScale)
            {
                direction = Vector3.Scale(direction, transform.lossyScale);
            }
            
            return direction;
        }

        public bool TryCalculateDissolvePoint(out Vector3 dissolveMeshPoint)
        {
            dissolveMeshPoint = Vector3.zero;
            _gizmosRenderer.Clear();

            if (!transform.TryGetComponent<MeshFilter>(out var filter)) return false;
            
            var dirWorld = GetUnravelDirection();

            
            var center = transform.TransformPoint(_dissolveData.CenterOffset);

            var radius = _dissolveData.DirectionRadiusMultiplier;

            var directionPerpendicular = UGIMath.VectorPerpendicular(dirWorld);
            
            var arcPerpendicular = Vector3.Cross(dirWorld, directionPerpendicular).normalized;

            var dissolveValue = _dissolveData.PointEase.Evaluate(DissolveValue);

            var axisStart = center - dirWorld * radius;
            var axisEnd = center + dirWorld * radius;
            var axisProgress = Vector3.Lerp(axisStart, axisEnd, dissolveValue);
            
            _gizmosRenderer.Queue(() =>
            {
                Gizmos.color = Color.green;
                Gizmos.DrawSphere(axisStart, GizmosUtil.PointSize);
                Gizmos.DrawSphere(axisEnd, GizmosUtil.PointSize);
                GizmosUtil.DrawArrow(axisStart, axisEnd, Color.green, arrowPosition: 1f);
            });

            var plane = new Plane(dirWorld, axisProgress);

            var crossPoints = UGIMeshMath.GetCrossSectionPoints(
                filter.sharedMesh,
                transform,
                plane, _dissolveData.PlanePrecision
            );
            
            _gizmosRenderer.Queue(() =>
            {
                Gizmos.color = Color.red;
                GizmosUtil.DrawPlane(axisProgress, plane);
                Gizmos.DrawSphere(axisProgress, GizmosUtil.PointSize);

                foreach (var crossPoint in crossPoints)
                {
                    Gizmos.DrawSphere(crossPoint, GizmosUtil.PointSize);
                }
            });

            if (crossPoints.Count == 0)
            {
                return false;
            }
            
            var arcPoint = axisProgress
                           + arcPerpendicular * radius;
            
            arcPoint = UGIMath.RotateAroundAxis(arcPoint, center, dirWorld, math.radians(PointRotation));

            var meshPoint = UGIMath.FindClosestPoint(arcPoint, crossPoints);

            _gizmosRenderer.Queue(() =>
            {
                Gizmos.color = Color.black;
                Gizmos.DrawSphere(meshPoint, GizmosUtil.PointSize);
                Gizmos.DrawWireSphere(arcPoint, GizmosUtil.PointSize);
            });

            dissolveMeshPoint = meshPoint;
            return true;
        }
        
        public void SetColor(string? colorId)
        {
            if (colorId.IsNullOrEmpty()) return;

            _renderer.sharedMaterial = ColorsLibrary.GetPreset(colorId).Material;
            UpdateMaterialData();
        }

        public void ClipByCustomMask(Texture2D texture)
        {
            _propertyBlock ??= new MaterialPropertyBlock();
            _dissolveMask = texture;
            UpdateMaterialData();
        }

        private void UpdateMaterialData()
        {
            _propertyBlock ??= new MaterialPropertyBlock();

            if (_dissolveMask != null)
            {
                _renderer.material.EnableKeyword("_DISSOLVE_MASK_AS_CLIP_ON");
                _propertyBlock.SetTexture(DissolveMaskPropertyId, _dissolveMask);
            }
        }

        public void UpdateDissolve()
        {
            if (!_dissolveEnabled) return;
            
            _dissolveData ??= new DissolveData();
            _propertyBlock ??= new MaterialPropertyBlock();

            _propertyBlock.SetVector(DissolveDirectionPropertyId,
                new Vector4(_dissolveData.Direction.x, _dissolveData.Direction.y, _dissolveData.Direction.z, 0));
            _propertyBlock.SetFloat(DissolveMinPropertyId, _dissolveData.Remap.x);
            _propertyBlock.SetFloat(DissolveMaxPropertyId, _dissolveData.Remap.y);
            _propertyBlock.SetFloat(DissolvePropertyId, DissolveValue);
            _renderer.AsNullable()?.SetPropertyBlock(_propertyBlock);
        }
        
        public async UniTask UnravelAnimation(float duration, float rotationSpeed, CancellationToken token)
        {
            var totaDuration = duration;
            while (duration > 0 && !token.IsCancellationRequested)
            {
                var v = math.lerp(0, 1, (1 - duration / totaDuration));
                DissolveValue = v;
                PointRotation += rotationSpeed * Time.deltaTime;
                duration -= Time.deltaTime;
                
                HapticImpulse.Generate();
                
                await UniTask.Yield(token, true);
            }
            
            this.SetActive(false);
        }

        public static implicit operator Renderer(YarnRenderer renderer) => renderer._renderer;

        [Serializable]
        public class DissolveData
        {
            public Vector2 Remap = new(0f, 1f);
            [Clamp(-1, 1, -1, 1, -1, 1)] public Vector3 Direction = new(0, 1, 0);
            public Vector3 CenterOffset = Vector3.zero;
            public AnimationCurve PointEase = AnimationCurveHelper.Linear4PointPreset();
            public float PlanePrecision = 0.01f;
            public float DirectionRadiusMultiplier = 1f;
            public bool ApplyScale = true;
        }
    }
}