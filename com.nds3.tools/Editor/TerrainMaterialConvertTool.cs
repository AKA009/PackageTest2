using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;

namespace EditorExt.Tools
{
    public class TerrainMaterialConvertTool: EditorWindow
    {
        [MenuItem("[美术用]/S3插件/TerrainMaterialConvertTool")]
        private static void Init()
        {
            EditorWindow window = GetWindow(typeof(TerrainMaterialConvertTool));
            window.titleContent = new GUIContent("TerrainMaterialConvertTool");
        }

        
        private Terrain m_terrain;
        private Material m_material;

        private string outputSplatMapName;
        private string saveLayersPath;


        private const int Max_Layers_Count = 4;

        void OnEnable()
        {
            outputSplatMapName = EditorPrefs.GetString("TerrainMaterialConvertTool.outputSplatMapName");
            saveLayersPath = EditorPrefs.GetString("TerrainMaterialConvertTool.saveLayersPath");
        }

        void OnGUI()
        {
            //EditorGUILayout.LabelField("注意事项");

            m_terrain = EditorGUILayout.ObjectField("Terrain", m_terrain, typeof(Terrain), true) as Terrain;
            m_material = EditorGUILayout.ObjectField("Material(S3Unity/terrainBlend)", m_material, typeof(Material), true) as Material;

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Export splat map");
            if (GUILayout.Button("Select"))
            {
                outputSplatMapName = EditorUtility.SaveFilePanelInProject("ExportSplatMap", "SplatMap", "png", "ExportSplatMap");
            }
            EditorGUILayout.EndHorizontal();
            outputSplatMapName = EditorGUILayout.TextField(outputSplatMapName);

            if (GUILayout.Button("ExportToMaterial"))
            {
                if (m_terrain == null)
                {
                    EditorUtility.DisplayDialog("TerrainMaterialConvertTool", "Terrain is null", "Oh");
                    return;
                }
                if (m_material == null)
                {
                    EditorUtility.DisplayDialog("TerrainMaterialConvertTool", "Target material is null", "Oh");
                    return;
                }

                string e = Path.GetExtension(outputSplatMapName);
                ExportToMaterial(m_terrain, m_material, outputSplatMapName.Remove(outputSplatMapName.Length - e.Length));
                AssetDatabase.SaveAssets();
            }



            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Save layers path");
            if (GUILayout.Button("Select"))
            {
                saveLayersPath = EditorUtility.SaveFolderPanel("SaveLayersPath", "Assets", "");
                if (saveLayersPath.StartsWith(Application.dataPath, StringComparison.Ordinal))
                {
                    saveLayersPath = saveLayersPath.Substring(Application.dataPath.Length - 6);
                }
                else
                {
                    saveLayersPath = "Assets";
                }
            }
            EditorGUILayout.EndHorizontal();
            saveLayersPath = EditorGUILayout.TextField(saveLayersPath);

            if (GUILayout.Button("MaterialToTerrain"))
            {
                if (m_terrain == null)
                {
                    EditorUtility.DisplayDialog("TerrainMaterialConvertTool", "Terrain is null", "Oh");
                    return;
                }
                if (m_material == null)
                {
                    EditorUtility.DisplayDialog("TerrainMaterialConvertTool", "Target material is null", "Oh");
                    return;
                }
                MaterialToTerrain(m_terrain, m_material, saveLayersPath);
                AssetDatabase.SaveAssets();
            }
        }

        void OnDisable()
        {
            EditorPrefs.SetString("TerrainMaterialConvertTool.outputSplatMapName", outputSplatMapName);
            EditorPrefs.SetString("TerrainMaterialConvertTool.saveLayersPath", saveLayersPath);
        }

        /// <summary>
        /// 将地形中的颜色信息导出给材质，将会导出混合图（PNG），支持最多4套贴图混合，多余信息不导出
        /// </summary>
        /// <param name="terrain"></param>
        /// <param name="targetMaterial">目标材质，必须是<c>S3Unity/terrainBlend</c></param>
        /// <param name="splatMapPathWithoutExt">导出混合图的路径</param>
        public static void ExportToMaterial(Terrain terrain, Material targetMaterial, string splatMapPathWithoutExt)
        {
            if (terrain == null)
            {
                Debug.LogError("[TerrainMaterialConvertTool] ExportToMaterial: Terrain is null");
                return;
            }

            if (targetMaterial == null)
            {
                Debug.LogError("[TerrainMaterialConvertTool] ExportToMaterial: Target material is null");
                return;
            }

            if (targetMaterial.shader.name != "S3Unity/terrainBlend")
            {
                Debug.LogError("[TerrainMaterialConvertTool] ExportToMaterial: Target material shader error");
                return;
            }

            TerrainData terrainData = terrain.terrainData;
            TerrainLayer[] layers = terrainData.terrainLayers;
            Texture2D splatMap = terrainData.GetAlphamapTexture(0);
            string filePath = EditorExtUtil.SaveTexture2DtoFile(splatMap, splatMapPathWithoutExt, EditorExtUtil.SavingTextureFormat.PNG, false);
            if (filePath == null || !File.Exists(filePath))
            {
                Debug.LogError("[TerrainMaterialConvertTool] ExportToMaterial: Splat map export error");
                return;
            }

            bool hasNormalMap = false;
            bool hasRMMap = false;
            Texture2D newSplatMap = AssetDatabase.LoadAssetAtPath<Texture2D>(filePath);
            targetMaterial.SetTexture("tBlendingMap", newSplatMap);
            Vector4 rmi = targetMaterial.GetVector("_RMMapIntensity");
            for (int i = 0; i < Mathf.Min(layers.Length, Max_Layers_Count); i++)
            {
                TerrainLayer layer = layers[i];
                targetMaterial.SetTexture($"tDetailMap{i}", layer.diffuseTexture);
                //terrainData.size.xyz: width, height, length
                //DO NOT USE SetVector to set tex_ST
                targetMaterial.SetTextureScale($"tDetailMap{i}", new Vector2(SafeDivide(terrainData.size.x, layer.tileSize.x), SafeDivide(terrainData.size.z, layer.tileSize.y)));
                targetMaterial.SetTextureOffset($"tDetailMap{i}", new Vector2(SafeDivide(layer.tileOffset.x, layer.tileSize.x), SafeDivide(layer.tileOffset.y, layer.tileSize.y)));
                //Set map to null to remove old map
                targetMaterial.SetTexture($"tBumpMap{i}", layer.normalMapTexture);
                if (layer.normalMapTexture != null)
                {
                    hasNormalMap = true;
                }
                targetMaterial.SetTexture($"tSpecularRM{i}", layer.maskMapTexture);
                targetMaterial.SetFloat($"_metallic{i}", layer.metallic);
                targetMaterial.SetFloat($"_glossiness{i}", layer.smoothness);
                if (layer.maskMapTexture == null)
                {
                    rmi[i] = 0;
                }
                else
                {
                    rmi[i] = 1;
                    hasRMMap = true;
                }
            }
            targetMaterial.SetVector("_RMMapIntensity", rmi);
            EditorExtUtil.SetMaterialKeyword(targetMaterial, "BUMPMAP", hasNormalMap, true);
            EditorExtUtil.SetMaterialKeyword(targetMaterial, "_RMMAP_ON", hasRMMap, true);
        }

        /// <summary>
        /// 将已有材质的颜色信息回流给地形，会重新创建4个layer文件
        /// </summary>
        /// <param name="terrain"></param>
        /// <param name="targetMaterial"></param>
        /// <param name="layersSavePath"></param>
        public static void MaterialToTerrain(Terrain terrain, Material targetMaterial, string layersSavePath)
        {
            if (terrain == null)
            {
                Debug.LogError("[TerrainMaterialConvertTool] MaterialToTerrain: Terrain is null");
                return;
            }

            if (targetMaterial == null)
            {
                Debug.LogError("[TerrainMaterialConvertTool] MaterialToTerrain: Target material is null");
                return;
            }

            if (targetMaterial.shader.name != "S3Unity/terrainBlend")
            {
                Debug.LogError("[TerrainMaterialConvertTool] MaterialToTerrain: Target material shader error");
                return;
            }

            TerrainData terrainData = terrain.terrainData;
            TerrainLayer[] layers = new TerrainLayer[Max_Layers_Count];
            for (int i = 0; i < Max_Layers_Count; i++)
            {
                layers[i] = new TerrainLayer();
                TerrainLayer layer = layers[i];
                layer.diffuseTexture = (Texture2D) targetMaterial.GetTexture($"tDetailMap{i}");
                Vector2 s = targetMaterial.GetTextureScale($"tDetailMap{i}");
                Vector2 t = targetMaterial.GetTextureScale($"tDetailMap{i}");
                layer.tileSize = new Vector2(SafeDivide(terrainData.size.x, s.x), SafeDivide(terrainData.size.z, s.y));
                Vector2 to = new Vector2((t.x * layer.tileSize.x) % terrainData.size.x, (t.y * layer.tileSize.y) % terrainData.size.z);
                to.x = ForceZero(to.x);
                to.y = ForceZero(to.y);
                layer.tileOffset = to;
                layer.normalMapTexture = (Texture2D) targetMaterial.GetTexture($"tBumpMap{i}");
                layer.maskMapTexture = (Texture2D) targetMaterial.GetTexture($"tSpecularRM{i}");
                layer.metallic = targetMaterial.GetFloat($"_metallic{i}");
                layer.smoothness = targetMaterial.GetFloat($"_glossiness{i}");

                AssetDatabase.CreateAsset(layer, $"{layersSavePath}/NewTerrainLayer_{i}.asset");
            }
            terrain.terrainData.terrainLayers = layers;

            Texture2D splatMap = (Texture2D) targetMaterial.GetTexture("tBlendingMap");
            int size = splatMap.height;
            terrain.terrainData.alphamapResolution = size;
            float[,,] map = new float[size, size, Max_Layers_Count];
            for (int i = 0; i < size; i++)
            {
                for (int j = 0; j < size; j++)
                {
                    Color c = splatMap.GetPixel(j, i);
                    map[i, j, 0] = c.r;
                    map[i, j, 1] = c.g;
                    map[i, j, 2] = c.b;
                    map[i, j, 3] = c.a;
                }
            }
            terrain.terrainData.SetAlphamaps(0, 0, map);
        }

        private static float SafeDivide(float a, float b)
        {
            return Math.Abs(b) < 0.0001 ? 0 : a / b;
        }

        private static float ForceZero(float a)
        {
            return Math.Abs(a) < 0.0001 ? 0 : a;
        }
    }
}