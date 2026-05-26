using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using EditorAttributes;
using UGI.CoreUtils;
using UGI.Serialization;
using UnityEditor;
using UnityEngine;
using UnityEngine.Assertions;

namespace UGI.Puzzle
{
    public class ColorsLibrary : ScriptableObject
    {
        private static Singleton<ColorsLibrary> _singleton =
            SingletonProvider.ProvideFromAddressables<ColorsLibrary>(nameof(ColorsLibrary));

        public static ColorPreset GetPreset(string id) => _singleton.Instance._colors.GetOrDefault(id) ?? NullColorPreset;
        public static ColorPreset GetPreset(Material material) => _singleton.Instance._colors.FirstOrDefault(preset => preset.Material == material) ?? NullColorPreset;

        public static IEnumerable<ColorPreset> Colors => _singleton.Instance._colors;

#if UNITY_EDITOR
        public static KeyedValuesCollection<string, ColorPreset> ColorsCollection => _singleton.Instance._colors;
#endif
        [SerializeField] private KeyedValuesCollection<string, ColorPreset> _colors;

        public static ColorPreset NullColorPreset => _singleton.Instance._nullColorPreset;
        [SerializeField] private ColorPreset _nullColorPreset;
        
        public static StringVariableAsset LockedColor => _singleton.Instance._lockedColor;
        [SerializeField] private StringVariableAsset _lockedColor; 

        public static Material YarnPartMaterial => _singleton.Instance._yarnPartMaterial;
        [SerializeField] private Material _yarnPartMaterial;
        
        public static Material DecorMaterial => _singleton.Instance._decorMaterial;
        [SerializeField] private Material _decorMaterial;

#if UNITY_EDITOR
        [Conditional("UNITY_EDITOR")]
        [Button]
        private void FindColorPresets()
        {
            _colors.Values.Clear();
            _colors.Values.AddRange(AssetDatabaseHelper.FindAssetsInProject<ColorPreset>());
            AssetDatabase.SaveAssetIfDirty(this);
        }
#endif
    }
}