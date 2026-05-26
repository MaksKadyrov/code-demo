using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using EditorAttributes;
using LitMotion;
using LitMotion.Extensions;
using UGI.CoreUtils;
using UGI.CoreUtils.Locking;
using UGI.Structures;
using UGI.Target;
using UnityEngine;
using VContainer;

namespace UGI.Puzzle
{
    public partial class YarnPart : MonoBehaviour, IRaycastReceiver, IReusable
    {
        [field: SerializeField] public string Id { get; private set; }

        public YarnRenderer Renderer => _yarnRenderer;
        [SerializeField] private YarnRenderer _yarnRenderer;
        private readonly List<MeshCollider> _meshColliders = new();

        [SerializeField] private List<YarnDecoration> _yarnDecorations = new();
        
        [Inject] private UnravelPartCommand _unravelPartCommand;
        [Inject] private LevelModel _levelModel;
        [Inject] private YarnColorRepository _yarnColors;
        [Inject] private ColorController _colorController;

        public ITarget UnravelHandle => _handleTarget;
        private SimplePositionTarget _handleTarget;

        public readonly LockObject UnravelLock = new LockObject();
        public string? ColorId { get; private set; }
        
        public bool IsComplete { get; set; }

        [ShowInInspector] private Vector3 _baseScale;
        private YarnPart? _nestedPart;
        
        private YarnPartModel _model;
        
        private void Awake()
        {
            _handleTarget = new SimplePositionTarget(() =>
            {
                _yarnRenderer.TryCalculateDissolvePoint(out var point);
                return point;
            });
            GetComponents(_meshColliders);
            _meshColliders.RemoveAll(c => !c.enabled);
            _baseScale = transform.localScale;
        }

        public void OnSetup()
        {
            _yarnDecorations.Clear();
            _nestedPart = null;
        }

        public void Configure(YarnPartModel model)
        {
            _model = model;
        }

        public void SetColor(string color)
        {
            if (!YarnColor.NonPlayable(color))
            {
                _model.Color = color;
            }
            
            _yarnRenderer.SetColor(color);
            ColorId = color;
            _yarnColors[color].ChangeCount(ColorCounterType.Part, 1);
        }

        public void SetNestedPart(YarnPart nestedPart)
        {
            _nestedPart = nestedPart;
            _nestedPart.SetActive(false);
        }

        public async UniTask SetComplete()
        {
            _meshColliders.ForEach(c => c.enabled = false);
            _model.CompletesCount++;
            IsComplete = true;
            
            UnpinDecor();

            using (_nestedPart.AsNullable()?.ShowAsNested())
            {
                await UnravelLock.WaitForUnlock();
            }
        }

        private DisposableHandle<YarnPart> ShowAsNested()
        {
            if (IsComplete)
            {
                return _nestedPart.AsNullable()?.ShowAsNested() ?? new DisposableHandle<YarnPart>(this, part => {});
            }
            
            transform.localScale = AnimationLibrary.YarnPartAnimation.GetPartStartScale(_baseScale);
            SetColor(_colorController.YarnPartGenerator.GenerateColor()!);
            this.SetActive(true);
            
            return new DisposableHandle<YarnPart>(this, part =>
            {
                var animation = AnimationLibrary.YarnPartAnimation;

                LMotion.Create(animation.GetPartStartScale(part._baseScale), part._baseScale, animation.NestedPartScaleDuration)
                    .WithEase(animation.NestedPartScaleEase)
                    .BindToLocalScale(part.transform)
                    .ToUniTask(CancelBehavior.Complete, part.destroyCancellationToken).Forget();
            });
        }

        public void UnpinDecor(bool instant = false)
        {
            _yarnDecorations.ForEachWithState(instant, (decor, instantDisable) =>
            {
                decor.Unpin(instantDisable);
            });
        }

        public void ReceiveRaycast()
        {
            if (UnravelLock.IsLocked || YarnColor.NonPlayable(ColorId)) return;
            
            _unravelPartCommand.Execute(this, destroyCancellationToken).Forget();
        }

        [Serializable]
        public class AnimationData
        {
            [SerializeField] private float NestedPartStartScale;
            public float NestedPartScaleDuration;
            public Ease NestedPartScaleEase;
            
            public Vector3 GetPartStartScale(Vector3 baseScale) => baseScale * NestedPartStartScale;
        }
    }
}