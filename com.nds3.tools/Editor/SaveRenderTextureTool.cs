//#define DEBUG_ON

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace EditorExt.Tools
{
    public static class SaveRenderTextureTool
    {
        ////TODO: 性能问题移除
        
        //[MenuItem("Assets/SaveRenderTexture", true)]
        //[MenuItem("[美术用]/S3插件/SaveRenderTexture", true)]
        //private static bool CheckSelectionFileFormat()
        //{
        //    Object[] SelectionAsset = Selection.GetFiltered(typeof(Object), SelectionMode.DeepAssets);

        //    if (SelectionAsset.Length <= 0)
        //    {
        //        return false;
        //    }

        //    for (int i = 0; i < SelectionAsset.Length; i++)
        //    {
        //        Object asset = SelectionAsset[i];
        //        string path = AssetDatabase.GetAssetPath(asset);

        //        if (!string.Equals(Path.GetExtension(path), ".renderTexture", StringComparison.OrdinalIgnoreCase))
        //        {
        //            return false;
        //        }
        //    }

        //    return true;
        //}

        [MenuItem("Assets/SaveRenderTexture")]
        [MenuItem("[美术用]/S3插件/SaveRenderTexture")]
        private static void DoSave()
        {
#if DEBUG_ON
            Debug.Log("SaveRenderTextureTool -> DoSave");
#endif
            Object[] SelectionAsset = Selection.GetFiltered(typeof(Object), SelectionMode.DeepAssets);

            if (SelectionAsset.Length <= 0)
            {
                return;
            }

            for (int i = 0; i < SelectionAsset.Length; i++)
            {
                Object asset = SelectionAsset[i];
                string path = AssetDatabase.GetAssetPath(asset);

                if (!string.Equals(Path.GetExtension(path), ".renderTexture", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            for (int i = 0; i < SelectionAsset.Length; i++)
            {
                Object asset = SelectionAsset[i];
                string path = AssetDatabase.GetAssetPath(asset);
#if DEBUG_ON
		        Debug.Log("SaveRenderTextureTool -> RT: " + path); 
#endif
                Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(path);
                
                for (int j = 0; j < subAssets.Length; j++)
                {
                    Object sa = subAssets[j];
                    if (SaveRenderTextureToPNG((RenderTexture) sa, Path.GetDirectoryName(path), asset.name))
                    {
                        Debug.Log("SaveRenderTextureTool -> Save: " + Path.GetDirectoryName(path) + "/" + asset.name + ".png");
                    }
                }
            }
            AssetDatabase.Refresh();
        }

        public static Texture2D SaveRenderTextureToTexture2D(RenderTexture rt)
        {
            if (rt == null)
            {
                return null;
            }

            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            Texture2D texture = new Texture2D(rt.width, rt.height, TextureFormat.ARGB32, false);
            texture.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            RenderTexture.active = prev;

            return texture;
        }

        /// <summary>
        /// 将RenderTexture保存成一张png图片 
        /// </summary>
        public static bool SaveRenderTextureToPNG(RenderTexture rt, string path, string fileName)
        {
            Texture2D png = SaveRenderTextureToTexture2D(rt);
            if (png == null)
            {
                return false;
            }
            EditorExtUtil.SaveTexture2DtoFile(png, path, fileName, EditorExtUtil.SavingTextureFormat.PNG, true);

            return true;
        }
    }
}