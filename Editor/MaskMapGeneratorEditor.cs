#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace UnityEssentials
{
    public partial class MaskMapGenerator
    {
        public EditorWindowDrawer Window;
        public Action Repaint;
        public Action Close;

        private bool _invertSmoothness = false;

        [MenuItem("Tools/Mask Map Packer %t", priority = 1002)]
        public static void ShowWindow()
        {
            var editor = new MaskMapGenerator();
            editor.Window = new EditorWindowDrawer("Mask Map Packer", new(400, 650))
                .SetHeader(editor.Header, EditorWindowStyle.Toolbar)
                .SetBody(editor.Body, EditorWindowStyle.Margin)
                .SetFooter(editor.Footer, EditorWindowStyle.HelpBox)
                .GetRepaintEvent(out editor.Repaint)
                .GetCloseEvent(out editor.Close)
                .ShowUtility();

            editor.Window.maxSize = new Vector2(400, 650);
        }

        private void Header()
        {
            if (GUILayout.Button(new GUIContent("Update Preview Texture", "Generate preview texture"), EditorStyles.toolbarButton))
            {
                EditorUtility.DisplayProgressBar("Generating Preview", "Please wait...", 0.5f);
                TryGenerateTexture(true);
                EditorUtility.ClearProgressBar();
            }

            if (GUILayout.Button(new GUIContent("Pack and Save Texture", "Combine and save mask texture"), EditorStyles.toolbarButton))
            {
                EditorUtility.DisplayProgressBar("Packing Textures", "Please wait...", 0.5f);
                PackTextures();
                EditorUtility.ClearProgressBar();
            }

            if (GUILayout.Button(new GUIContent("Clear", "Reset all inputs and outputs"), EditorStyles.toolbarButton))
            {
                ClearAll();
                GUI.FocusControl(null);
            }
        }

        private void Body()
        {
            GUILayout.Space(10);

            DrawTextureField("Metallic (R Channel)", ref _metallic);

            DrawTextureField("AO (G Channel)", ref _ambientOcclusion);

            DrawTextureField("Detail Mask (B Channel)", ref _detailMask);

            // For smoothness/roughness, show the invert toggle
            DrawTextureField("Smoothness (A Channel)", ref _smoothnessMap, true);
        }

        private void DrawTextureField(string title, ref Texture2D texture, bool showInvert = false)
        {
            GUILayout.Label(title, EditorStyles.boldLabel);

            GUILayout.Space(-19);
            texture = (Texture2D)EditorGUILayout.ObjectField(
                GUIContent.none,
                texture, typeof(Texture2D), false);

            if (texture != null)
                GUILayout.Space(20);

            if (texture == null)
            {
                string propertyName = $"Default{title.Split(' ')[0].Replace("(", "")}";
                float value = (float)typeof(MaskMapGenerator).GetField(propertyName).GetValue(this);
                value = EditorGUILayout.Slider(GUIContent.none, value, 0f, 1f);
                typeof(MaskMapGenerator).GetField(propertyName).SetValue(this, value);
            }

            if (showInvert)
            {
                _invertSmoothness = EditorGUILayout.Toggle(
                    new GUIContent("Invert Smoothness", "Invert the smoothness/roughness map (Roughness = 1 - Smoothness)"),
                    _invertSmoothness);
            }
            else GUILayout.Space(25f);
        }

        private void Footer()
        {
            if (_finalTexture != null)
            {
                GUILayout.Label($" {GetDefaultFileName()}.png", EditorStyles.label);

                using (new GUILayout.HorizontalScope())
                {
                    GUILayout.Label(_finalTexture, GUILayout.Width(128), GUILayout.Height(128));
                    using (new GUILayout.VerticalScope())
                    {
                        GUILayout.Space(4);
                        GUILayout.Label($"Size: {_finalTexture.width} x {_finalTexture.height}", EditorStyles.miniLabel);
                        GUILayout.Label($"Format: {_finalTexture.format}", EditorStyles.miniLabel);
                        GUILayout.Space(2);
                        GUILayout.Label($"MipMap: {_finalTexture.mipmapCount}", EditorStyles.miniLabel);
                        GUILayout.Label($"sRGB: {_finalTexture.isDataSRGB}", EditorStyles.miniLabel);
                        GUILayout.Label($"Is Readable: {_finalTexture.isReadable}", EditorStyles.miniLabel);
                        GUILayout.Label($"Wrap Mode: {_finalTexture.wrapMode}", EditorStyles.miniLabel);
                        GUILayout.Label($"Filter Mode: {_finalTexture.filterMode}", EditorStyles.miniLabel);
                        GUILayout.Label($"Aniso Level: {_finalTexture.anisoLevel}", EditorStyles.miniLabel);
                    }
                }
            }
            else
            {
                string footerInfoText =
                    "Ensure all textures have matching dimensions and Read/Write enabled,"
                    + "\nsRGB in their import settings for best results.";
                GUILayout.Label(footerInfoText, EditorStyles.wordWrappedMiniLabel);
            }
        }
    }
}
#endif