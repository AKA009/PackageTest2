using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using EditorExt.Tools.TreeViewExample;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using UnityEngine.Assertions;

namespace EditorExt.Tools
{
    public class MaterialInfoCollector: EditorWindow
    {
        public struct AssetInfo
        {
            public string Name;
            public string Folder;
            public string RPath;
            //public string GUID;
            public string ShaderName;
        }

        public enum ShaderNameCompareMode
        {
            FullStringEqual,
            StartWith
        }


        [MenuItem("S3插件/MaterialInfoCollector")]
        private static void Init()
        {
            MaterialInfoCollector window = GetWindow<MaterialInfoCollector>("MaterialInfoCollector");
            window.minSize = new Vector2(460, 300);
        }


        private Dictionary<string, string> dataDictionary = new Dictionary<string, string>();
        private List<AssetInfo> infoList = new List<AssetInfo>();
        private List<AssetInfo> subInfoList = new List<AssetInfo>();

        private string searchString = "t:Material";
        private string exportPath = "export.xml";
        private string materialsPath = "Assets/art";
        private bool resetOnlyFilteredShader = true;
        private bool[] filterToggleValue;


        [NonSerialized] private bool m_initialized;

        // Serialized in the window layout file so it survives assembly reloading
        [SerializeField] private TreeViewState m_treeViewState;
        [SerializeField] private MultiColumnHeaderState m_multiColumnHeaderState;

        private SearchField m_searchField;
        private MIC_TreeView m_treeView;

        private bool useSubList = false;
        private bool infoListChanged = false;

        private const string c_Dialog_Title = "MaterialInfoCollector";

        #region Unity Functions

        void OnGUI()
        {
            searchString = EditorGUILayout.TextField("SearchString", searchString);
            exportPath = EditorGUILayout.TextField("ExportPath", exportPath);
            materialsPath = EditorGUILayout.TextField("MaterialsPath", materialsPath);
            resetOnlyFilteredShader = EditorGUILayout.ToggleLeft("Reset only filtered shaders", resetOnlyFilteredShader);

            MainButtonsGUI();
            FilterButtonsGUI();
            //ListGUI();
            TreeViewGUI();
        }

        #endregion


        #region Public Functions

        public static Dictionary<string, string> Collect(string findFilter)
        {
            Dictionary<string, string> dic = new Dictionary<string, string>();

            string[] assetsGUID = AssetDatabase.FindAssets(findFilter);
            string[] assetsPath = new string[assetsGUID.Length];
            for (int i = 0; i < assetsGUID.Length; i++)
            {
                assetsPath[i] = AssetDatabase.GUIDToAssetPath(assetsGUID[i]);
                dic.Add(assetsPath[i], assetsGUID[i]);
            }

            return dic;
        }

        public static List<AssetInfo> CollectMaterialInfo(string searchFilter, string targetFolder)
        {
            if (targetFolder.EndsWith("/", StringComparison.OrdinalIgnoreCase))
            {
                targetFolder = targetFolder.Remove(targetFolder.Length - 1, 1);
            }

            List<AssetInfo> list = new List<AssetInfo>();
            string[] guidMats;
            if (string.IsNullOrEmpty(targetFolder))
            {
                guidMats = AssetDatabase.FindAssets(searchFilter);
            }
            else
            {
                guidMats = AssetDatabase.FindAssets(searchFilter, new string[] { targetFolder });
            }

            //移除空材质、错误材质和嵌入材质附带的FBX文件
            for (int i = 0; i < guidMats.Length; i++)
            {
                EditorUtility.DisplayProgressBar(c_Dialog_Title, $"Searching materials: {i} / {guidMats.Length}", i * 1.0f / guidMats.Length);
                string an = AssetDatabase.GUIDToAssetPath(guidMats[i]);
                if (string.Equals(Path.GetExtension(an), ".fbx", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                Material mat = (Material) AssetDatabase.LoadAssetAtPath(an, typeof(Material));
                if (mat == null)
                {
                    continue;
                }

                string fileName = Path.GetFileName(an);
                AssetInfo assetInfo = new AssetInfo();
                assetInfo.Name = mat.name;
                assetInfo.Folder = an.Remove(an.Length - fileName.Length, fileName.Length);
                assetInfo.RPath = an;
                assetInfo.ShaderName = mat.shader?.name;

                list.Add(assetInfo);
            }

            EditorUtility.ClearProgressBar();
            return list;
        }

        public static void ExportXML(List<AssetInfo> list, string path)
        {
            //xml保存的路径，这里放在Assets路径 注意路径。
            string filepath = Application.dataPath + "/" + path;
            //继续判断当前路径下是否有该文件
            if (File.Exists(filepath))
            {
                FileUtil.DeleteFileOrDirectory(filepath);
            }
            //创建XML文档实例
            XmlDocument xmlDoc = new XmlDocument();
            //创建root节点，也就是最上一层节点
            XmlElement root = xmlDoc.CreateElement("Assets");
            //继续创建下一层节点
            for (int i = 0; i < list.Count; i++)
            {
                EditorUtility.DisplayProgressBar(c_Dialog_Title, $"Exporting: {i} / {list.Count}", i * 1.0f / list.Count);
                XmlElement ai = xmlDoc.CreateElement("AssetInfo");

                XmlElement name = xmlDoc.CreateElement("Name");
                XmlElement rPath = xmlDoc.CreateElement("RPath");
                XmlElement ShaderName = xmlDoc.CreateElement("ShaderName");
                name.InnerText = list[i].Name;
                rPath.InnerText = list[i].RPath;
                ShaderName.InnerText = list[i].ShaderName;
                ai.AppendChild(name);
                ai.AppendChild(rPath);
                ai.AppendChild(ShaderName);

                root.AppendChild(ai);
            }

            //把节点一层一层的添加至XMLDoc中 ，请仔细看它们之间的先后顺序，这将是生成XML文件的顺序
            xmlDoc.AppendChild(root);
            //把XML文件保存至本地
            xmlDoc.Save(filepath);

            EditorUtility.ClearProgressBar();
        }

        public static List<AssetInfo> ReadXML(string path)
        {
            string filepath = Application.dataPath + "/" + path;
            if (File.Exists(filepath))
            {
                XmlDocument xmlDoc = new XmlDocument();
                //根据路径将XML读取出来
                xmlDoc.Load(filepath);
                //得到transforms下的所有子节点
                XmlNodeList nodeList = xmlDoc.SelectSingleNode("Assets").ChildNodes;

                List<AssetInfo> list = new List<AssetInfo>();

                //遍历所有子节点
                foreach (XmlElement xe in nodeList)
                {
                    if (xe.Name == "AssetInfo")
                    {
                        AssetInfo ai = new AssetInfo();

                        for (int i = 0; i < xe.ChildNodes.Count; i++)
                        {
                            XmlNode c = xe.ChildNodes[i];
                            if (c.Name == "Name")
                            {
                                ai.Name = c.InnerText;
                            }
                            if (c.Name == "RPath")
                            {
                                ai.RPath = c.InnerText;
                            }
                            if (c.Name == "ShaderName")
                            {
                                ai.ShaderName = c.InnerText;
                            }

                        }

                        string fileName = Path.GetFileName(ai.RPath);
                        ai.Folder = ai.RPath.Remove(ai.RPath.Length - fileName.Length, fileName.Length);

                        list.Add(ai);
                    }
                }

                return list;
            }

            return null;
        }

        /// <summary>
        /// 重新设置指定材质的shader。调用结束后，需要调用<see cref="AssetDatabase.SaveAssets()"/>保存修改。
        /// </summary>
        public static int ResetShaders(List<AssetInfo> list)
        {
            int count = 0;
            for (int i = 0; i < list.Count; i++)
            {
                EditorUtility.DisplayProgressBar(c_Dialog_Title, $"Resetting shader: {i} / {list.Count}", i * 1.0f / list.Count);

                AssetInfo info = list[i];
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(info.RPath);
                if (mat == null)
                {
                    Debug.LogError($"Material not found: {info.RPath}");
                    continue;
                }

                if (string.IsNullOrEmpty(info.ShaderName))
                {
                    continue;
                }

                Shader shader = Shader.Find(info.ShaderName);
                if (shader == null)
                {
                    Debug.LogError($"Shader not found: {info.ShaderName}");
                    continue;
                }

                //Unity在设置shader时会将下面两个值重置为默认值，不管新shader是否与原shader相同
                //直接获取renderQueue的话不能取到-1，只能取到具体的值。如果在取值之前材质信息没有保存，则无法取到最新的值
                int oldRenderQueue = mat.renderQueue;
                SerializedObject matInfo = new SerializedObject(mat);
                SerializedProperty propArr = matInfo.FindProperty("m_CustomRenderQueue");
                if (propArr != null)
                {
                    oldRenderQueue = propArr.intValue;
                }
                string oldRenderType = mat.GetTag("RenderType", false);
                mat.shader = shader;
                mat.renderQueue = oldRenderQueue;
                mat.SetOverrideTag("RenderType", oldRenderType);

                count++;
            }

            EditorUtility.ClearProgressBar();
            return count;
        }

        public static List<AssetInfo> FilterByShaderName(List<AssetInfo> source, string[] targetShaderNames, ShaderNameCompareMode compareMode)
        {
            if (source == null)
                return null;

            List<AssetInfo> list = new List<AssetInfo>();
            for (int i = 0; i < source.Count; i++)
            {
                AssetInfo info = source[i];
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(info.RPath);
                if (mat == null)
                {
                    continue;
                }

                bool find = false;
                for (int j = 0; j < targetShaderNames.Length; j++)
                {
                    if (compareMode == ShaderNameCompareMode.FullStringEqual)
                    {
                        if (info.ShaderName.Equals(targetShaderNames[j], StringComparison.OrdinalIgnoreCase))
                        {
                            find = true;
                            break;
                        }
                    }
                    else if (compareMode == ShaderNameCompareMode.StartWith)
                    {
                        if (info.ShaderName.StartsWith(targetShaderNames[j], StringComparison.OrdinalIgnoreCase))
                        {
                            find = true;
                            break;
                        }
                    }
                    else
                    {
                        Debug.LogError("Enum out of Range (type: MaterialInfoCollector.ShaderNameCompareMode, value: " + compareMode + ")");
                        return null;
                    }
                }

                if (find)
                {
                    list.Add(info);
                }
            }

            return list;
        }

        #endregion


        #region Private Functions

        private void MainButtonsGUI()
        {
            EditorGUILayout.BeginHorizontal();
            Color defaultColor = GUI.backgroundColor;
            GUI.backgroundColor = Color.green;
            if (GUILayout.Button("Collect"))
            {
                //dataDictionary = Collect(searchString);
                //foreach (KeyValuePair<string, string> kvp in dataDictionary)
                //{
                //    string fileName = Path.GetFileName(kvp.Key);
                //    AssetInfo a = new AssetInfo();
                //    a.Name = fileName;
                //    a.RPath = kvp.Key.Remove(kvp.Key.Length - fileName.Length, fileName.Length);
                //    //a.GUID = kvp.Value;
                //    a.ShaderName = AssetDatabase.LoadAssetAtPath<Material>(kvp.Key).shader.name;
                //    infoList.Add(a);
                //}

                infoList = CollectMaterialInfo(searchString, materialsPath);
                infoListChanged = true;
            }
            GUI.backgroundColor = defaultColor;

            if (GUILayout.Button("ExportXML"))
            {
                ExportXML(infoList, exportPath);
                AssetDatabase.Refresh();
            }

            if (GUILayout.Button("ReadXML"))
            {
                infoList = ReadXML(exportPath);
                infoListChanged = true;
            }

            GUI.backgroundColor = Color.yellow;
            if (GUILayout.Button("ResetShader"))
            {
                int count = ResetShaders((resetOnlyFilteredShader && useSubList) ? subInfoList : infoList);
                AssetDatabase.SaveAssets();
                EditorUtility.DisplayDialog(c_Dialog_Title, "Reset shader count: " + count, "Ok");
            }
            GUI.backgroundColor = defaultColor;
            EditorGUILayout.EndHorizontal();
        }

        private void FilterButtonsGUI()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("Shader name filter");

            int width = 60;

            if (GUILayout.Button("All", EditorStyles.miniButtonLeft, GUILayout.Width(width)))
            {
                useSubList = false;
                infoListChanged = true;
            }
            if (GUILayout.Button("Error", EditorStyles.miniButtonMid, GUILayout.Width(width)))
            {
                subInfoList = FilterByShaderName(infoList, new[] { "Hidden/InternalErrorShader" }, ShaderNameCompareMode.FullStringEqual);
                useSubList = true;
                infoListChanged = true;
            }
            if (GUILayout.Button("Standard", EditorStyles.miniButtonMid, GUILayout.Width(width)))
            {
                subInfoList = FilterByShaderName(infoList, new[] { "Standard", "Standard (Specular setup)" }, ShaderNameCompareMode.FullStringEqual);
                useSubList = true;
                infoListChanged = true;
            }
            if (GUILayout.Button("URP", EditorStyles.miniButtonMid, GUILayout.Width(width)))
            {
                subInfoList = FilterByShaderName(infoList, new[] { "Universal Render Pipeline/" }, ShaderNameCompareMode.StartWith);
                useSubList = true;
                infoListChanged = true;
            }
            if (GUILayout.Button("S3Unity", EditorStyles.miniButtonRight, GUILayout.Width(width)))
            {
                subInfoList = FilterByShaderName(infoList, new[] { "S3Unity/" }, ShaderNameCompareMode.StartWith);
                useSubList = true;
                infoListChanged = true;
            }
            EditorGUILayout.EndHorizontal();

        }


        private void TreeViewGUI()
        {
            Rect lastRect = GUILayoutUtility.GetLastRect();
            Rect toolbarRect = new Rect(4f, lastRect.y + 22f, position.width - 8f, 20f);
            Rect multiColumnTreeViewRect = new Rect(4, lastRect.y + 42, position.width - 8 , position.height - 150);

            ////Debug
            //GUI.DrawTexture(lastRect, AssetDatabase.LoadAssetAtPath<Texture>("Assets/MaterialTest/AdditionalRes/UV_Grid_GL2.png"), ScaleMode.StretchToFill);
            //GUI.DrawTexture(toolbarRect, AssetDatabase.LoadAssetAtPath<Texture>("Assets/MaterialTest/AdditionalRes/UV_Grid_GL2.png"), ScaleMode.StretchToFill);
            //GUI.DrawTexture(multiColumnTreeViewRect, AssetDatabase.LoadAssetAtPath<Texture>("Assets/MaterialTest/AdditionalRes/UV_Grid_GL2.png"), ScaleMode.StretchToFill);
            //return;

            IList<MIC_TreeViewElement> data = null;
            if (infoListChanged)
            {
                data = ConvertListData(useSubList ? subInfoList : infoList);
            }

            if (!m_initialized)
            {
                if (data == null)
                {
                    data = GetNullListData();
                }
                InitTreeView(multiColumnTreeViewRect, data);
                m_initialized = true;
            }

            SearchBar(toolbarRect);

            if (infoListChanged)
            {
                m_treeView.treeModel.SetData(data);
                m_treeView.Reload();
                infoListChanged = false;
            }
            DoTreeView(multiColumnTreeViewRect);
        }


        #endregion


        #region TreeView

        private void InitTreeView(Rect multiColumnTreeViewRect, IList<MIC_TreeViewElement> data)
        {
            // Check if it already exists (deserialized from window layout file or scriptable object)
            if (m_treeViewState == null)
                m_treeViewState = new TreeViewState();

            bool firstInit = (m_multiColumnHeaderState == null);

            MultiColumnHeaderState headerState = MIC_TreeView.CreateDefaultMultiColumnHeaderState(multiColumnTreeViewRect.width);
            if (MultiColumnHeaderState.CanOverwriteSerializedFields(m_multiColumnHeaderState, headerState))
            {
                MultiColumnHeaderState.OverwriteSerializedFields(m_multiColumnHeaderState, headerState);
            }

            m_multiColumnHeaderState = headerState;

            MultiColumnHeader multiColumnHeader = new MultiColumnHeader(headerState);
            if (firstInit)
            {
                multiColumnHeader.ResizeToFit();
            }

            TreeModel<MIC_TreeViewElement> treeModel = new TreeModel<MIC_TreeViewElement>(data);
            m_treeView = new MIC_TreeView(m_treeViewState, multiColumnHeader, treeModel);

            m_searchField = new SearchField();
            m_searchField.downOrUpArrowKeyPressed += m_treeView.SetFocusAndEnsureSelectedItem;
        }

        private IList<MIC_TreeViewElement> GetNullListData()
        {
            List<MIC_TreeViewElement> list = new List<MIC_TreeViewElement>();
            list.Add(new MIC_TreeViewElement("root", -1, -1, "root", "root"));
            return list;
        }

        private IList<MIC_TreeViewElement> ConvertListData(List<AssetInfo> source)
        {
            List<MIC_TreeViewElement> list = new List<MIC_TreeViewElement>();
            list.Add(new MIC_TreeViewElement("root", -1, -1, "root", "root"));
            for (int i = 0; i < source.Count; i++)
            {
                AssetInfo info = source[i];
                list.Add(new MIC_TreeViewElement(info.Name, 0, i, info.RPath, info.ShaderName));
            }

            // generate some test data
            return list;
        }

        private void SearchBar(Rect rect)
        {
            Rect r = rect;
            r.width = EditorGUIUtility.labelWidth;
            EditorGUI.PrefixLabel(r, new GUIContent("Search in list (not filter)"));
            r = rect;
            r.x += EditorGUIUtility.labelWidth;
            r.width -= EditorGUIUtility.labelWidth;
            
            m_treeView.searchString = m_searchField.OnGUI(r, m_treeView.searchString);
        }

        private void DoTreeView(Rect rect)
        {
            m_treeView.OnGUI(rect);
        }

        [Serializable]
        internal class MIC_TreeViewElement: TreeElement
        {
            public string Path;
            public string ShaderName;

            /// <inheritdoc />
            public MIC_TreeViewElement(string name, int depth, int id, string path, string shaderName) : base(name, depth, id)
            {
                Path = path;
                ShaderName = shaderName;
            }
        }

        internal class MIC_TreeView: TreeViewWithTreeModel<MIC_TreeViewElement>
        {
            public enum AllColumns
            {
                Index,
                Path,
                ShaderName,
            }

            public enum SortOption
            {
                ID,
                Path,
                ShaderName,
            }

            private readonly SortOption[] c_sortOptionsPerColumn = { SortOption.ID, SortOption.Path, SortOption.ShaderName, };

            private const float c_rowHeights = 20f;

            public MIC_TreeView(TreeViewState state, MultiColumnHeader multicolumnHeader, TreeModel<MIC_TreeViewElement> model) : base(state, multicolumnHeader, model)
            {
                Assert.AreEqual(c_sortOptionsPerColumn.Length, Enum.GetValues(typeof(AllColumns)).Length, "Ensure number of sort options are in sync with number of MyColumns enum values");

                // Custom setup
                rowHeight = c_rowHeights;
                columnIndexForTreeFoldouts = 0;
                showAlternatingRowBackgrounds = true;
                showBorder = true;
                // center foldout in the row since we also center content. See RowGUI
                customFoldoutYOffset = (c_rowHeights - EditorGUIUtility.singleLineHeight) * 0.5f;
                //extraSpaceBeforeIconAndLabel = kToggleWidth;
                multicolumnHeader.sortingChanged += OnSortingChanged;
                multicolumnHeader.canSort = true;
                multicolumnHeader.height = MultiColumnHeader.DefaultGUI.defaultHeight;

                Reload();
            }

            public static MultiColumnHeaderState CreateDefaultMultiColumnHeaderState(float treeViewWidth)
            {
                MultiColumnHeaderState.Column[] columns = new[]
                {
                new MultiColumnHeaderState.Column
                {
                    headerContent = GUIContent.none,
                    headerTextAlignment = TextAlignment.Center,
                    sortedAscending = true,
                    sortingArrowAlignment = TextAlignment.Right,
                    width = 34,
                    minWidth = 34,
                    autoResize = false,
                    allowToggleVisibility = false
                },
                new MultiColumnHeaderState.Column
                {
                    headerContent = new GUIContent("MaterialPath", EditorGUIUtility.IconContent("Material Icon").image),
                    contextMenuText = "MaterialPath",
                    headerTextAlignment = TextAlignment.Center,
                    sortedAscending = true,
                    sortingArrowAlignment = TextAlignment.Right,
                    width = 200,
                    minWidth = 200,
                    //maxWidth = 60,
                    autoResize = true,
                    allowToggleVisibility = true
                },
                new MultiColumnHeaderState.Column
                {
                    headerContent = new GUIContent("ShaderName", EditorGUIUtility.IconContent("Shader Icon").image),
                    contextMenuText = "ShaderName",
                    headerTextAlignment = TextAlignment.Center,
                    sortedAscending = true,
                    sortingArrowAlignment = TextAlignment.Right,
                    width = 200,
                    minWidth = 200,
                    //maxWidth = 60,
                    autoResize = true,
                    allowToggleVisibility = true
                }
            };

                Assert.AreEqual(columns.Length, Enum.GetValues(typeof(AllColumns)).Length, "Number of columns should match number of enum values: You probably forgot to update one of them.");

                MultiColumnHeaderState state = new MultiColumnHeaderState(columns);
                return state;
            }

            protected override void RowGUI(RowGUIArgs args)
            {
                TreeViewItem<MIC_TreeViewElement> item = (TreeViewItem<MIC_TreeViewElement>) args.item;

                for (int i = 0; i < args.GetNumVisibleColumns(); ++i)
                {
                    CellGUI(args.GetCellRect(i), item, (AllColumns) args.GetColumn(i), ref args);
                }
            }

            void CellGUI(Rect cellRect, TreeViewItem<MIC_TreeViewElement> item, AllColumns column, ref RowGUIArgs args)
            {
                // Center cell rect vertically (makes it easier to place controls, icons etc in the cells)
                CenterRectUsingSingleLineHeight(ref cellRect);

                Rect r;
                switch (column)
                {
                    case AllColumns.Index:
                        //Draw id
                        r = cellRect;
                        r.x += 2;
                        DefaultGUI.Label(r, args.item.id.ToString(), args.selected, args.focused);
                        break;

                    case AllColumns.Path:
                        r = cellRect;
                        r.x = cellRect.width - 30;
                        r.width = 60;
                        //if (GUI.Button(r, GUIContent.none, GUIStyle.none))
                        if (GUI.Button(r, "Select"))
                        {
                            UnityEngine.Object mat = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(item.data.Path);
                            if (mat != null)
                            {
                                EditorGUIUtility.PingObject(mat);
                                Selection.activeObject = mat;
                            }
                            else
                            {
                                Debug.LogError("Material is null (" + item.data.Path + ")");
                            }
                        }

                        r = cellRect;
                        r.width -= 66;
                        DefaultGUI.Label(r, item.data.Path, args.selected, args.focused);
                        break;

                    case AllColumns.ShaderName:
                        DefaultGUI.Label(cellRect, item.data.ShaderName, args.selected, args.focused);
                        break;
                }
            }

            // Note we We only build the visible rows, only the backend has the full tree information. 
            // The treeview only creates info for the row list.
            protected override IList<TreeViewItem> BuildRows(TreeViewItem root)
            {
                IList<TreeViewItem> rows = base.BuildRows(root);
                SortIfNeeded(root, rows);
                return rows;
            }

            #region TreeViewSort

            public static void TreeToList(TreeViewItem root, IList<TreeViewItem> result)
            {
                if (root == null)
                    throw new NullReferenceException("root");
                if (result == null)
                    throw new NullReferenceException("result");

                result.Clear();

                if (root.children == null)
                    return;

                Stack<TreeViewItem> stack = new Stack<TreeViewItem>();
                for (int i = root.children.Count - 1; i >= 0; i--)
                    stack.Push(root.children[i]);

                while (stack.Count > 0)
                {
                    TreeViewItem current = stack.Pop();
                    result.Add(current);

                    if (current.hasChildren && current.children[0] != null)
                    {
                        for (int i = current.children.Count - 1; i >= 0; i--)
                        {
                            stack.Push(current.children[i]);
                        }
                    }
                }
            }

            void OnSortingChanged(MultiColumnHeader multiColumnHeader)
            {
                SortIfNeeded(rootItem, GetRows());
            }

            void SortIfNeeded(TreeViewItem root, IList<TreeViewItem> rows)
            {
                if (rows.Count <= 1)
                    return;

                if (multiColumnHeader.sortedColumnIndex == -1)
                {
                    return; // No column to sort for (just use the order the data are in)
                }

                // Sort the roots of the existing tree items
                SortByMultipleColumns();
                TreeToList(root, rows);
                Repaint();
            }

            void SortByMultipleColumns()
            {
                int[] sortedColumns = multiColumnHeader.state.sortedColumns;

                if (sortedColumns.Length == 0)
                    return;

                IEnumerable<TreeViewItem<MIC_TreeViewElement>> myTypes = rootItem.children.Cast<TreeViewItem<MIC_TreeViewElement>>();
                IOrderedEnumerable<TreeViewItem<MIC_TreeViewElement>> orderedQuery = InitialOrder(myTypes, sortedColumns);
                for (int i = 1; i < sortedColumns.Length; i++)
                {
                    SortOption sortOption = c_sortOptionsPerColumn[sortedColumns[i]];
                    bool ascending = multiColumnHeader.IsSortedAscending(sortedColumns[i]);

                    switch (sortOption)
                    {
                        case SortOption.ID:
                            orderedQuery = orderedQuery.ThenBy(l => l.data.id, ascending);
                            break;
                        case SortOption.Path:
                            orderedQuery = orderedQuery.ThenBy(l => l.data.Path, ascending);
                            break;
                        case SortOption.ShaderName:
                            orderedQuery = orderedQuery.ThenBy(l => l.data.ShaderName, ascending);
                            break;
                    }
                }

                rootItem.children = orderedQuery.Cast<TreeViewItem>().ToList();
            }

            IOrderedEnumerable<TreeViewItem<MIC_TreeViewElement>> InitialOrder(IEnumerable<TreeViewItem<MIC_TreeViewElement>> myTypes, int[] history)
            {
                SortOption sortOption = c_sortOptionsPerColumn[history[0]];
                bool ascending = multiColumnHeader.IsSortedAscending(history[0]);
                switch (sortOption)
                {
                    case SortOption.ID:
                        return myTypes.Order(l => l.data.id, ascending);
                    case SortOption.Path:
                        return myTypes.Order(l => l.data.Path, ascending);
                    case SortOption.ShaderName:
                        return myTypes.Order(l => l.data.ShaderName, ascending);
                    default:
                        Assert.IsTrue(false, "Unhandled enum");
                        break;
                }

                // default
                return myTypes.Order(l => l.data.name, ascending);
            }

            #endregion

            // Misc
            //--------

            protected override bool CanMultiSelect(TreeViewItem item)
            {
                return true;
            }
        }

        #endregion

    }

}