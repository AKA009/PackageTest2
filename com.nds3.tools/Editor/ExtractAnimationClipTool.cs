using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Object = UnityEngine.Object;

namespace EditorExt.Tools
{
    /// <summary>
    /// Extract AnimationClips from a FBX file
    /// </summary>
    public sealed class ExtractAnimationClipTool
    {
        ////TODO: 性能问题移除

        //[MenuItem("Assets/ExtractAnimationClip", true)]
        //[MenuItem("Assets/ExtractAnimationClipAndDeleteFBX", true)]
        //[MenuItem("[美术用]/S3插件/ExtractAnimationClip", true)]
        //[MenuItem("[美术用]/S3插件/ExtractAnimationClipAndDeleteFBX", true)]
        //public static bool CheckSelectionFileFormat()
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

        //        if (!string.Equals(Path.GetExtension(path), ".fbx", StringComparison.OrdinalIgnoreCase))
        //        {
        //            return false;
        //        }
        //    }

        //    return true;
        //}

        [MenuItem("Assets/ExtractAnimationClip")]
        [MenuItem("S3插件/ExtractAnimationClip")]
        public static List<string> DoExtract()
        {
            Object[] SelectionAsset = Selection.GetFiltered(typeof(Object), SelectionMode.DeepAssets);

            if (SelectionAsset.Length <= 0)
            {
                return null;
            }

            for (int i = 0; i < SelectionAsset.Length; i++)
            {
                Object asset = SelectionAsset[i];
                string path = AssetDatabase.GetAssetPath(asset);

                if (!string.Equals(Path.GetExtension(path), ".fbx", StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }
            }

            Debug.Log("ExtractAnimationClip -> DoExtract");

            List<string> successPathList = new List<string>();
            for (int i = 0; i < SelectionAsset.Length; i++)
            {
                Object asset = SelectionAsset[i];
                string path = AssetDatabase.GetAssetPath(asset);

                //Debug.Log("ExtractAnimationClip -> FBX: " + path);

                Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(path);
                List<AnimationClip> clips = new List<AnimationClip>();
                for (int j = 0; j < subAssets.Length; j++)
                {
                    Object sa = subAssets[j];
                    //Debug.Log("subAssets[" + j + "] name: " + sa.name + ", type: " + sa.GetType() + ", is:" + (sa is AnimationClip));

                    //Merged type check and type cast
                    //Remove the internal clip named as "__preview__xxx"
                    if (sa is AnimationClip clip && !clip.name.StartsWith("__"))
                    {
                        clips.Add(clip);
                    }
                }
                
                if (clips.Count <= 0)
                {
                    Debug.LogError("ExtractAnimationClip -> AnimationClip is null at " + path);
                    continue;
                }

                if (clips.Count == 1)
                {
                    AnimationClip newClip = new AnimationClip();
                    EditorUtility.CopySerialized(clips[0], newClip);
                    string newPath = Path.GetDirectoryName(path) + "\\" + asset.name + ".anim";
                    AssetDatabase.CreateAsset(newClip, newPath);
                }
                else
                {
                    //Extract all ac to file without @, then rename them to FBXName@ACName
                    //Import them all at once for saving a lot of time
                    for (int j = 0; j < clips.Count; j++)
                    {
                        AnimationClip newClip = new AnimationClip();
                        EditorUtility.CopySerialized(clips[j], newClip);

                        string head = Path.GetDirectoryName(path) + "\\";
                        string fileName1 = asset.name + "_" + clips[j].name + ".anim";
                        string fileName2 = asset.name + "@" + clips[j].name + ".anim";

                        EditorUtility.DisplayProgressBar("ExtractAnimationClip", "-> " + fileName2, (float) (j + 1) / clips.Count);
                        AssetDatabase.CreateAsset(newClip, head + fileName1);

                        File.Delete(head + fileName2);
                        File.Delete(head + fileName2 + ".meta");
                        File.Move(head + fileName1, head + fileName2);
                        File.Move(head + fileName1 + ".meta", head + fileName2 + ".meta");
                    }
                    EditorUtility.ClearProgressBar();
                }
                successPathList.Add(path);

                //Debug.Log("ExtractAnimationClip -> Extract: " + path);
            }
            AssetDatabase.Refresh();
            Debug.Log("ExtractAnimationClip -> Total FBX: " + SelectionAsset.Length + ", Success: " + successPathList.Count);

            return successPathList;
        }

        [MenuItem("Assets/ExtractAnimationClipAndDeleteFBX")]
        [MenuItem("S3插件/ExtractAnimationClipAndDeleteFBX")]
        public static void DoExtractAndDelete()
        {
            List<string> successPathList = DoExtract();
            if (successPathList == null)
            {
                return;
            }

            for (int i = 0; i < successPathList.Count; i++)
            {
                AssetDatabase.DeleteAsset(successPathList[i]);
            }

            Debug.Log("ExtractAnimationClip -> Delete FBX Done");
        }
    }
}