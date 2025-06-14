#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Text.RegularExpressions;

namespace UnityEssentials
{
    /// <summary>
    /// Provides a Unity Editor tool for combining grayscale mask maps into a single RGBA texture.
    /// </summary>
    /// <remarks>The <see cref="MaskMapGenerator"/> class allows users to pack multiple grayscale textures 
    /// (Metallic, Ambient Occlusion, Detail Mask, and Smoothness/Roughness) into a single mask map texture.  It
    /// provides a graphical interface for assigning textures, adjusting default values, and generating  the final
    /// combined texture. The tool also includes options for previewing the result and saving the  final texture as a
    /// PNG file.  This tool is accessible via the Unity Editor menu under "Tools/Mask Map Packer" or through the 
    /// context menu in the Assets window.</remarks>
    public partial class MaskMapGenerator
    {
        // Mask Map Textures
        private Texture2D _metallic;
        private Texture2D _ambientOcclusion;
        private Texture2D _detailMask;
        private Texture2D _smoothnessMap;

        // Default Values
        public float DefaultMetallic = 0.5f;
        public float DefaultAO = 1f;
        public float DefaultDetail = 0.5f;
        public float DefaultSmoothness = 0.5f;

        // Processed Texture
        private Texture2D _finalTexture;

        // State Variables
        private Vector2Int _textureSize = Vector2Int.zero;

        /// <summary>
        /// Packs the textures and saves the final mask map as a PNG.
        /// Modified to start save dialog in the folder of the first input texture and name it accordingly.
        /// </summary>
        private void PackTextures()
        {
            CheckTexture();
            UpdateTexture(asPreview: false);

            if (_finalTexture == null)
            {
                EditorUtility.DisplayDialog(
                    "Error",
                    "Final texture is not generated. Please ensure that at least one mask map texture is assigned and all textures are readable.",
                    "OK");
                return;
            }

            // Determine default folder and file name based on input textures
            string defaultFolder = GetDefaultFolderPath();
            string defaultName = GetDefaultFileName();

            // Open Save File Dialog in the determined folder with the default name
            string path = EditorUtility.SaveFilePanelInProject(
                "Save Mask Map",
                defaultName,
                "png",
                "Specify where to save the mask map texture.",
                defaultFolder);

            if (!string.IsNullOrEmpty(path))
            {
                byte[] pngData = _finalTexture.EncodeToPNG();
                if (pngData != null)
                {
                    File.WriteAllBytes(path, pngData);
                    AssetDatabase.Refresh();
                    Debug.Log($"Mask map saved to: {path}");
                }
                else EditorUtility.DisplayDialog("Error", "Failed to encode texture to PNG.", "OK");
            }
            else Debug.LogWarning("Save operation canceled.");
        }

        private void ClearAll()
        {
            _metallic = null;
            _ambientOcclusion = null;
            _detailMask = null;
            _smoothnessMap = null;

            DefaultMetallic = 0.5f;
            DefaultAO = 1f;
            DefaultDetail = 0.5f;
            DefaultSmoothness = 0.5f;

            _finalTexture = null;
            _textureSize = Vector2Int.zero;
            Repaint();
        }

        private float GetPixelValue(Texture2D texture, int x, int y)
        {
            if (texture == null)
                return 0f;

            // Ensure texture is readable
            if (!texture.isReadable)
            {
                Debug.LogError($"Texture '{texture.name}' is not readable. Please enable 'Read/Write Enabled' in the import settings.");
                return 0f;
            }

            Color pixel = texture.GetPixel(x, y);
            return pixel.grayscale;
        }

        private void ValidateTextureSizes()
        {
            // Reset textureSize
            _textureSize = Vector2Int.zero;

            // List of mask maps
            Texture2D[] maskMaps = { _metallic, _ambientOcclusion, _detailMask, _smoothnessMap };
            string[] maskNames = { "Metallic", "Ambient Occlusion", "Detail Mask", "Smoothness/Roughness" };

            // Initialize variables to determine consistency
            bool firstReadableFound = false;
            int requiredWidth = 0;
            int requiredHeight = 0;

            // First pass: Determine the required resolution based on the first readable texture
            for (int i = 0; i < maskMaps.Length; i++)
            {
                var texture = maskMaps[i];
                if (texture != null)
                {
                    // Check if texture is readable
                    if (!texture.isReadable)
                    {
                        Debug.LogError($"Texture '{texture.name}' is not readable. Please enable 'Read/Write Enabled' in the import settings.");
                        EditorUtility.DisplayDialog(
                            "Unreadable Texture",
                            $"The texture '{texture.name}' assigned to '{maskNames[i]}' is not readable. Please enable 'Read/Write Enabled' in the texture's import settings.",
                            "OK");
                        continue; // Do not nullify; allow user to fix it
                    }

                    if (!firstReadableFound)
                    {
                        requiredWidth = texture.width;
                        requiredHeight = texture.height;
                        firstReadableFound = true;
                        // Continue to set required size
                        _textureSize = new Vector2Int(requiredWidth, requiredHeight);
                        break;
                    }
                }
            }

            // If no readable mask maps are assigned, set to default 512x512
            if (!firstReadableFound)
            {
                _textureSize = new Vector2Int(512, 512);
                Debug.Log("No readable mask map textures assigned. Using default texture size: 512x512");
                return;
            }

            // Second pass: Validate all readable textures have the same size
            for (int i = 0; i < maskMaps.Length; i++)
            {
                var texture = maskMaps[i];
                if (texture != null)
                {
                    // Check if texture is readable
                    if (!texture.isReadable)
                        // Already notified in the first pass
                        continue;

                    // Size Check
                    if (texture.width != _textureSize.x || texture.height != _textureSize.y)
                    {
                        EditorUtility.DisplayDialog(
                            "Texture Size Mismatch",
                            $"Texture '{texture.name}' size {texture.width}x{texture.height} does not match required size {_textureSize.x}x{_textureSize.y}. It will be ignored.",
                            "OK");

                        // Nullify the mismatched texture
                        switch (i)
                        {
                            case 0: _metallic = null; break;
                            case 1: _ambientOcclusion = null; break;
                            case 2: _detailMask = null; break;
                            case 3: _smoothnessMap = null; break;
                        }
                        Debug.LogWarning($"Texture '{texture.name}' was ignored due to size mismatch.");
                        continue;
                    }

                    // Additional readability check (redundant but safe)
                    if (!texture.isReadable)
                    {
                        Debug.LogError($"Texture '{texture.name}' is not readable. Please enable 'Read/Write Enabled' in the import settings.");
                        EditorUtility.DisplayDialog(
                            "Unreadable Texture",
                            $"The texture '{texture.name}' assigned to '{maskNames[i]}' is not readable. Please enable 'Read/Write Enabled' in the texture's import settings.",
                            "OK");

                        // Nullify the unreadable texture
                        switch (i)
                        {
                            case 0: _metallic = null; break;
                            case 1: _ambientOcclusion = null; break;
                            case 2: _detailMask = null; break;
                            case 3: _smoothnessMap = null; break;
                        }
                        continue;
                    }
                }
            }

            // At this point, all readable textures have the same resolution
        }

        private void CheckTexture()
        {
            bool needsFix = false;

            Texture2D[] maskMaps = { _metallic, _ambientOcclusion, _detailMask, _smoothnessMap };
            for (int i = 0; i < maskMaps.Length; i++)
            {
                var texture = maskMaps[i];
                if (texture == null)
                    continue;

                if (!texture.isReadable || IsCrunchCompressed(texture))
                    needsFix = true;
            }

            string message = "Some textures need to be fixed before packing"
                + "\nClick OK to fix them automatically and continue, or Cancel to abort.";

            if (EditorUtility.DisplayDialog("Fix Texture Import Settings", message, "OK", "Cancel"))
            {
                for (int i = 0; i < maskMaps.Length; i++)
                {
                    var texture = maskMaps[i];
                    if (texture == null)
                        continue;

                    if (!texture.isReadable)
                        FixTextureImportSettings(texture);
                    else if (IsCrunchCompressed(texture))
                        FixCrunchCompression(texture);
                }
                AssetDatabase.Refresh();
            }
        }

        private void UpdateTexture(bool asPreview)
        {
            CheckTexture();

            // Reset textureSize before validation
            _textureSize = Vector2Int.zero;

            // Validate and determine texture size
            ValidateTextureSizes();

            if (_textureSize == Vector2Int.zero)
            {
                EditorUtility.DisplayDialog(
                    "No Valid Textures",
                    "No valid mask map textures are assigned. Please assign at least one mask map texture and ensure all textures are readable and have the same resolution.",
                    "OK");
                return;
            }

            // Create or update the final texture
            if (_finalTexture == null || _finalTexture.width != _textureSize.x || _finalTexture.height != _textureSize.y)
            {
                _finalTexture = new Texture2D(_textureSize.x, _textureSize.y, TextureFormat.RGBA32, false, false)
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
                Debug.Log($"Created finalTexture with size: {_textureSize.x}x{_textureSize.y}");
            }

            // Prepare pixel data
            Color[] pixels = new Color[_textureSize.x * _textureSize.y];
            for (int y = 0; y < _textureSize.y; y++)
            {
                for (int x = 0; x < _textureSize.x; x++)
                {
                    float r = (_metallic != null && _metallic.isReadable) ? GetPixelValue(_metallic, x, y) : DefaultMetallic;
                    float g = (_ambientOcclusion != null && _ambientOcclusion.isReadable) ? GetPixelValue(_ambientOcclusion, x, y) : DefaultAO;
                    float b = (_detailMask != null && _detailMask.isReadable) ? GetPixelValue(_detailMask, x, y) : DefaultDetail;
                    float a = 0f;

                    if (_smoothnessMap != null && _smoothnessMap.isReadable)
                        a = _invertSmoothness ? 1f - GetPixelValue(_smoothnessMap, x, y) : GetPixelValue(_smoothnessMap, x, y);
                    else
                        a = _invertSmoothness ? 1f - DefaultSmoothness : DefaultSmoothness;

                    pixels[y * _textureSize.x + x] = new Color(r, g, b, a);
                }
            }

            _finalTexture.SetPixels(pixels);
            _finalTexture.Apply();

            //Debug.Log("Final texture updated.");
        }

        private string GetDefaultFolderPath()
        {
            // Choose base texture in order of priority
            Texture2D baseTexture = _metallic != null ? _metallic :
                                    _ambientOcclusion != null ? _ambientOcclusion :
                                    _detailMask != null ? _detailMask :
                                    _smoothnessMap;

            if (baseTexture != null)
            {
                string assetPath = AssetDatabase.GetAssetPath(baseTexture);
                if (!string.IsNullOrEmpty(assetPath))
                {
                    string folderPath = Path.GetDirectoryName(assetPath);

                    // Replace backslashes with forward slashes for Unity compatibility
                    folderPath = folderPath.Replace("\\", "/");

                    // Ensure the folder path starts with "Assets/"
                    if (!folderPath.StartsWith("Assets/"))
                        folderPath = "Assets";

                    return folderPath;
                }
            }

            // Default to project root if no textures are assigned
            return "Assets";
        }

        private string GetDefaultFileName()
        {
            // Choose base texture in order of priority
            Texture2D baseTexture = _metallic != null ? _metallic :
                                    _ambientOcclusion != null ? _ambientOcclusion :
                                    _detailMask != null ? _detailMask :
                                    _smoothnessMap;

            if (baseTexture != null)
            {
                string assetPath = AssetDatabase.GetAssetPath(baseTexture);
                if (!string.IsNullOrEmpty(assetPath))
                {
                    string fileName = Path.GetFileNameWithoutExtension(assetPath);
                    string baseName = fileName;

                    // Check if the filename already ends with "Mask"
                    if (!baseName.EndsWith("Mask"))
                        baseName = RemoveLastWord(baseName) + "Mask";

                    return baseName;
                }
            }

            // Default name if no textures are assigned
            return "LitMask";
        }

        private string RemoveLastWord(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            // Check for delimiters: '_', '-', ' '
            int lastDelimiterIndex = -1;
            char lastDelimiter = '\0';
            foreach (char delimiter in new char[] { '_', '-', ' ' })
            {
                int index = input.LastIndexOf(delimiter);
                if (index > lastDelimiterIndex)
                {
                    lastDelimiterIndex = index;
                    lastDelimiter = delimiter;
                }
            }

            if (lastDelimiterIndex != -1)
            {
                // Remove the last word including the delimiter
                // e.g., "Metallic_Texture" -> "Metallic_"
                return input.Substring(0, lastDelimiterIndex + 1);
            }
            else
            {
                // Handle PascalCase: remove the last PascalCase word
                // For example, "MetallicTexture" -> "Metallic"
                // Use regex to split at uppercase letters
                MatchCollection matches = Regex.Matches(input, "[A-Z][a-z]*");
                if (matches.Count > 1)
                {
                    // Exclude the last match
                    string baseName = "";
                    for (int i = 0; i < matches.Count - 1; i++)
                        baseName += matches[i].Value;
                    return baseName;
                }
                else
                {
                    // If no clear PascalCase split, remove the last character as a fallback
                    return input.Length > 1 ? input.Substring(0, input.Length - 1) : input;
                }
            }
        }

        /// <summary>
        /// Opens the texture's import settings and enables Read/Write.
        /// </summary>
        private void FixTextureImportSettings(Texture2D texture)
        {
            if (texture == null)
            {
                EditorUtility.DisplayDialog("No Texture Selected", "Please assign a texture first.", "OK");
                return;
            }

            string assetPath = AssetDatabase.GetAssetPath(texture);
            if (string.IsNullOrEmpty(assetPath))
            {
                EditorUtility.DisplayDialog("Invalid Texture", "Cannot find the texture in the project.", "OK");
                return;
            }

            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer != null)
            {
                importer.isReadable = true;
                importer.SaveAndReimport();
                EditorUtility.DisplayDialog("Import Settings Updated", $"Enabled Read/Write for '{texture.name}'.", "OK");
            }
            else EditorUtility.DisplayDialog("Error", "Failed to retrieve TextureImporter.", "OK");
        }

        private void FixCrunchCompression(Texture2D texture)
        {
            if (texture == null)
            {
                EditorUtility.DisplayDialog("No Texture Selected", "Please assign a texture first.", "OK");
                return;
            }

            string assetPath = AssetDatabase.GetAssetPath(texture);
            if (string.IsNullOrEmpty(assetPath))
            {
                EditorUtility.DisplayDialog("Invalid Texture", "Cannot find the texture in the project.", "OK");
                return;
            }

            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer != null)
            {
                importer.crunchedCompression = false; // Only disable Crunch
                importer.isReadable = true;
                importer.SaveAndReimport();
                EditorUtility.DisplayDialog("Import Settings Updated", $"Disabled Crunch compression and enabled Read/Write for '{texture.name}'.", "OK");
            }
            else EditorUtility.DisplayDialog("Error", "Failed to retrieve TextureImporter.", "OK");
        }

        private static bool IsCrunchCompressed(Texture2D texture)
        {
            if (texture == null) return false;
            var format = texture.format;
            return format == TextureFormat.DXT1Crunched ||
                   format == TextureFormat.DXT5Crunched ||
                   format == TextureFormat.ETC2_RGBA8Crunched;
        }
    }
}
#endif