using System.Collections.Generic;
using Cysharp.Text;
using EditorAttributes;
using Plawius.NonConvexCollider;
using UGI.CoreUtils;
using Unity.VisualScripting;
using UnityEngine;

namespace UGI.Puzzle
{
    public partial class YarnPart
    {
#if UNITY_EDITOR
        public List<YarnDecoration> Decorations
        {
            get => _yarnDecorations;
            set => _yarnDecorations = value;
        }
        
        private void Reset()
        {
            ResetComponent();
        }

        [Button]
        public void ResetComponent()
        {
            if (string.IsNullOrEmpty(Id))
            {
                using (var nameBuilder = ZString.CreateStringBuilder())
                {
                    var parentName = transform.parent.AsNullable()?.name;
                    if (!parentName.IsNullOrEmpty() && !name.Contains(parentName))
                    {
                        nameBuilder.AppendFormat("{0}_", parentName);
                    }

                    nameBuilder.Append(name);
                    Id = nameBuilder.ToString();
                }
            }

            gameObject.GetOrInitField(ref _yarnRenderer, true);
            
            RefreshCollider(false);
        }

        [Button]
        public void RefreshCollider(bool force = true, bool maximizeResolution = true)
        {
            var meshFilter = _yarnRenderer.AsNullable()?.GetComponent<MeshFilter>();
            if (meshFilter.IsNull()) return;
            
            var nonConvexColliderComponent = this.GetOrAddComponent<NonConvexColliderComponent>();
            if (maximizeResolution)
            {
                nonConvexColliderComponent.Params = Parameters.HighResolution();
            }

            var colliderAsset = nonConvexColliderComponent.AsNullable()?.ColliderAsset;
            
            if (colliderAsset.IsNull() || force)
            {
                Plawius.NonConvexCollider.Editor.UnityExtensions.GenerateCollidersFromRenderingMesh(nonConvexColliderComponent);
                UnityEditor.EditorUtility.SetDirty(nonConvexColliderComponent);
            }

            if (nonConvexColliderComponent.NotNull())
            {
                AssetDatabaseHelper.RenameWithOverwrite(nonConvexColliderComponent.ColliderAsset, $"Convex_{Id}");
            }
        }
#endif
    }
}