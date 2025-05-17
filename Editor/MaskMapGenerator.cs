using UnityEngine;
using UnityEditor;
using System.IO;
using System.Text.RegularExpressions;

namespace UnityEssentials
{
    public class MaskMapGenerator : EditorWindow
    {
        // Mask Map Textures
        private Texture2D _metallic;
        private Texture2D _ambientOcclusion;
        private Texture2D _detailMask;
        private Texture2D _smoothnessMap;

        // Default Values
        private float _defaultMetal = 0.5f;
        private float _defaultAO = 1f;
        private float _defaultDetail = 0.5f;
        private float _defaultSmoothness = 0.5f;

        // Processed Texture
        private Texture2D _finalTexture;

        // State Variables
        private bool _isRoughnessMap = false;
        private Vector2Int _textureSize = Vector2Int.zero;
        private Vector2 _scrollPosition;

        // Foldout Sections
        private bool _showTextureInputs = true;
        private bool _showPreviewOptions = true;
        private bool _showFinalTexturePreview = true;

        [MenuItem("Tools/Mask Map Packer")]
        public static void ShowWindow()
        {
            var window = GetWindow<MaskMapGenerator>("Mask Map Packer");
            window.minSize = new Vector2(400, 600);
        }

        [MenuItem("Assets/Mask Map Packer")]
        public static void ShowWindowContext()
        {
            var window = GetWindow<MaskMapGenerator>();
            window.minSize = new Vector2(400, 300);
        }

        public void OnGUI()
        {
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition, false, true);

            GUILayout.Space(10f);

            // Begin Horizontal Layout to add fixed space on the right
            GUILayout.BeginHorizontal();

            // Begin Vertical Layout for main content
            GUILayout.BeginVertical();

            // Header with Important Note
            GUILayout.Label("Combine grayscale mask maps into a single RGBA texture:");
            GUILayout.Space(10f);

            // Texture Inputs Section
            _showTextureInputs = EditorGUILayout.Foldout(_showTextureInputs, "Texture Inputs", true);
            if (_showTextureInputs)
            {
                EditorGUI.indentLevel++;
                GUILayout.BeginVertical("box");
                GUILayout.Space(5f);

                // Metallic
                GUILayout.BeginVertical("box");
                GUILayout.Label("Metallic (R Channel)", EditorStyles.boldLabel);
                GUILayout.BeginHorizontal();

                _metallic = (Texture2D)EditorGUILayout.ObjectField(
                    new GUIContent(
                        "Metallic Texture",
                        "Grayscale texture for Metallic (R channel)"),
                    _metallic, typeof(Texture2D), false);

                // Conditionally show the Fix button
                if (_metallic != null && !_metallic.isReadable)
                    if (GUILayout.Button("Fix", GUILayout.Width(40)))
                        FixTextureImportSettings(_metallic);

                GUILayout.EndHorizontal();

                if (_metallic == null)
                {
                    GUILayout.Label(
                        new GUIContent(
                            "No Metallic texture assigned. Use the slider below to set a default value.",
                            "Default value for Metallic channel."),
                        EditorStyles.helpBox);

                    _defaultMetal = EditorGUILayout.Slider(
                        new GUIContent(
                            "Default Metallic",
                            "Default Metallic value if no texture is assigned."),
                        _defaultMetal, 0f, 1f);
                }
                GUILayout.EndVertical();
                GUILayout.Space(5f);

                // Ambient Occlusion
                GUILayout.BeginVertical("box");
                GUILayout.Label("Ambient Occlusion (G Channel)", EditorStyles.boldLabel);
                GUILayout.BeginHorizontal();

                _ambientOcclusion = (Texture2D)EditorGUILayout.ObjectField(
                    new GUIContent(
                        "Ambient Occlusion Texture",
                        "Grayscale texture for Ambient Occlusion (G channel)"),
                    _ambientOcclusion, typeof(Texture2D), false);

                // Conditionally show the Fix button
                if (_ambientOcclusion != null && !_ambientOcclusion.isReadable)
                    if (GUILayout.Button("Fix", GUILayout.Width(40)))
                        FixTextureImportSettings(_ambientOcclusion);

                GUILayout.EndHorizontal();

                if (_ambientOcclusion == null)
                {
                    GUILayout.Label(
                        new GUIContent(
                            "No Ambient Occlusion texture assigned. Use the slider below to set a default value.",
                            "Default value for Ambient Occlusion channel."),
                        EditorStyles.helpBox);

                    _defaultAO = EditorGUILayout.Slider(
                        new GUIContent(
                            "Default AO",
                            "Default Ambient Occlusion value if no texture is assigned."),
                        _defaultAO, 0f, 1f);
                }
                GUILayout.EndVertical();
                GUILayout.Space(5f);

                // Detail Mask
                GUILayout.BeginVertical("box");
                GUILayout.Label("Detail Mask (B Channel)", EditorStyles.boldLabel);
                GUILayout.BeginHorizontal();

                _detailMask = (Texture2D)EditorGUILayout.ObjectField(
                    new GUIContent(
                        "Detail Mask Texture",
                        "Grayscale texture for Detail Mask (B channel)"),
                    _detailMask, typeof(Texture2D), false);

                // Conditionally show the Fix button
                if (_detailMask != null && !_detailMask.isReadable)
                    if (GUILayout.Button("Fix", GUILayout.Width(40)))
                        FixTextureImportSettings(_detailMask);

                GUILayout.EndHorizontal();

                if (_detailMask == null)
                {
                    GUILayout.Label(
                        new GUIContent(
                            "No Detail Mask texture assigned. Use the slider below to set a default value.",
                            "Default value for Detail Mask channel."),
                        EditorStyles.helpBox);

                    _defaultDetail = EditorGUILayout.Slider(
                        new GUIContent(
                            "Default Detail Mask",
                            "Default Detail Mask value if no texture is assigned."),
                        _defaultDetail, 0f, 1f);
                }
                GUILayout.EndVertical();
                GUILayout.Space(5f);

                // Smoothness/Roughness
                GUILayout.BeginVertical("box");
                GUILayout.Label(_isRoughnessMap ? "Roughness (A Channel)" : "Smoothness (A Channel)", EditorStyles.boldLabel);
                _isRoughnessMap = EditorGUILayout.Toggle(
                    new GUIContent(
                        "Is Roughness Map",
                        "Toggle to indicate if the input texture is a Roughness map instead of Smoothness."),
                    _isRoughnessMap);

                GUILayout.BeginHorizontal();

                _smoothnessMap = (Texture2D)EditorGUILayout.ObjectField(
                    new GUIContent(
                        _isRoughnessMap
                            ? "Roughness Texture"
                            : "Smoothness Texture",
                        _isRoughnessMap
                            ? "Grayscale texture for Roughness (A channel)"
                            : "Grayscale texture for Smoothness (A channel)"),
                    _smoothnessMap, typeof(Texture2D), false);

                // Conditionally show the Fix button
                if (_smoothnessMap != null && !_smoothnessMap.isReadable)
                    if (GUILayout.Button("Fix", GUILayout.Width(40)))
                        FixTextureImportSettings(_smoothnessMap);

                GUILayout.EndHorizontal();

                if (_smoothnessMap == null)
                {
                    GUILayout.Label(
                        new GUIContent(
                            $"No {(_isRoughnessMap ? "Roughness" : "Smoothness")} texture assigned. Use the slider below to set a default value.",
                            $"Default value for {(_isRoughnessMap ? "Roughness" : "Smoothness")} channel."),
                        EditorStyles.helpBox);
                    _defaultSmoothness = EditorGUILayout.Slider(
                        new GUIContent(
                            $"Default {(_isRoughnessMap ? "Roughness" : "Smoothness")}",
                            $"Default {(_isRoughnessMap ? "Roughness" : "Smoothness")} value if no texture is assigned."),
                        _defaultSmoothness, 0f, 1f);

                    if (_defaultSmoothness == 0 && _isRoughnessMap)
                        GUILayout.Label(
                            new GUIContent(
                                "Roughness set to 0 means Smoothness is 1.",
                                "Roughness value is inverted in the final texture."),
                            EditorStyles.helpBox);
                }
                GUILayout.EndVertical();
                GUILayout.Space(5f);

                GUILayout.EndVertical();
                EditorGUI.indentLevel--;
            }

            GUILayout.Space(10f);

            // Preview and Actions Section
            _showPreviewOptions = EditorGUILayout.Foldout(_showPreviewOptions, "Actions", true);
            if (_showPreviewOptions)
            {
                EditorGUI.indentLevel++;
                GUILayout.BeginVertical("box");
                GUILayout.Space(5f);

                // Action Buttons
                GUILayout.BeginHorizontal();

                if (GUILayout.Button(new GUIContent("Update Preview Texture", "Generate the preview texture based on current inputs.")))
                {
                    EditorUtility.DisplayProgressBar("Generating Preview", "Please wait...", 0.5f);
                    UpdateTexture(asPreview: true);
                    EditorUtility.ClearProgressBar();
                }

                if (GUILayout.Button(new GUIContent("Pack and Save Texture", "Combine inputs and save the final mask texture.")))
                {
                    EditorUtility.DisplayProgressBar("Packing Textures", "Please wait...", 0.5f);
                    PackTextures();
                    EditorUtility.ClearProgressBar();
                }

                if (GUILayout.Button(new GUIContent("Clear All", "Reset all inputs and outputs.")))
                    ClearAll();

                GUILayout.EndHorizontal();

                GUILayout.Space(10f);
                GUILayout.EndVertical();
                EditorGUI.indentLevel--;
            }

            GUILayout.Space(10f);

            // Final Texture Preview Section
            _showFinalTexturePreview = EditorGUILayout.Foldout(_showFinalTexturePreview, "Final Texture Preview", true);
            if (_showFinalTexturePreview && _finalTexture != null)
            {
                GUILayout.BeginVertical("box");
                GUILayout.Label("Final Mask Map", EditorStyles.boldLabel);
                GUILayout.Space(5f);
                // Display the texture at a scaled-down size for preview
                GUILayout.Label(_finalTexture, GUILayout.Width(256), GUILayout.Height(256));
                GUILayout.EndVertical();
                GUILayout.Space(10f);
            }

            // Footer
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label(
                new GUIContent(
                    "Ensure all textures are the same size, Read/Write enabled, and have sRGB correctly set in import settings."),
                EditorStyles.helpBox);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.EndVertical(); // End of main vertical layout

            // Add fixed 5-pixel space on the right
            GUILayout.Space(5f);

            GUILayout.EndHorizontal(); // End of horizontal layout

            GUILayout.EndScrollView();
        }

        /// <summary>
        /// Packs the textures and saves the final mask map as a PNG.
        /// Modified to start save dialog in the folder of the first input texture and name it accordingly.
        /// </summary>
        private void PackTextures()
        {
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

        /// <summary>
        /// Clears all assigned textures and resets default values.
        /// </summary>
        private void ClearAll()
        {
            if (EditorUtility.DisplayDialog("Clear All", "Are you sure you want to clear all inputs and outputs?", "Yes", "No"))
            {
                _metallic = null;
                _ambientOcclusion = null;
                _detailMask = null;
                _smoothnessMap = null;

                _defaultMetal = 0.5f;
                _defaultAO = 1f;
                _defaultDetail = 0.5f;
                _defaultSmoothness = 0.5f;

                _finalTexture = null;
                _textureSize = Vector2Int.zero;
                Repaint();

                Debug.Log("All inputs and outputs have been cleared.");
            }
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

        private void UpdateTexture(bool asPreview)
        {
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
                    float r = (_metallic != null && _metallic.isReadable) ? GetPixelValue(_metallic, x, y) : _defaultMetal;
                    float g = (_ambientOcclusion != null && _ambientOcclusion.isReadable) ? GetPixelValue(_ambientOcclusion, x, y) : _defaultAO;
                    float b = (_detailMask != null && _detailMask.isReadable) ? GetPixelValue(_detailMask, x, y) : _defaultDetail;
                    float a = 0f;

                    if (_smoothnessMap != null && _smoothnessMap.isReadable)
                        a = _isRoughnessMap ? 1f - GetPixelValue(_smoothnessMap, x, y) : GetPixelValue(_smoothnessMap, x, y);
                    else
                        a = _isRoughnessMap ? 1f - _defaultSmoothness : _defaultSmoothness;

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
    }
}