#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEditor;

namespace UnityEssentials
{
    public partial class MaskMapGenerator
    {
        public float DefaultMetallic = 0.5f;
        public float DefaultAO = 1f;
        public float DefaultDetail = 0.5f;
        public float DefaultSmoothness = 0.5f;

        private Texture2D _metallic, _ambientOcclusion, _detailMask, _smoothnessMap;
        private Texture2D _finalTexture;
        private Vector2Int _textureSize;

        private struct TextureImportSettings
        {
            public bool isReadable;
            public bool crunchedCompression;
        }

        private Dictionary<string, TextureImportSettings> _originalTextureSettings = new();

        private void SetTexturesForProcessing()
        {
            foreach (var texture in new[] { _metallic, _ambientOcclusion, _detailMask, _smoothnessMap })
            {
                if (texture == null)
                    continue;

                string path = AssetDatabase.GetAssetPath(texture);
                if (string.IsNullOrEmpty(path))
                    continue;

                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                    continue;

                // Store original settings if not already stored
                if (!_originalTextureSettings.ContainsKey(path))
                    _originalTextureSettings[path] = new TextureImportSettings
                    {
                        isReadable = importer.isReadable,
                        crunchedCompression = importer.crunchedCompression
                    };

                // Set required settings for processing
                if (!importer.isReadable || importer.crunchedCompression)
                {
                    importer.isReadable = true;
                    importer.crunchedCompression = false;
                    importer.SaveAndReimport();
                }
            }
        }

        private void RestoreTextureSettings()
        {
            foreach (var kvp in _originalTextureSettings)
            {
                var importer = AssetImporter.GetAtPath(kvp.Key) as TextureImporter;
                if (importer == null)
                    continue;

                importer.isReadable = kvp.Value.isReadable;
                importer.crunchedCompression = kvp.Value.crunchedCompression;
                importer.SaveAndReimport();
            }
            _originalTextureSettings.Clear();
        }

        private void PackTextures()
        {
            SetTexturesForProcessing();
            try
            {
                if (!TryGenerateTexture(false))
                    return;

                string path = EditorUtility.SaveFilePanelInProject(
                    "Save Mask Map",
                    GetDefaultFileName(),
                    "png",
                    "Specify where to save the mask map texture.",
                    GetDefaultFolderPath());

                if (string.IsNullOrEmpty(path))
                {
                    Debug.LogWarning("Save operation canceled.");
                    return;
                }

                File.WriteAllBytes(path, _finalTexture.EncodeToPNG());
                AssetDatabase.Refresh();
                Debug.Log($"Mask map saved to: {path}");
            }
            finally { RestoreTextureSettings(); }
        }

        private void ClearAll()
        {
            _metallic = _ambientOcclusion = _detailMask = _smoothnessMap = _finalTexture = null;
            _textureSize = Vector2Int.zero;

            DefaultMetallic = 0.5f;
            DefaultAO = 1f;
            DefaultDetail = 0.5f;
            DefaultSmoothness = 0.5f;

            Repaint();
        }

        private bool TryGenerateTexture(bool asPreview)
        {
            SetTexturesForProcessing();
            try
            {
                if (!ValidateTextures())
                    return false;

                if (_finalTexture == null || _finalTexture.width != _textureSize.x || _finalTexture.height != _textureSize.y)
                    _finalTexture = new Texture2D(_textureSize.x, _textureSize.y, TextureFormat.RGBA32, false, false)
                    {
                        hideFlags = HideFlags.HideAndDontSave
                    };

                var pixels = new Color[_textureSize.x * _textureSize.y];
                for (int y = 0; y < _textureSize.y; y++)
                    for (int x = 0; x < _textureSize.x; x++)
                        pixels[y * _textureSize.x + x] = new Color(
                            GetChannelValue(_metallic, DefaultMetallic, x, y),
                            GetChannelValue(_ambientOcclusion, DefaultAO, x, y),
                            GetChannelValue(_detailMask, DefaultDetail, x, y),
                            GetSmoothnessValue(x, y));

                _finalTexture.SetPixels(pixels);
                _finalTexture.Apply();
                return true;
            }
            finally { RestoreTextureSettings(); }
        }

        private float GetChannelValue(Texture2D texture, float defaultValue, int x, int y) =>
            texture ? texture.GetPixel(x, y).grayscale : defaultValue;

        private float GetSmoothnessValue(int x, int y)
        {
            float value = _smoothnessMap ?
                _smoothnessMap.GetPixel(x, y).grayscale :
                DefaultSmoothness;
            return _invertSmoothness ? 1f - value : value;
        }

        private bool ValidateTextures()
        {
            if (!CheckTextureRequirements())
                return false;

            _textureSize = GetBaseTextureSize();
            if (_textureSize == Vector2Int.zero)
            {
                EditorUtility.DisplayDialog("Error", "No valid textures found", "OK");
                return false;
            }

            ValidateTextureDimensions();
            return true;
        }

        private bool CheckTextureRequirements()
        {
            var problematic = new List<Texture2D>();
            foreach (var texture in new[] { _metallic, _ambientOcclusion, _detailMask, _smoothnessMap })
            {
                if (texture && (!texture.isReadable || IsCrunchCompressed(texture)))
                    problematic.Add(texture);
            }

            if (problematic.Count == 0)
                return true;

            string message = $"{problematic.Count} texture(s) need fixing:\n" +
                             string.Join("\n", problematic.ConvertAll(t => t.name));

            if (!EditorUtility.DisplayDialog("Fix Textures", message, "Fix", "Cancel"))
                return false;

            problematic.ForEach(FixTextureSettings);
            AssetDatabase.Refresh();
            return true;
        }

        private Vector2Int GetBaseTextureSize()
        {
            foreach (var texture in new[] { _metallic, _ambientOcclusion, _detailMask, _smoothnessMap })
                if (texture && texture.isReadable)
                    return new(texture.width, texture.height);
            return new(512, 512);
        }

        private void ValidateTextureDimensions()
        {
            var textures = new[] {
                (_metallic, "Metallic"),
                (_ambientOcclusion, "AO"),
                (_detailMask, "Detail Mask"),
                (_smoothnessMap, "Smoothness")
            };

            foreach (var (texture, name) in textures)
                if (texture && (texture.width != _textureSize.x || texture.height != _textureSize.y))
                {
                    Debug.LogWarning($"Removing {name} texture due to size mismatch");
                    if (name == "Metallic") _metallic = null;
                    else if (name == "AO") _ambientOcclusion = null;
                    else if (name == "Detail Mask") _detailMask = null;
                    else if (name == "Smoothness") _smoothnessMap = null;
                }
        }

        private string GetDefaultFolderPath()
        {
            foreach (var texture in new[] { _metallic, _ambientOcclusion, _detailMask, _smoothnessMap })
            {
                if (!texture)
                    continue;
                string path = Path.GetDirectoryName(AssetDatabase.GetAssetPath(texture));
                return path?.Replace("\\", "/") ?? "Assets";
            }
            return "Assets";
        }

        private string GetDefaultFileName()
        {
            foreach (var texture in new[] { _metallic, _ambientOcclusion, _detailMask, _smoothnessMap })
            {
                if (!texture)
                    continue;
                string name = Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(texture));
                return name.EndsWith("Mask") ? name : RemoveLastWord(name) + "Mask";
            }
            return "LitMask";
        }

        private string RemoveLastWord(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            int lastIndex = input.LastIndexOfAny(new[] { '_', '-', ' ' });
            if (lastIndex > 0)
                return input.Substring(0, lastIndex + 1);

            var matches = Regex.Matches(input, "[A-Z][a-z]*");
            if (matches.Count > 1)
                return input.Substring(0, matches[^1].Index);

            return input;
        }

        private void FixTextureSettings(Texture2D texture)
        {
            string path = AssetDatabase.GetAssetPath(texture);
            if (string.IsNullOrEmpty(path))
                return;

            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
                return;

            importer.isReadable = true;
            importer.crunchedCompression = false;
            importer.SaveAndReimport();
        }

        private static bool IsCrunchCompressed(Texture2D texture) =>
            texture.format == TextureFormat.DXT1Crunched ||
            texture.format == TextureFormat.DXT5Crunched ||
            texture.format == TextureFormat.ETC2_RGBA8Crunched;
    }
}
#endif