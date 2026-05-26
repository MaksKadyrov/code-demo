using System.Collections.Generic;
using System.Linq;
using UGI.CoreUtils;
using UGI.CoreUtils.Locking;
using UnityEngine;
using VContainer;

namespace UGI.Puzzle
{
    public class YarnObject : MonoBehaviour
    {
        public const string GameLayer = "Gameplay";
        
        public Transform RotationRoot { get; private set; }
        public Transform PositionRoot { get; private set; }
        
        public string Id { get; private set; }
        
        public IEnumerable<YarnPart> Parts
        {
            get
            {
                if (!_allPartsInitialized)
                {
                    _allParts.AddRange(_parts);
                    _allPartsInitialized = true;
                }
                return _allParts;
            }
        }

        [SerializeField] private YarnPart[] _parts;

        [Inject] private IObjectResolver _resolver;
        [Inject] private ColorsPool _colorsPool;
        [Inject] private LevelAnimationLock _levelAnimationLock;
        
        private readonly List<YarnPart> _allParts = new();
        private bool _allPartsInitialized;

        public LockObject UnravelingLock { get; } = new();

        public void Configure(string id, ObjectPartsModel objectPartsModel, Transform rotationRoot)
        {
            _levelAnimationLock.Attach(UnravelingLock);
            RotationRoot = rotationRoot;
            PositionRoot = transform;
            Id = id;
            _allParts.Clear();
            _allPartsInitialized = true;
            foreach (YarnPart part in _parts)
            {
                if (!objectPartsModel.TryGetValue(part.Id, out var yarnPartModel))
                {
                    Debug.LogError($"Part {part.Id} not found in config");
                    continue;
                }

                var currentHead = part;
                
                if (yarnPartModel.CompletesCount > 0)
                {
                    currentHead.UnpinDecor(true);
                }

                if (yarnPartModel.IsComplete)
                {
                    currentHead.IsComplete = true;
                    currentHead.SetActive(false);
                    continue;
                }
                
                part.AddTo(_allParts);
                part.Configure(yarnPartModel);
                part.SetColor(yarnPartModel.Color ?? _colorsPool.GetNext()!);
                part.UnravelLock.Attach(UnravelingLock);

                for (int i = yarnPartModel.CompletesCount; i < yarnPartModel.StackCount; i++)
                {
                    var stackPart = Instantiate(part, part.transform.parent, false).InjectGameObject(_resolver);
                    stackPart.AddTo(_allParts);
                    stackPart.OnSetup();
                    stackPart.Configure(yarnPartModel);
                    stackPart.UnravelLock.Attach(UnravelingLock);
                    currentHead.SetNestedPart(stackPart);
                    currentHead = stackPart;
                }
            }
        }

        public IEnumerable<YarnPart> GetPlayableParts() => Parts.Where(p => p.gameObject.activeInHierarchy && !p.IsComplete);

        private void OnDrawGizmos()
        {
            var bounds = CalculateBounds();
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(bounds.center, bounds.size);
            Gizmos.DrawSphere(CalculateCenter(), GizmosUtil.PointSize);
        }

        public Bounds CalculateBounds()
        {
            var bounds = new Bounds();
            var initialized = false;

            foreach (var targetPart in GetPlayableParts())
            {
                var renderer = targetPart.Renderer.Renderer;
                if (renderer == null) continue;

                if (!initialized)
                {
                    bounds = renderer.bounds;
                    initialized = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return bounds;
        }

        public Vector3 CalculateCenter()
        {
            int pointsCount = 0;
            var sum = Vector3.zero;
            
            foreach (var targetPart in GetPlayableParts())
            {
                pointsCount++;
                sum += transform.InverseTransformPoint(targetPart.transform.position);
            }

            if (pointsCount == 0)
            {
                return Vector3.zero;
            }

            return sum / pointsCount;
        }
    }
}