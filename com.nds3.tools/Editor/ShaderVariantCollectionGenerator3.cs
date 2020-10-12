//#define DEBUG_ON

using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using System;
using System.IO;
using System.Text;
using UnityEditor.Build;
using UnityEditor.Rendering;

namespace EditorExt.Tools.ShaderVariant
{
    public class ShaderVariantCollectionGenerator3: EditorWindow
    {
        [MenuItem("[美术用]/S3插件/创建ShaderVariantCollection")]
        static void Init()
        {
            EditorWindow window = GetWindow(typeof(ShaderVariantCollectionGenerator3));
            window.titleContent = new GUIContent("SVCG3");
            window.minSize = new Vector2(380, 710);
        }

        private UnityEngine.Object pathObj;
        private string m_materialsPath = "Assets/Resources";

        private ShaderVariantCollection svcToShow;
        private string m_svcPath = "Assets/Resources/shader/variants.shadervariants";

        private ShaderKeywordConfig excludeConfig;
        private string configPath = "Assets/Resources/shader/ShaderKeywordConfig.asset";
        private string m_tempPath = "Assets/SVCG_Temp";
        private string m_txtPath = "Assets/data.txt";
        private BuildTarget m_materialBuildTarget = BuildTarget.StandaloneWindows;
        private static bool stopOnException = false;
        private bool autoDeleteTempFolder = true;
        private static bool writeToFile = false;

        private StringBuilder outputSB;
        private StringBuilder exceptionSB;

        private static bool isCreating = false;
        private static Dictionary<Shader, HashSet<string>> dataDictionary = null;
        private static ShaderVariantCollection currentSVC = null;
        private static ShaderKeywordConfig currentConfig = null;

        private const string abName = "SVCG_TempMaterialsBundle.bundle";

        #region Internal Properties

        internal static bool IsCreating { get { return isCreating; } }

        internal static bool WillWriteToFile { get { return writeToFile; } }

        internal static bool StopOnException { get { return stopOnException; } }

        internal static Dictionary<Shader, HashSet<string>> DataDictionary { get { return dataDictionary; } }

        internal static ShaderVariantCollection CurrentSVC { get { return currentSVC; } }

        internal static ShaderKeywordConfig CurrentConfig { get { return currentConfig; } }

        #endregion

        #region Public Functions

        /// <summary>
        /// 创建变体文件，扫描Assets/Resources下的所有材质，生成的文件位于Assets/Resources/shader/variants.shadervariants
        /// </summary>
        /// <param name="materialBuildTarget">材质的打包平台</param>
        public static void CreateSVC(BuildTarget materialBuildTarget)
        {
            ShaderVariantCollectionGenerator3 instance = new ShaderVariantCollectionGenerator3();
            instance.excludeConfig = (ShaderKeywordConfig) AssetDatabase.LoadAssetAtPath(instance.configPath, typeof(ShaderKeywordConfig));
            instance.DoCreate(materialBuildTarget);
        }

        #endregion

        #region Unity Functions

        void OnEnable()
        {
            outputSB = new StringBuilder();
        }

        void OnGUI()
        {
            GUILayout.Space(5);

            GUIStyle logoFont = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 20
            };
            GUILayout.Label("ShaderVariantCollectionGenerator", logoFont);

            GUILayout.Space(10);

            GUILayout.Label("扫描材质路径（可以拖入文件夹自动获取）");
            EditorGUILayout.BeginHorizontal();
            {
                EditorGUI.BeginChangeCheck();
                pathObj = EditorGUILayout.ObjectField(pathObj, typeof(UnityEngine.Object), false, GUILayout.Width(110));
                if (EditorGUI.EndChangeCheck())
                {
                    m_materialsPath = AssetDatabase.GetAssetPath(pathObj);
                    //判断是否为斜杠结尾，搜索的文件夹不能用斜杠结尾
                    if (m_materialsPath.EndsWith("/"))
                    {
                        m_materialsPath = m_materialsPath.Remove(m_materialsPath.Length-2, 1);
                    }
                    pathObj = null;
                }

                m_materialsPath = EditorGUILayout.TextField(m_materialsPath);
            }
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(10);

            GUILayout.Label("生成变体文件路径");
            m_svcPath = EditorGUILayout.TextField(m_svcPath);
            svcToShow = (ShaderVariantCollection) EditorGUILayout.ObjectField("ShaderVariantCollection", svcToShow, typeof(ShaderVariantCollection), false);
            GUILayout.Space(10);

            GUILayout.Label("剔除配置文件路径");
            configPath = EditorGUILayout.TextField(configPath);
            excludeConfig = (ShaderKeywordConfig) EditorGUILayout.ObjectField("ExcludeKeywordConfig", excludeConfig, typeof(ShaderKeywordConfig), false);
            GUILayout.Space(10);

            GUILayout.Label("临时文件夹路径，不能用斜杠结尾");
            m_tempPath = EditorGUILayout.TextField(m_tempPath);
            GUILayout.Space(10);

            GUILayout.Label("文本文件路径");
            m_txtPath = EditorGUILayout.TextField(m_txtPath);
            GUILayout.Space(10);

            m_materialBuildTarget = (BuildTarget) EditorGUILayout.EnumPopup("MaterialBuildTarget", m_materialBuildTarget);
            GUILayout.Space(10);

            stopOnException = EditorGUILayout.ToggleLeft("异常时中断执行，严重增加运行时间", stopOnException);
            autoDeleteTempFolder = EditorGUILayout.ToggleLeft("自动清除临时文件夹", autoDeleteTempFolder);
            writeToFile = EditorGUILayout.ToggleLeft("结果写入文本文件，不生成变体", writeToFile);

            const int buttonHeight = 40;
            EditorGUILayout.BeginHorizontal();
            {
                if (GUILayout.Button("自动获取 KeywordConfig", GUILayout.Height(buttonHeight)))
                {
                    excludeConfig = (ShaderKeywordConfig) AssetDatabase.LoadAssetAtPath(configPath, typeof(ShaderKeywordConfig));
                    if (excludeConfig == null)
                    {
                        EditorUtility.DisplayDialog("SVCG", "文件不存在或无法加载", "哦");
                    }
                }
                if (GUILayout.Button("创建 KeywordConfig", GUILayout.Height(buttonHeight)))
                {
                    if (File.Exists(configPath))
                    {
                        if (!EditorUtility.DisplayDialog("SVCG", "文件已存在，是否覆盖？", "是", "否"))
                        {
                            return;
                        }
                    }
                    excludeConfig = CreateInstance<ShaderKeywordConfig>();
                    AssetDatabase.CreateAsset(excludeConfig, configPath);
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            {
                if (GUILayout.Button("自动获取 SVC", GUILayout.Height(buttonHeight)))
                {
                    svcToShow = (ShaderVariantCollection) AssetDatabase.LoadAssetAtPath(m_svcPath, typeof(ShaderVariantCollection));
                    if (svcToShow == null)
                    {
                        EditorUtility.DisplayDialog("SVCG", "文件不存在或无法加载", "哦");
                    }
                }
                if (GUILayout.Button("覆盖选择 SVC", GUILayout.Height(buttonHeight)))
                {
                    if (svcToShow != null)
                    {
                        m_svcPath = AssetDatabase.GetAssetPath(svcToShow);
                    }
                }
                if (GUILayout.Button("显示SVC信息", GUILayout.Height(buttonHeight)))
                {
                    ClearOutput();
                    ShowSVCInfo();
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            {
                if (GUILayout.Button("清除临时文件", GUILayout.Height(buttonHeight)))
                {
                    DeleteTempFile();
                }
                if (GUILayout.Button("清除临时文件夹", GUILayout.Height(buttonHeight)))
                {
                    DeleteTempFolder();
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            {
                Color defaultColor = GUI.backgroundColor;
                GUI.backgroundColor = Color.green;
                if (GUILayout.Button("收集变体", GUILayout.Height(buttonHeight)))
                {
                    ClearOutput();
                    outputSB.AppendLine("收集中...");
                    DoCreate(m_materialBuildTarget);
                }
                GUI.backgroundColor = defaultColor;

                if (GUILayout.Button("从文本文件写入变体", GUILayout.Height(buttonHeight)))
                {
                    ShaderVariantCollection svc = new ShaderVariantCollection();
                    svcToShow = svc;
                    AssetDatabase.CreateAsset(svc, m_svcPath);
                    ReadSVCDataAndAdd(svcToShow);
                }
            }
            EditorGUILayout.EndHorizontal();


            //使用这种方式禁止交互
            GUILayout.Label(outputSB.ToString(), EditorStyles.textArea, GUILayout.Height(120));

            //_guiDebug();
        }

        #endregion


        #region Private Functions

        private void DoCreate(BuildTarget materialBuildTarget)
        {
            if (exceptionSB == null)
            {
                exceptionSB = new StringBuilder();
            }
            else
            {
                exceptionSB.Clear();
            }

            //计时开始
            System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
            stopwatch.Start();

            if (!writeToFile)
            {
                ShaderVariantCollection svc = new ShaderVariantCollection();
                currentSVC = svc;
                svcToShow = svc;
                AssetDatabase.CreateAsset(svc, m_svcPath);
            }

            currentConfig = excludeConfig;

            isCreating = true;
            dataDictionary = new Dictionary<Shader, HashSet<string>>();

            int totalMaterialsCount = 0;
            int removedFBXCount = 0;
            int removedErrorMaterialsCount = 0;
            int finalMaterialsCount = 0;

            //Resources目录下的所有材质
            string[] guidMats = AssetDatabase.FindAssets("t:Material", new string[] { m_materialsPath });
            totalMaterialsCount = guidMats.Length;

            List<string> assetNameList = new List<string>();

            //移除空材质、错误材质和嵌入材质附带的FBX文件
            for (int i = 0; i < guidMats.Length; i++)
            {
                EditorUtility.DisplayProgressBar("正在扫描材质", i + " / " + guidMats.Length, i * 1.0f / guidMats.Length);
                string an = AssetDatabase.GUIDToAssetPath(guidMats[i]);
                if (string.Equals(Path.GetExtension(an), ".fbx", StringComparison.OrdinalIgnoreCase))
                {
                    removedFBXCount++;
                    continue;
                }
                Material mat = (Material) AssetDatabase.LoadAssetAtPath(an, typeof(Material));
                if (mat == null || mat.shader == null)
                {
                    removedErrorMaterialsCount++;
                    continue;
                }
                assetNameList.Add(an);
#if DEBUG_ON
                Debug.Log("Add: " + an);
#endif
            }

            // Create the array of bundle build details.
            AssetBundleBuild buildMap = new AssetBundleBuild
            {
                assetBundleName = abName,
                assetNames = assetNameList.ToArray(),
            };

            AssetBundleBuild[] buildMapArray = new AssetBundleBuild[1];
            buildMapArray[0] = buildMap;
            EditorUtility.ClearProgressBar();
            finalMaterialsCount = buildMap.assetNames.Length;

            if (!Directory.Exists(m_tempPath))
            {
                Directory.CreateDirectory(m_tempPath);
            }
            BuildPipeline.BuildAssetBundles(m_tempPath, buildMapArray, BuildAssetBundleOptions.None, materialBuildTarget);

            if (writeToFile)
            {
                //StreamWriter第二个参数为false覆盖现有文件，为true则把文本追加到文件末尾
                using (StreamWriter file = new StreamWriter(m_txtPath, false))
                {
                    foreach (KeyValuePair<Shader, HashSet<string>> kvp in dataDictionary)
                    {
                        file.WriteLine("Shader: " + kvp.Key.name);
                        HashSet<string> v = kvp.Value;
                        foreach (string kws in v)
                        {
                            file.WriteLine(kws);
                        }
                    }

                    file.Close();
                }
            }

            if (autoDeleteTempFolder)
            {
#if DEBUG_ON
                Debug.Log("Delete temp bundle");
#endif
                DeleteTempFolder();
            }

            //计时结束
            stopwatch.Stop();
            TimeSpan timespan = stopwatch.Elapsed;

            isCreating = false;
            currentSVC = null;
            currentConfig = null;

            ClearOutput();
            outputSB.AppendLine("创建完成，耗时： " + timespan.Hours + "h," + timespan.Minutes + "m," + timespan.Seconds + "s");
            outputSB.AppendLine("已扫描材质数量： " + totalMaterialsCount);
            outputSB.AppendLine("移除FBX数量： " + removedFBXCount);
            outputSB.AppendLine("移除错误材质数量： " + removedErrorMaterialsCount);
            outputSB.AppendLine("最终材质数量： " + finalMaterialsCount);
            if (!writeToFile)
            {
                ShowSVCInfo();
            }
            AssetDatabase.SaveAssets();
        }

        private void ReadSVCDataAndAdd(ShaderVariantCollection svc)
        {
            if (!File.Exists(m_txtPath))
            {
                return;
            }

            //计时开始
            System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
            stopwatch.Start();

            //StreamWriter第二个参数为false覆盖现有文件，为true则把文本追加到文件末尾
            using (StreamReader file = new StreamReader(m_txtPath))
            {
                Shader shader = null;

                //逐行读取文件处理至文件结束
                string str;
                while ((str = file.ReadLine()) != null)
                {
                    if (str.StartsWith("Shader: "))
                    {
                        shader = Shader.Find(str.Replace("Shader: ", ""));
                    }
                    else
                    {
                        if (shader == null)
                        {
                            continue;
                        }

                        //只搜索前4位
                        int firstSpaceIndex = str.IndexOf(" ", 0, 4, StringComparison.Ordinal);
                        PassType passType = (PassType) Convert.ToInt32(str.Substring(0, firstSpaceIndex));
                        string[] kws = str.Remove(0, firstSpaceIndex + 1).Split(' ');

                        svc.Add(new ShaderVariantCollection.ShaderVariant(shader, passType, kws));
                    }
                }

                file.Close();
            }

            //计时结束
            stopwatch.Stop();
            TimeSpan timespan = stopwatch.Elapsed;

            ClearOutput();
            outputSB.AppendLine("创建完成，耗时： " + timespan.Hours + "h," + timespan.Minutes + "m," + timespan.Seconds + "s");
            ShowSVCInfo();
            AssetDatabase.SaveAssets();
        }

        private void ShowSVCInfo()
        {
            if (svcToShow == null)
            {
                return;
            }

            outputSB.AppendLine("名称： " + svcToShow.name);
            outputSB.AppendLine("包含的Shader数量： " + svcToShow.shaderCount);
            outputSB.AppendLine("包含的变体数量： " + svcToShow.variantCount);
        }

        private void ClearOutput()
        {
            outputSB.Clear();
        }

        private List<string[]>[] GetAllCustomVariants(string[] keywords)
        {
            List<string[]>[] result = new List<string[]>[keywords.Length];

            for (int i = 0; i < keywords.Length; i++)
            {
                result[i] = PermutationAndCombination<string>.GetCombination(keywords, i + 1);
            }

            return result;
        }

        private void DeleteTempFile()
        {
            string[] fs = new[]
            {
                m_tempPath + "/" + abName,
                m_tempPath + "/" + abName + ".manifest",
            };

            for (int i = 0; i < fs.Length; i++)
            {
                //if (File.Exists(fs[i]))
                //{
                //    File.Delete(fs[i]);
                //}
                AssetDatabase.DeleteAsset(fs[i]);
            }

            AssetDatabase.Refresh();
        }

        private void DeleteTempFolder()
        {
            FileUtil.DeleteFileOrDirectory(m_tempPath);
            FileUtil.DeleteFileOrDirectory(m_tempPath + ".meta");
            AssetDatabase.Refresh();
        }

        private T[] CombineArray<T> (T[] array1, T[] array2)
        {
            T[] temp = new T[array1.Length + array2.Length];
            array1.CopyTo(temp, 0);
            array2.CopyTo(temp, array1.Length);

            return temp;
        }

        #endregion


        #region PermutationAndCombination

        //REF: https://www.cnblogs.com/zhao-yi/p/8533035.html

        public class PermutationAndCombination<T>
        {
            /// <summary>
            /// 交换两个变量
            /// </summary>
            /// <param name="a">变量1</param>
            /// <param name="b">变量2</param>
            public static void Swap(ref T a, ref T b)
            {
                T temp = a;
                a = b;
                b = temp;
            }
            /// <summary>
            /// 递归算法求数组的组合(私有成员)
            /// </summary>
            /// <param name="list">返回的范型</param>
            /// <param name="t">所求数组</param>
            /// <param name="n">辅助变量</param>
            /// <param name="m">辅助变量</param>
            /// <param name="b">辅助数组</param>
            /// <param name="M">辅助变量M</param>
            private static void GetCombination(ref List<T[]> list, T[] t, int n, int m, int[] b, int M)
            {
                for (int i = n; i >= m; i--)
                {
                    b[m - 1] = i - 1;
                    if (m > 1)
                    {
                        GetCombination(ref list, t, i - 1, m - 1, b, M);
                    }
                    else
                    {
                        if (list == null)
                        {
                            list = new List<T[]>();
                        }
                        T[] temp = new T[M];
                        for (int j = 0; j < b.Length; j++)
                        {
                            temp[j] = t[b[j]];
                        }
                        list.Add(temp);
                    }
                }
            }
            /// <summary>
            /// 递归算法求排列(私有成员)
            /// </summary>
            /// <param name="list">返回的列表</param>
            /// <param name="t">所求数组</param>
            /// <param name="startIndex">起始标号</param>
            /// <param name="endIndex">结束标号</param>
            private static void GetPermutation(ref List<T[]> list, T[] t, int startIndex, int endIndex)
            {
                if (startIndex == endIndex)
                {
                    if (list == null)
                    {
                        list = new List<T[]>();
                    }
                    T[] temp = new T[t.Length];
                    t.CopyTo(temp, 0);
                    list.Add(temp);
                }
                else
                {
                    for (int i = startIndex; i <= endIndex; i++)
                    {
                        Swap(ref t[startIndex], ref t[i]);
                        GetPermutation(ref list, t, startIndex + 1, endIndex);
                        Swap(ref t[startIndex], ref t[i]);
                    }
                }
            }
            /// <summary>
            /// 求从起始标号到结束标号的排列，其余元素不变
            /// </summary>
            /// <param name="t">所求数组</param>
            /// <param name="startIndex">起始标号</param>
            /// <param name="endIndex">结束标号</param>
            /// <returns>从起始标号到结束标号排列的范型</returns>
            public static List<T[]> GetPermutation(T[] t, int startIndex, int endIndex)
            {
                if (startIndex < 0 || endIndex > t.Length - 1)
                {
                    return null;
                }
                List<T[]> list = new List<T[]>();
                GetPermutation(ref list, t, startIndex, endIndex);
                return list;
            }
            /// <summary>
            /// 返回数组所有元素的全排列
            /// </summary>
            /// <param name="t">所求数组</param>
            /// <returns>全排列的范型</returns>
            public static List<T[]> GetPermutation(T[] t)
            {
                return GetPermutation(t, 0, t.Length - 1);
            }
            /// <summary>
            /// 求数组中n个元素的排列
            /// </summary>
            /// <param name="t">所求数组</param>
            /// <param name="n">元素个数</param>
            /// <returns>数组中n个元素的排列</returns>
            public static List<T[]> GetPermutation(T[] t, int n)
            {
                if (n > t.Length)
                {
                    return null;
                }
                List<T[]> list = new List<T[]>();
                List<T[]> c = GetCombination(t, n);
                for (int i = 0; i < c.Count; i++)
                {
                    List<T[]> l = new List<T[]>();
                    GetPermutation(ref l, c[i], 0, n - 1);
                    list.AddRange(l);
                }
                return list;
            }
            /// <summary>
            /// 求数组中n个元素的组合
            /// </summary>
            /// <param name="t">所求数组</param>
            /// <param name="n">元素个数</param>
            /// <returns>数组中n个元素的组合的范型</returns>
            public static List<T[]> GetCombination(T[] t, int n)
            {
                if (t.Length < n)
                {
                    return null;
                }
                int[] temp = new int[n];
                List<T[]> list = new List<T[]>();
                GetCombination(ref list, t, t.Length, n, temp, n);
                return list;
            }
        }

        #endregion


        #region Debug Functions

        private void _guiDebug()
        {
            //*---- Debug ----*/
            EditorGUILayout.Space();
            Color defaultColor = GUI.color;
            GUI.color = Color.green;

            EditorGUILayout.LabelField("this.position: " + this.position);

            GUI.color = defaultColor;
        }

        #endregion

    }

    internal class SVCG_ShaderPreprocessor: IPreprocessShaders
    {
        #region Implementation of IOrderedCallback

        /// <inheritdoc />
        public int callbackOrder { get { return 0; } }

        /// <inheritdoc />
        public void OnProcessShader(Shader shader, ShaderSnippetData snippet, IList<ShaderCompilerData> data)
        {
            if (!ShaderVariantCollectionGenerator3.IsCreating)
            {
                return;
            }
#if DEBUG_ON
            Debug.Log("SVCG_ShaderPreprocessor.OnProcessShader(" + shader.name + ")");
#endif
            ShaderVariantCollection svc = ShaderVariantCollectionGenerator3.CurrentSVC;
            if (!ShaderVariantCollectionGenerator3.WillWriteToFile && svc == null)
            {
                return;
            }

            //处理剔除配置
            if (ShaderVariantCollectionGenerator3.CurrentConfig != null)
            {
                EditorUtility.DisplayProgressBar("正在执行剔除", $"Shader: {shader.name}", 0);
                ShaderKeywordConfig config = ShaderVariantCollectionGenerator3.CurrentConfig;
                if (DoExcludeShader(config, shader))
                {
                    return;
                }
                EditorUtility.DisplayProgressBar("正在执行剔除", $"Shader: {shader.name}", 0.3333f);
                if (DoExclude(config.GetGlobalConfig(), snippet, data))
                {
                    return;
                }
                EditorUtility.DisplayProgressBar("正在执行剔除", $"Shader: {shader.name}", 0.6666f);
                if (DoExclude(config.GetConfigOfShader(shader), snippet, data))
                {
                    return;
                }
                EditorUtility.DisplayProgressBar("正在执行剔除", $"Shader: {shader.name}", 1);
            }

            for (int i = 0; i < data.Count; i++)
            {
                EditorUtility.DisplayProgressBar("正在处理变体", $"Shader: {shader.name}, data: {i}/{data.Count}", (float) i / data.Count);

                ShaderKeyword[] kws = data[i].shaderKeywordSet.GetShaderKeywords();

                if (ShaderVariantCollectionGenerator3.WillWriteToFile)
                {
                    string[] strKWs = new string[kws.Length + 1];
                    strKWs[0] = ((int) snippet.passType).ToString();
                    for (int j = 1; j < kws.Length + 1; j++)
                    {
#if UNITY_2019_3_OR_NEWER
                        strKWs[j] = ShaderKeyword.GetKeywordName(shader, kws[j - 1]);
#else
                        strKWs[j] = kws[j - 1].GetKeywordName();
#endif
                    }

                    HashSet<string> d = null;
                    if (ShaderVariantCollectionGenerator3.DataDictionary.TryGetValue(shader, out d))
                    {
                        d.Add(DebugUtil.LogString(strKWs));
                    }
                    else
                    {
                        d = new HashSet<string>();
                        d.Add(DebugUtil.LogString(strKWs));
                        ShaderVariantCollectionGenerator3.DataDictionary.Add(shader, d);
                    }
#if DEBUG_ON
                    Debug.Log("file.Add(" + DebugUtil.LogString(strKWs) + ")");
#endif
                }
                else
                {
                    string[] strKWs = new string[kws.Length];
                    for (int j = 0; j < kws.Length; j++)
                    {
#if UNITY_2019_3_OR_NEWER
                        strKWs[j] = ShaderKeyword.GetKeywordName(shader, kws[j]);
#else
                        strKWs[j] = kws[j].GetKeywordName();
#endif
                    }
#if DEBUG_ON
                    Debug.Log("svc.Add(" + shader + ", " + snippet.passType + ", " + DebugUtil.LogString(strKWs) + ")");
#endif
                    if (ShaderVariantCollectionGenerator3.StopOnException)
                    {
                        svc.Add(new ShaderVariantCollection.ShaderVariant(shader, snippet.passType, strKWs));
                    }
                    else
                    {
                        //不使用构造函数可以避免调用 ShaderVariantCollection.ShaderVariant.CheckShaderVariant
                        //它将耗费大量时间来判断输入数据是否存在异常
                        ShaderVariantCollection.ShaderVariant sv = new ShaderVariantCollection.ShaderVariant();
                        sv.shader = shader;
                        sv.passType = snippet.passType;
                        sv.keywords = strKWs;
                        svc.Add(sv);
                    }
                }
            }

            //实际打包时不编译shader变体，仅收集信息，大幅优化执行时间
            data.Clear();
        }

        #endregion

        /// <summary>
        /// 按shader进行剔除
        /// </summary>
        /// <returns>如果需要放弃本次处理，返回true</returns>
        private bool DoExcludeShader(ShaderKeywordConfig config, Shader currentShader)
        {
            Shader[] e = config.ExcludeShader;
            if (e == null)
            {
                return false;
            }

            for (int i = 0; i < e.Length; i++)
            {
                if (e[i] == currentShader)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 执行剔除
        /// </summary>
        /// <returns>如果需要放弃本次处理，返回true</returns>
        private bool DoExclude(ShaderKeywordObject configObj, ShaderSnippetData snippet, IList<ShaderCompilerData> data)
        {
            if (configObj == null)
            {
                return false;
            }

            if (configObj.ExcludePassTypes != null)
            {
                for (int i = 0; i < configObj.ExcludePassTypes.Length; i++)
                {
                    if (snippet.passType == configObj.ExcludePassTypes[i])
                    {
                        return true;
                    }
                }
            }

            if (configObj.ExcludeKeywords != null)
            {
                for (int kwIndex = 0; kwIndex < configObj.ExcludeKeywords.Length; kwIndex++)
                {
                    for (int dataIndex = data.Count - 1; dataIndex >= 0; --dataIndex)
                    {
                        //如果没有定义这个关键字就过
                        if (!data[dataIndex].shaderKeywordSet.IsEnabled(new ShaderKeyword(configObj.ExcludeKeywords[kwIndex])))
                            continue;

                        //否则将这一条data移除
                        data.RemoveAt(dataIndex);
                    }
                }
            }

            return false;
        }
    }

    internal static class DebugUtil
    {
        public static string LogString(string[] array)
        {
            return string.Join(" ", array);
        }
        public static string LogString(HashSet<string> set)
        {
            return string.Join(" ", set.ToArray());
        }
    }
}