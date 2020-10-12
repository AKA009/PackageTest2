using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Rendering;

namespace EditorExt.Tools.ShaderVariant
{
    [CreateAssetMenu(fileName = "ShaderKeywordConfig", menuName = "ScriptableObjects/ShaderKeywordConfig", order = 1)]
    public class ShaderKeywordConfig: ScriptableObject
    {
        public Shader[] ExcludeShader;
        public ShaderKeywordObject[] Config;

        public ShaderKeywordObject GetGlobalConfig()
        {
            if (Config == null || Config.Length == 0)
            {
                return null;
            }

            for (int i = 0; i < Config.Length; i++)
            {
                ShaderKeywordObject c = Config[i];
                if (c.Shader == null)
                {
                    return c;
                }
            }

            return null;
        }

        public ShaderKeywordObject GetConfigOfShader(Shader shader)
        {
            if (Config == null || Config.Length == 0)
            {
                return null;
            }

            for (int i = 0; i < Config.Length; i++)
            {
                ShaderKeywordObject c = Config[i];
                if (c.Shader == shader)
                {
                    return c;
                }
            }

            return null;
        }
    }

    [Serializable]
    public class ShaderKeywordObject
    {
        public Shader Shader;
        public PassType[] ExcludePassTypes;
        public string[] ExcludeKeywords;
    }

    [CustomEditor(typeof(ShaderKeywordConfig))]
    public class CE_ShaderKeywordConfig: Editor
    {
        private Color m_guiDefaultColor;

        #region Overrides of Editor

        /// <inheritdoc />
        public override void OnInspectorGUI()
        {
            EditorGUILayout.LabelField("在此处配置需要在集合中排除的变体");
            EditorGUILayout.LabelField("每个Shader一条配置，Shader重复时只读取第一条配置");
            EditorGUILayout.LabelField("Shader 为 None 时表示全局配置");
            EditorGUILayout.LabelField("排除关键字的方式为删除所有包含它的变体");
            EditorGUILayout.LabelField("也可以直接排除指定的Shader");

            Color_Begin(Color.yellow);
            EditorGUILayout.LabelField("--------------------------------------------------------------------------------------------------------------------");
            Color_End();

            base.OnInspectorGUI();
        }

        #endregion

        private void Color_Begin(Color color)
        {
            m_guiDefaultColor = GUI.color;
            GUI.color = color;
        }
        private void Color_End()
        {
            GUI.color = m_guiDefaultColor;
        }
    }
}