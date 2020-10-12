using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace EditorExt.Tools
{
    public class MaterialCleanTool: EditorWindow
    {
        public struct CleanResult
        {
            public int SearchCount;
            public int CleanCount;
            public string DetailInfo;

            public CleanResult(int searchCount, int cleanCount, string detailInfo)
            {
                SearchCount = searchCount;
                CleanCount = cleanCount;
                DetailInfo = detailInfo;
            }
        }

        [MenuItem("S3插件/材质引用清理工具")]
        public static void ShowWindow()
        {
            EditorWindow.GetWindow<MaterialCleanTool>(false, "MaterialCleanTool");
        }


        private bool includePackages = false;
        private bool skipErrorShader = true;
        private Vector2 scroll;

        private string outputInfo = "清理Material记录的无用属性信息\n用途：\n给Material切换Shader的时候，\n上一个Shader的序列化信息不会被删除。\n本工具可以清除这些无用信息，避免出现问题。";
        private CleanResult result;

        private const string c_Dialog_Title = "MaterialCleanTool";

        void OnGUI()
        {
            includePackages = EditorGUILayout.Toggle("Include packages", includePackages);
            skipErrorShader = EditorGUILayout.Toggle("Skip error shader", skipErrorShader);

            EditorGUILayout.BeginHorizontal();
            {
                if (GUILayout.Button("清理", GUILayout.Height(40f)))
                {
                    outputInfo = "正在清理中，请稍候。。。";

                    List<string> searchFolders = new List<string>();
                    searchFolders.Add("Assets");
                    if (includePackages)
                    {
                        searchFolders.Add("Packages");
                    }

                    result = SearchAndCleanUnusedProperties(searchFolders.ToArray(), skipErrorShader, true);

                    outputInfo = $"Total materials: {result.SearchCount}\nCleaned materials: {result.CleanCount}\n" + result.DetailInfo;
                    EditorUtility.DisplayDialog(c_Dialog_Title, $"Cleaned {result.CleanCount} materials", "Ok");
                }
                //if (GUILayout.Button("保存结果到文件", GUILayout.Height(40f)))
                //{
                //    if (isCleanButtonClicked)
                //    {
                //        EditorToolsHelper.SaveStringToLocal("RegisterOnClick查询结果", cleanResultString);
                //        outputInfo = "查询结果已保存至桌面";
                //    }
                //    else
                //    {
                //        outputInfo = "请先查找";
                //    }
                //}
            }
            EditorGUILayout.EndHorizontal();

            //禁止交互、自动扩展大小、自动隐藏滚动条
            scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.Height(this.position.height - 88));
            {
                EditorGUILayout.LabelField(outputInfo, EditorStyles.textArea, GUILayout.ExpandHeight(true));
            }
            EditorGUILayout.EndScrollView();
        }

        /// <summary>
        /// 清理Material记录的无用属性信息
        /// <para>
        /// 用途：给Material切换Shader的时候，上一个Shader的序列化信息不会被删除。本工具可以清除这些无用信息，避免出现问题。
        /// </para>
        /// </summary>
        /// <param name="searchPath">搜索目录（相对路径）</param>
        /// <param name="skipErrorShader"></param>
        /// <param name="showProgressBar"></param>
        public static CleanResult SearchAndCleanUnusedProperties(string[] searchPath, bool skipErrorShader, bool showProgressBar)
        {
            /**
             * REF: http://www.jianshu.com/p/83de2971ea27
             */

            int searchCount = 0;
            int cleanCount = 0;
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("CLEAN LIST (file names)");

            string[] matGUIDs = AssetDatabase.FindAssets("t:Material", searchPath);
            searchCount = matGUIDs.Length;
            for (int i = 0; i < matGUIDs.Length; i++)
            {
                if (showProgressBar)
                {
                    EditorUtility.DisplayProgressBar(c_Dialog_Title, $"Cleaning: {i + 1}/{searchCount}", (float) (i + 1) / searchCount);
                }

                string guid = matGUIDs[i];
                string matPath = AssetDatabase.GUIDToAssetPath(guid);
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                bool changed = CleanSingle(mat, skipErrorShader);
                if (changed)
                {
                    cleanCount++;
                    sb.AppendLine("- " + matPath);
                }
            }

            AssetDatabase.SaveAssets();

            if (showProgressBar)
            {
                EditorUtility.ClearProgressBar();
            }

            return new CleanResult(searchCount, cleanCount, sb.ToString());
        }

        /// <summary>
        /// 调用结束后，需要调用<see cref="AssetDatabase.SaveAssets()"/>保存修改。
        /// </summary>
        public static bool CleanSingle(Material mat, bool skipErrorShader)
        {
            if (null == mat) return false;
            if (skipErrorShader && (mat.shader == null || mat.shader.name == "Hidden/InternalErrorShader")) return false;

            bool changed = false;
            SerializedObject matInfo = new SerializedObject(mat);
            SerializedProperty propArr = matInfo.FindProperty("m_SavedProperties");

            propArr.Next(true);
            do
            {
                if (!propArr.isArray) continue;

                for (int i = propArr.arraySize - 1; i >= 0; --i)
                {
                    SerializedProperty prop = propArr.GetArrayElementAtIndex(i);

#if !UNITY_2017_1_OR_NEWER
                    prop = prop.FindPropertyRelative("first");
                    if (!mat.HasProperty(prop.stringValue))
                    {
                        propArr.DeleteArrayElementAtIndex(i);
                        changed = true;
                    }
#else
                    //[2019/1/10 补充]
                    //上面的内容是在Unity5.4环境下写的，现在新项目用的Unity2018.2.14。
                    //Material序列化结构变了，导致上面代码失效，在此补充新版本。
                    if (!mat.HasProperty(prop.displayName))
                    {
                        propArr.DeleteArrayElementAtIndex(i);
                        changed = true;
                    }
#endif
                }
            }
            while (propArr.Next(false));

            matInfo.ApplyModifiedProperties();
            return changed;
        }

    }
}