using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace EditorExt.Tools
{
    internal class MatcapRenderTool: EditorWindow
    {
        [MenuItem("[美术用]/S3插件/MatcapRenderTool")]
        private static void Init()
        {
            EditorWindow window = GetWindow(typeof(MatcapRenderTool));
            window.titleContent = new GUIContent("MatcapRenderTool");
            window.minSize = new Vector2(380, 500);
        }


        private int mode = 0;
        private string outputPath;
        private string outputTextureName;
        private int size;
        private int depth;
        private RenderTexture rgbRenderTexture;
        private RenderTexture aRenderTexture;
        private Material rgbMaterial;
        private Material aMaterial;

        #region Unity Functions

        void OnEnable()
        {
            outputPath = "Assets/Example/Matcap";
            outputTextureName = "mc_name";
            size = 512;
            depth = 32;
        }

        void OnGUI()
        {
            mode = GUILayout.Toolbar(mode, new[] {"SingleObjMode", "DualObjMode"});
            GUILayout.Space(10);

            EditorExtUtil.UI_PathField("生成文件路径", ref outputPath);

            GUILayout.Label("生成文件名");
            outputTextureName = EditorGUILayout.TextField(outputTextureName);
            GUILayout.Space(10);

            size = EditorGUILayout.IntField("Size", size);
            GUILayout.Space(10);

            rgbRenderTexture = EditorGUILayout.ObjectField("RGB RenderTexture", rgbRenderTexture, typeof(RenderTexture), false) as RenderTexture;
            aRenderTexture = EditorGUILayout.ObjectField("Alpha RenderTexture", aRenderTexture, typeof(RenderTexture), false) as RenderTexture;
            GUILayout.Space(10);

            rgbMaterial = EditorGUILayout.ObjectField("RGB Material", rgbMaterial, typeof(Material), false) as Material;
            aMaterial = EditorGUILayout.ObjectField("Alpha Material", aMaterial, typeof(Material), false) as Material;
            GUILayout.Space(10);

            if (mode == 0)
            {
                if (GUILayout.Button("BuildScene"))
                {
                    if (rgbRenderTexture == null)
                    {
                        rgbRenderTexture = new RenderTexture(size, size, depth);
                        AssetDatabase.CreateAsset(rgbRenderTexture, outputPath + "/" + outputTextureName + "_RGB.renderTexture");
                    }
                    if (aRenderTexture == null)
                    {
                        aRenderTexture = new RenderTexture(size, size, depth);
                        AssetDatabase.CreateAsset(aRenderTexture, outputPath + "/" + outputTextureName + "_A.renderTexture");
                    }

                    GameObject camera_obj = new GameObject(outputTextureName + "_RGB_camera");
                    SetupCamera(camera_obj, rgbRenderTexture);

                    GameObject camera_a_obj = new GameObject(outputTextureName + "_A_camera");
                    SetupCamera(camera_a_obj, aRenderTexture);

                    string targetName = outputTextureName + "_target";
                    GameObject target = GameObject.Find(targetName);
                    if (target == null)
                    {
                        target = Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Example/Matcap/Ball_High.fbx"));
                        target.name = targetName;
                    }

                    target.GetComponent<Renderer>().sharedMaterial = rgbMaterial;

                    camera_obj.transform.Translate(0, 0, 5);
                    camera_obj.transform.LookAt(target.transform);
                    camera_a_obj.transform.Translate(0, 0, 5);
                    camera_a_obj.transform.LookAt(target.transform);

                    SceneView.RepaintAll();
                    AssetDatabase.Refresh();
                }
                if (GUILayout.Button("Selected Camera Aim Target"))
                {
                    CameraAim("");
                }
                if (GUILayout.Button("Swap Material"))
                {
                    SwapMaterial();
                }
            }
            else if (mode == 1)
            {
                if (GUILayout.Button("BuildScene_RGB"))
                {
                    if (rgbRenderTexture == null)
                    {
                        rgbRenderTexture = new RenderTexture(size, size, depth);
                        AssetDatabase.CreateAsset(rgbRenderTexture, outputPath + "/" + outputTextureName + "_RGB.renderTexture");
                    }

                    GameObject camera_obj = new GameObject(outputTextureName + "_RGB_camera");
                    SetupCamera(camera_obj, rgbRenderTexture);

                    string targetName = outputTextureName + "_RGB_target";
                    GameObject target = GameObject.Find(targetName);
                    if (target == null)
                    {
                        target = Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Example/Matcap/Ball_High.fbx"));
                        target.name = targetName;
                    }

                    target.GetComponent<Renderer>().sharedMaterial = rgbMaterial;

                    camera_obj.transform.Translate(0, 0, 5);
                    camera_obj.transform.LookAt(target.transform);

                    SceneView.RepaintAll();
                    AssetDatabase.SaveAssets();
                }

                if (GUILayout.Button("BuildScene_A"))
                {
                    if (aRenderTexture == null)
                    {
                        aRenderTexture = new RenderTexture(size, size, depth);
                        AssetDatabase.CreateAsset(aRenderTexture, outputPath + "/" + outputTextureName + "_A.renderTexture");
                    }

                    GameObject camera_obj = new GameObject(outputTextureName + "_A_camera");
                    SetupCamera(camera_obj, aRenderTexture);

                    string targetName = outputTextureName + "_A_target";
                    GameObject target = GameObject.Find(targetName);
                    if (target == null)
                    {
                        target = Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Example/Matcap/Ball_High.fbx"));
                        target.name = targetName;
                    }

                    target.GetComponent<Renderer>().sharedMaterial = aMaterial;

                    target.transform.Translate(10, 0, 0);
                    camera_obj.transform.Translate(10, 0, 5);
                    camera_obj.transform.LookAt(target.transform);

                    SceneView.RepaintAll();
                    AssetDatabase.SaveAssets();
                }

                EditorGUILayout.BeginHorizontal();
                {
                    if (GUILayout.Button("Selected Camera Aim RGB"))
                    {
                        CameraAim("_RGB");
                    }
                    if (GUILayout.Button("Selected Camera Aim A"))
                    {
                        CameraAim("_A");
                    }
                }
                EditorGUILayout.EndHorizontal();

            }

            EditorGUILayout.BeginHorizontal();
            {
                if (GUILayout.Button("Create Texture RGB"))
                {
                    SaveRenderTextureTool.SaveRenderTextureToPNG(rgbRenderTexture, outputPath, outputTextureName + "_RGB");
                    AssetDatabase.Refresh();
                }
                if (GUILayout.Button("Create Texture A"))
                {
                    SaveRenderTextureTool.SaveRenderTextureToPNG(aRenderTexture, outputPath, outputTextureName + "_A");
                    AssetDatabase.Refresh();
                }
                if (mode == 1)
                {
                    if (GUILayout.Button("Create Texture RGBA"))
                    {
                        Texture2D rgb = SaveRenderTextureTool.SaveRenderTextureToTexture2D(rgbRenderTexture);

                        Texture2D a = SaveRenderTextureTool.SaveRenderTextureToTexture2D(aRenderTexture);

                        if (rgb == null)
                        {
                            EditorUtility.DisplayDialog("Error", "rgbRenderTexture saved in Texture2D is null", "OK");
                            return;
                        }
                        if (a == null)
                        {
                            EditorUtility.DisplayDialog("Error", "aRenderTexture saved in Texture2D is null", "OK");
                            return;
                        }

                        Texture2D texture = TextureCombineTool.CombineRGBAndASameSize(rgb, a);
                        EditorExtUtil.SaveTexture2DtoFile(texture, outputPath, outputTextureName, EditorExtUtil.SavingTextureFormat.PNG, true);
                        AssetDatabase.Refresh();
                    }
                }
            }
            EditorGUILayout.EndHorizontal();

        }

        #endregion

        private void CameraAim(string objName)
        {
            if (Selection.activeGameObject != null)
            {
                string targetName = outputTextureName + objName + "_target";
                GameObject target = GameObject.Find(targetName);
                if (target != null)
                {
                    Selection.activeGameObject.transform.LookAt(target.transform);
                }
            }
        }

        private void SetupCamera(GameObject obj, RenderTexture rt)
        {
            Camera camera = obj.AddComponent<Camera>();
            camera.cameraType = CameraType.Game;
            camera.orthographic = true;
            camera.orthographicSize = 0.99f;
            camera.farClipPlane = 10f;
            camera.nearClipPlane = 0.1f;
            camera.targetTexture = rt;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
        }

        private void SwapMaterial()
        {
            string targetName = outputTextureName + "_target";
            GameObject target = GameObject.Find(targetName);
            if (target != null)
            {
                Material mat = target.GetComponent<Renderer>().sharedMaterial;
                target.GetComponent<Renderer>().sharedMaterial = mat == rgbMaterial ? aMaterial : rgbMaterial;
            }
        }
    }
}