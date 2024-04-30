// Unity C# reference source
// Copyright (c) Unity Technologies. For terms of use, see
// https://unity3d.com/legal/licenses/Unity_Reference_Only_License

using System.Globalization;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;

namespace UnityEditor
{
    [CustomEditor(typeof(Cubemap))]
    internal class CubemapInspector : TextureInspector
    {
        static private readonly string[] kSizes = { "16", "32", "64", "128" , "256" , "512" , "1024" , "2048" };
        static private readonly int[] kSizesValues = { 16, 32, 64, 128, 256, 512, 1024, 2048 };
        const int kTextureSize = 64;

        private static readonly string kNativeTextureNotice = L10n.Tr("External texture: Unity cannot make changes to this Cubemap.");

        private Texture2D[] m_Images;

        protected override void OnDisable()
        {
            base.OnDisable();

            if (m_Images != null)
            {
                for (int i = 0; i < m_Images.Length; ++i)
                {
                    if (m_Images[i] && !EditorUtility.IsPersistent(m_Images[i]))
                        DestroyImmediate(m_Images[i]);
                }
            }
            m_Images = null;
        }

        private void InitTexturesFromCubemap()
        {
            var c = target as Cubemap;
            if (c is null || c.isNativeTexture)
            {
                return;
            }

            if (m_Images == null)
                m_Images = new Texture2D[6];
            for (int i = 0; i < m_Images.Length; ++i)
            {
                if (m_Images[i] && !EditorUtility.IsPersistent(m_Images[i]))
                    DestroyImmediate(m_Images[i]);

                if (TextureUtil.GetSourceTexture(c, (CubemapFace)i))
                {
                    m_Images[i] = TextureUtil.GetSourceTexture(c, (CubemapFace)i);
                }
                else
                {
                    // When the Cubemap is compressed, avoid "CopyCubemapFaceIntoTexture" due to potentially very high decompression cost. (example: Cubemap with no mipmaps)
                    // Note: the CopyTexture approach may produce results that look slightly different if "CopyCubemapFaceIntoTexture" would have downscaled to kTextureSize.
                    if (GraphicsFormatUtility.IsCompressedFormat(c.format) && SystemInfo.copyTextureSupport.HasFlag(CopyTextureSupport.DifferentTypes))
                    {
                        int previewSize = System.Math.Clamp(kTextureSize, c.width >> (c.mipmapCount - 1), c.width);
                        m_Images[i] = new Texture2D(previewSize, previewSize, c.format, false);
                        m_Images[i].hideFlags = HideFlags.HideAndDontSave;

                        int mipToCopy = (int)(System.Math.Log(c.width, 2) - System.Math.Log(previewSize, 2));
                        Graphics.CopyTexture(c, i, mipToCopy, m_Images[i], 0, 0);
                    }
                    else
                    {
                        m_Images[i] = new Texture2D(kTextureSize, kTextureSize, TextureFormat.RGBA32, false);
                        m_Images[i].hideFlags = HideFlags.HideAndDontSave;
                        TextureUtil.CopyCubemapFaceIntoTexture(c, (CubemapFace)i, m_Images[i]);
                    }
                }
            }
        }

        public override void OnInspectorGUI()
        {
            var c = target as Cubemap;
            if (c == null)
                return;

            if (c.isNativeTexture)
            {
                EditorGUILayout.HelpBox(kNativeTextureNotice, MessageType.Info);
                return;
            }

            if (m_Images == null)
                InitTexturesFromCubemap();

            EditorGUIUtility.labelWidth = 50;

            GUILayout.BeginVertical();

            GUILayout.BeginHorizontal();
            ShowFace("Right\n(+X)", CubemapFace.PositiveX);
            ShowFace("Left\n(-X)", CubemapFace.NegativeX);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            ShowFace("Top\n(+Y)", CubemapFace.PositiveY);
            ShowFace("Bottom\n(-Y)", CubemapFace.NegativeY);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            ShowFace("Front\n(+Z)", CubemapFace.PositiveZ);
            ShowFace("Back\n(-Z)", CubemapFace.NegativeZ);
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();

            EditorGUIUtility.labelWidth = 0;

            EditorGUILayout.Space();

            EditorGUI.BeginChangeCheck();

            EditorGUILayout.HelpBox("Lowering face size is a destructive operation, you might need to re-assign the textures later to fix resolution issues. It's preferable to use Cubemap texture import type instead of Legacy Cubemap assets.", MessageType.Warning);
            int faceSize = TextureUtil.GetGPUWidth(c);
            faceSize = EditorGUILayout.IntPopup("Face size", faceSize, kSizes, kSizesValues);

            int mipMaps = TextureUtil.GetMipmapCount(c);
            bool useMipMap = EditorGUILayout.Toggle("Generate Mipmap", mipMaps > 1);

            bool streamingMipmaps = TextureUtil.GetCubemapStreamingMipmaps(c);
            if (useMipMap)
            {
                EditorGUI.indentLevel++;
                streamingMipmaps = EditorGUILayout.Toggle(EditorGUIUtility.TrTextContent("Stream Mipmap Levels", "Don't load image data immediately but wait till image data is requested from script."), streamingMipmaps);
                EditorGUI.indentLevel--;
            }

            bool linear = TextureUtil.GetLinearSampled(c);
            linear = EditorGUILayout.Toggle("Linear", linear);

            bool readable = TextureUtil.IsCubemapReadable(c);
            readable = EditorGUILayout.Toggle("Readable", readable);

            if (EditorGUI.EndChangeCheck())
            {
                // reformat the cubemap
                if (TextureUtil.ReformatCubemap(c, faceSize, faceSize, c.format, useMipMap, linear))
                    InitTexturesFromCubemap();

                TextureUtil.MarkCubemapReadable(c, readable);
                TextureUtil.SetCubemapStreamingMipmaps(c, streamingMipmaps);
                c.Apply();
            }
        }

        // A minimal list of settings to be shown in the Asset Store preview inspector
        internal override void OnAssetStoreInspectorGUI()
        {
            OnInspectorGUI();
        }

        private void ShowFace(string label, CubemapFace face)
        {
            var c = target as Cubemap;
            var iface = (int)face;
            GUI.changed = false;

            var tex = (Texture2D)ObjectField(label, m_Images[iface], typeof(Texture2D), false);
            if (GUI.changed)
            {
                if (tex != null)
                {
                    TextureUtil.CopyTextureIntoCubemapFace(tex, c, face);
                }
                // enable this line in order to retain connections from cube faces to their corresponding
                // texture2D assets, this allows auto-update functionality when editing the source texture
                // images
                //TextureUtil.SetSourceTexture(c, face, tex);
                m_Images[iface] = tex;
            }
        }

        // Variation of ObjectField where label is not restricted to one line
        public static Object ObjectField(string label, Object obj, System.Type objType, bool allowSceneObjects, params GUILayoutOption[] options)
        {
            GUILayout.BeginHorizontal();
            Rect r = GUILayoutUtility.GetRect(EditorGUIUtility.labelWidth, EditorGUI.kSingleLineHeight * 2, EditorStyles.label, GUILayout.ExpandWidth(false));
            GUI.Label(r, label, EditorStyles.label);
            r = GUILayoutUtility.GetAspectRect(1, EditorStyles.objectField, GUILayout.Width(64));
            Object retval = EditorGUI.ObjectField(r, obj, objType, allowSceneObjects);
            GUILayout.EndHorizontal();
            return retval;
        }
    }
}
