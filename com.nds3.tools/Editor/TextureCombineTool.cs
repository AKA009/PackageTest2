using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace EditorExt.Tools
{
    public class TextureCombineTool: EditorWindow
    {
        [MenuItem("[美术用]/S3插件/TextureCombineTool")]
        private static void Init()
        {
            EditorWindow window = GetWindow(typeof(TextureCombineTool));
            window.titleContent = new GUIContent("TextureCombineTool");
        }

        private string outputPath;
        private string outputTextureName;
        private int modeIndex;
        private int[] channelRefArray = new int[4];
        private Texture2D[] texArray = new Texture2D[4];

        void OnGUI()
        {
            EditorExtUtil.UI_PathField("生成文件路径", ref outputPath);

            GUILayout.Label("生成文件名");
            outputTextureName = EditorGUILayout.TextField(outputTextureName);
            GUILayout.Space(10);

            modeIndex = GUILayout.Toolbar(modeIndex, new[] { "RGB+A", "R+G+B+A" }, GUILayout.Height(20), GUILayout.ExpandWidth(true));

            if (modeIndex == 0)
            {
                UI_TextureSlot(0, "RGB Texture", false);
                UI_TextureSlot(3, "Alpha Texture", true);
            }
            else if (modeIndex == 1)
            {
                UI_TextureSlot(0, "Red Texture", true);
                UI_TextureSlot(1, "Green Texture", true);
                UI_TextureSlot(2, "Blue Texture", true);
                UI_TextureSlot(3, "Alpha Texture", true);
            }


            if (GUILayout.Button("Do Combine"))
            {
                Texture2D final = null;
                if (modeIndex == 0)
                {
                    texArray[1] = texArray[0];
                    texArray[2] = texArray[0];
                    channelRefArray[0] = 0;
                    channelRefArray[1] = 1;
                    channelRefArray[2] = 2;
                    final = CombineRGBASameSize(texArray, channelRefArray);
                }
                else if (modeIndex == 1)
                {
                    final = CombineRGBASameSize(texArray, channelRefArray);
                }
                    
                EditorExtUtil.SaveTexture2DtoFile(final, outputPath, outputTextureName, EditorExtUtil.SavingTextureFormat.TGA, true);
                AssetDatabase.Refresh();
            }
        }

        /// <summary>
        /// Combine a color texture and an alpha texture, both must be the same size and readable
        /// </summary>
        /// <param name="rgb">RGB channel as final RGB</param>
        /// <param name="a">Red channel as final alpha</param>
        /// <returns></returns>
        public static Texture2D CombineRGBAndASameSize(Texture2D rgb, Texture2D a)
        {
            return CombineRGBASameSize(new[] {rgb, rgb, rgb, a}, new[] {0, 1, 2, 1});
        }

        /// <summary>
        /// All textures must be the same size and readable
        /// </summary>
        /// <param name="sourceTextures">Index 0123 for final channel RGBA</param>
        /// <param name="sourceChannelRef">Which channel to read from each source texture</param>
        /// <returns>Final texture</returns>
        public static Texture2D CombineRGBASameSize(Texture2D[] sourceTextures, int[] sourceChannelRef)
        {
            //for (int i = 0; i < 4; i++)
            //{
            //    Debug.Log($"Combine {sourceTextures[i].name}.{sourceChannelRef[i]} as {i}");
            //}

            int width = sourceTextures[0].width;
            int height = sourceTextures[0].height;

            Texture2D temp = new Texture2D(width, height);
            for (int w = 0; w < width; w++)
            {
                for (int h = 0; h < height; h++)
                {
                    Color c = new Color
                    (
                    sourceTextures[0].GetPixel(w, h)[sourceChannelRef[0]],
                    sourceTextures[1].GetPixel(w, h)[sourceChannelRef[1]],
                    sourceTextures[2].GetPixel(w, h)[sourceChannelRef[2]],
                    sourceTextures[3].GetPixel(w, h)[sourceChannelRef[3]]
                    );
                    temp.SetPixel(w, h, c);
                }
            }

            return temp;
        }

        private void UI_TextureSlot(int index, string label, bool showChannelToolbar)
        {
            texArray[index] = EditorGUILayout.ObjectField(label, texArray[index], typeof(Texture2D), false) as Texture2D;
            if (showChannelToolbar)
            {
                channelRefArray[index] = UI_ChannelToolbar(channelRefArray[index]);
            }
        }

        private int UI_ChannelToolbar(int value)
        {
            Rect r = GUILayoutUtility.GetLastRect();
            r.y += 18;
            r.width = 120;
            r.height = 20;
            return GUI.Toolbar(r, value, new[] { "R", "G", "B", "A" });
        }
    }
}