using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Cysharp.Text;
using UGI.CoreUtils;
using UGI.UIElements;
using Unity.Properties;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;
using VContainer;

namespace UGI.Puzzle
{
    [CustomEditor(typeof(YarnObject))]
    public class YarnObjectEditor : Editor
    {
        private YarnObject _yarnObject;
        private Property<YarnObject, YarnPart[]> PartsArrayProperty => (Property<YarnObject, YarnPart[]>)PropertyContainer.GetProperty(_yarnObject, new PropertyPath("_parts"));

        private string[] _decorNamesInserts = {"button", "btn", "decor"};

        public override VisualElement CreateInspectorGUI()
        {
            _yarnObject =  (YarnObject)target;

            var integrationButton = new Button()
            {
                style = {color = Color.darkOrange}
            };

            return new VisualElement()
                .AddDefaultInspector(this)
                .AddChild(new Button().SetText(nameof(UpdateParts)).SetClickCallback(UpdateParts))
                .AddChild(new Button().SetText(nameof(SyncConfigsColor)).SetClickCallback(() => SyncConfigsColor().Forget()))
                .AddChild(integrationButton.SetText("Launch Integration").SetClickCallback(LaunchIntegration))
                .AddChild(new Label().SetText("Detailed Updating:"))
                .AddChild(new Button().SetText("Update Colliders").SetClickCallback(() =>
                {
                    _yarnObject.Parts.Foreach(part => part.ResetComponent());
                }))
                .AddChild(new Button().SetText("Update Renderers").SetClickCallback(() =>
                {
                    _yarnObject.Parts.Foreach(YarnRendererUtils.CalculateDissolveDirection);
                    _yarnObject.Parts.Foreach(YarnRendererUtils.CalculateRemap);
                    _yarnObject.Parts.Foreach(e => YarnRendererUtils.CalculateDissolveRadius(e.Renderer));
                }))
                .AddChild(new Button().SetText("Update Directions").SetClickCallback(() =>
                {
                    _yarnObject.Parts.Foreach(YarnRendererUtils.CalculateDissolveDirection);
                }))
                .AddChild(new Button().SetText("Update Remap").SetClickCallback(() =>
                {
                    _yarnObject.Parts.Foreach(YarnRendererUtils.CalculateRemap);
                }))
                .AddChild(new Button().SetText("Update Radius Multiplier").SetClickCallback(() =>
                {
                    _yarnObject.Parts.Foreach(e => YarnRendererUtils.CalculateDissolveRadius(e.Renderer));
                }))
                .AddChild(new Button().SetText("Update Decor").SetClickCallback(UpdateDecor));
        }

        private void LaunchIntegration()
        {
            var report = new Report();

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Yarn Integration");
            var undoGroup = Undo.GetCurrentGroup();
            Undo.RegisterFullObjectHierarchyUndo(_yarnObject.gameObject, "Yarn Integration");

            try
            {
                InitParts();

                var parts = CollectParts();

                if (!Validate(parts, report))
                {
                    EditorUtility.DisplayDialog(
                        "Yarn Integration aborted",
                        string.Join("\n", report.Errors),
                        "OK");
                    return;
                }

                WriteParts(parts);
                report.Parts = parts.Length;

                foreach (var part in parts)
                {
                    YarnRendererUtils.CalculateDissolveDirection(part);
                    YarnRendererUtils.CalculateRemap(part);
                    YarnRendererUtils.CalculateDissolveRadius(part.Renderer);
                }

                BindDecorations(parts, report);

                EditorUtility.SetDirty(_yarnObject);
            }
            catch (Exception e)
            {
                report.Error($"Integration failed: {e.Message}");
                Debug.LogException(e, _yarnObject);
            }
            finally
            {
                Undo.CollapseUndoOperations(undoGroup);
                report.Flush("Launch Integration");
            }
        }

        private void InitParts()
        {
            var elements = _yarnObject.GetComponentsInChildren<MeshRenderer>(true);

            foreach (var element in elements)
            {
                var go = element.gameObject;

                if (go.GetComponent<YarnDecoration>() != null || go.GetComponent<YarnPart>() != null)
                {
                    continue;
                }

                var isDecor = _decorNamesInserts.Any(insert => go.name.ToLower().Contains(insert));

                if (isDecor)
                {
                    Undo.AddComponent<YarnDecoration>(go);
                }
                else
                {
                    Undo.AddComponent<YarnPart>(go);
                }
            }
        }

        private YarnPart[] CollectParts() => _yarnObject.GetComponentsInChildren<YarnPart>(true);

        private bool Validate(YarnPart[] parts, Report report)
        {
            if (parts.Length == 0)
            {
                report.Error("No YarnParts found under this object.", _yarnObject);
            }

            var seenIds = new HashSet<string>();

            foreach (var part in parts)
            {
                if (part.Renderer == null || part.Renderer.Renderer == null)
                {
                    report.Error($"Part '{part.name}' has no renderer.", part);
                    continue;
                }

                var meshFilter = part.GetComponent<MeshFilter>();
                if (meshFilter == null || meshFilter.sharedMesh == null)
                {
                    report.Error($"Part '{part.name}' has no mesh.", part);
                }

                if (part.GetComponent<Collider>() == null)
                {
                    report.Error($"Part '{part.name}' has no collider. Run Reset / Update Colliders first.", part);
                }

                if (part.Id.IsNullOrEmpty())
                {
                    report.Error($"Part '{part.name}' has an empty id.", part);
                }
                else if (!seenIds.Add(part.Id))
                {
                    report.Error($"Duplicate part id '{part.Id}'.", part);
                }

                var material = part.Renderer.Renderer.sharedMaterial;
                if (material == null || ResolveColorName(material).IsNullOrEmpty())
                {
                    var materialName = material != null ? material.name : "<none>";
                    report.Error($"Part '{part.name}' material '{materialName}' has no matching color in ColorsLibrary.", part);
                }
            }

            return !report.HasErrors;
        }

        private static string ResolveColorName(Material material) =>
            ColorsLibrary.Colors.FirstOrDefault(preset => preset.Equals(material))?.Name ?? "";

        private void WriteParts(YarnPart[] parts)
        {
            PropertyContainer.SetValue(_yarnObject, "_parts", parts);
            EditorUtility.SetDirty(_yarnObject);
        }

        private void UpdateParts()
        {
            var report = new Report();
            var parts = CollectParts();

            if (Validate(parts, report))
            {
                WriteParts(parts);
                report.Parts = parts.Length;
            }

            report.Flush(nameof(UpdateParts));
        }

        private void UpdateDecor()
        {
            var report = new Report();
            BindDecorations(CollectParts(), report);
            report.Flush("Update Decor");
        }

        private void BindDecorations(YarnPart[] yarnParts, Report report)
        {
            var decorations = _yarnObject.GetComponentsInChildren<YarnDecoration>(true);

            foreach (var part in yarnParts)
            {
                part.Decorations.Clear();
            }

            var partColliders = new Collider[yarnParts.Length];
            for (int i = 0; i < yarnParts.Length; i++)
            {
                partColliders[i] = yarnParts[i].GetComponent<Collider>();
            }

            foreach (var decor in decorations)
            {
                var decorPosition = decor.transform.position;

                var decorMeshRenderer = decor.GetComponent<MeshRenderer>();
                if (decorMeshRenderer != null)
                {
                    decorMeshRenderer.sharedMaterial = ColorsLibrary.DecorMaterial;
                }

                YarnPart closestPart = null;
                var closestSqrMagnitude = float.MaxValue;

                for (int i = 0; i < yarnParts.Length; i++)
                {
                    var collider = partColliders[i];
                    if (collider == null) continue;

                    var candidatePoint = collider.ClosestPoint(decorPosition);
                    var sqrMagnitude = (candidatePoint - decorPosition).sqrMagnitude;

                    if (sqrMagnitude >= closestSqrMagnitude)
                    {
                        continue;
                    }

                    closestSqrMagnitude = sqrMagnitude;
                    closestPart = yarnParts[i];
                }

                if (closestPart != null)
                {
                    closestPart.Decorations.Add(decor);
                    decor.SetDirection(Vector3.up);
                    EditorUtility.SetDirty(closestPart);
                    EditorUtility.SetDirty(decor);
                    report.Decorations++;
                }
                else
                {
                    report.Warn($"Decoration '{decor.name}' could not be bound to any part.");
                }
            }
        }

        private async Task SyncConfigsColor()
        {
            var targetAsset = AssetDatabaseHelper.GetObjectProjectAsset(_yarnObject.gameObject);

            var objectInfo = GeneralLibrary.YarnObjects.Values
                .FirstOrDefault(info => ((GameObject)info.Prefab.editorAsset) == targetAsset);
            if (objectInfo == null)
            {
                Debug.LogError($"[Yarn] SyncConfigsColor: no YarnObjectInfo matches prefab '{_yarnObject.name}'.", _yarnObject);
                return;
            }

            var resolver = await ConfigsContainer.Build();
            var objectConfig = resolver.Resolve<YarnObjectConfig.Collection>()
                .FirstOrDefault(config => config.Id == objectInfo.Id);
            if (objectConfig == null)
            {
                Debug.LogError($"[Yarn] SyncConfigsColor: no config found for object id '{objectInfo.Id}'.", _yarnObject);
                return;
            }
            var partsArray = PartsArrayProperty.GetValue(ref _yarnObject);
            var sb = new StringBuilder();
            sb.AppendLine("Start part configs color sync process.");

            foreach (var yarnPart in partsArray)
            {
                var renderer = yarnPart.Renderer;
                var partConfig = objectConfig.Parts[yarnPart.Id];
                var currentRendererColor = ColorsLibrary.GetPreset(renderer.Renderer.sharedMaterial);

                if (currentRendererColor.Name != partConfig.Color)
                {
                    sb.AppendLine($"Change part {yarnPart.Id} color from {currentRendererColor.Name} to {partConfig.Color}");
                    renderer.SetColor(partConfig.Color);
                }
            }

            sb.LogConsole();
            EditorUtility.SetDirty(_yarnObject.gameObject);
        }

        private class Report
        {
            public readonly List<string> Errors = new();
            public readonly List<string> Warnings = new();
            public int Parts;
            public int Decorations;

            public bool HasErrors => Errors.Count > 0;

            public void Error(string message, UnityEngine.Object context = null)
            {
                Errors.Add(message);
                Debug.LogError(message, context);
            }

            public void Warn(string message) => Warnings.Add(message);

            public void Flush(string header)
            {
                using var sb = ZString.CreateStringBuilder();
                sb.AppendFormat("[Yarn] {0}: {1} part(s), {2} decoration(s), {3} error(s), {4} warning(s).",
                    header, Parts, Decorations, Errors.Count, Warnings.Count);

                foreach (var warning in Warnings)
                {
                    sb.AppendLine();
                    sb.AppendFormat("  warn: {0}", warning);
                }

                if (HasErrors)
                {
                    Debug.LogError(sb.ToString());
                }
                else
                {
                    Debug.Log(sb.ToString());
                }
            }
        }
    }
}
