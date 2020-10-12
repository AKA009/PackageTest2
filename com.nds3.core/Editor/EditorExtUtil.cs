using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace EditorExt
{
    public class EditorExtUtil
    {
        /// <summary>
        /// A copy of <see cref="UnityEditor.SavedBool"/>
        /// </summary>
        public class SavedBool
        {
            bool m_Value;
            string m_Name;
            bool m_Loaded;

            public SavedBool(string name, bool value)
            {
                m_Name = name;
                m_Loaded = false;
                m_Value = value;
            }

            private void Load()
            {
                if (m_Loaded)
                    return;

                m_Loaded = true;
                m_Value = EditorPrefs.GetBool(m_Name, m_Value);
            }

            public bool value
            {
                get { Load(); return m_Value; }
                set
                {
                    Load();
                    if (m_Value == value)
                        return;
                    m_Value = value;
                    EditorPrefs.SetBool(m_Name, value);
                }
            }

            public static implicit operator bool(SavedBool s)
            {
                return s.value;
            }
        }


        /// <summary>
        /// 按Float精度输出V4，默认的toString只保留2位
        /// </summary>
        public static string LogVector4(Vector4 v)
        {
            return $"({v.x}, {v.y}, {v.z}, {v.w})";
        }

        /// <summary>
        /// 读取指定路径下的全部Unity资源
        /// </summary>
        /// <typeparam name="T">资源类型，必须是<see cref="UnityEngine.Object"/></typeparam>
        /// <param name="path">路径，可以为相对路径</param>
        /// <param name="includeExt">包含的文件扩展名，不能是.meta</param>
        /// <returns>如果路径不存在则返回null</returns>
        public static List<T> LoadAllAsset<T>(string path, params string[] includeExt) where T : UnityEngine.Object
        {
            if (Directory.Exists(path))
            {
                DirectoryInfo directoryInfo = new DirectoryInfo(path);
                FileInfo[] fileInfos = directoryInfo.GetFiles("*");
                List<T> list = new List<T>();

                for (int i = 0; i < fileInfos.Length; i++)
                {
                    FileInfo f = fileInfos[i];
                    string ext = Path.GetExtension(f.Name);

                    if (ext.Equals(".meta", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    //如果是指定的扩展名之一则进行加载
                    bool willInc = false;
                    for (int incIndex = 0; incIndex < includeExt.Length; incIndex++)
                    {
                        if (ext.Equals(includeExt[incIndex], StringComparison.OrdinalIgnoreCase))
                        {
                            willInc = true;
                            break;
                        }
                    }

                    if (!willInc)
                    {
                        continue;
                    }

                    T asset = AssetDatabase.LoadAssetAtPath<T>(path + "/" + f.Name);
                    list.Add(asset);
                }

                return list;
            }

            return null;
        }

        /// <summary>
        /// 读取指定路径下的全部文件的信息
        /// </summary>
        /// <param name="path">路径，可以为相对路径</param>
        /// <param name="includeExt">包含的文件扩展名，不能是.meta</param>
        /// <returns>如果路径不存在则返回null</returns>
        public static List<FileInfo> GetAllFileInfo(string path, params string[] includeExt)
        {
            if (Directory.Exists(path))
            {
                DirectoryInfo directoryInfo = new DirectoryInfo(path);
                FileInfo[] fileInfos = directoryInfo.GetFiles("*", SearchOption.AllDirectories);
                List<FileInfo> list = new List<FileInfo>();

                for (int i = 0; i < fileInfos.Length; i++)
                {
                    FileInfo f = fileInfos[i];
                    string ext = Path.GetExtension(f.Name);

                    if (ext.Equals(".meta", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    //如果是指定的扩展名之一则进行加载
                    bool willInc = false;
                    for (int incIndex = 0; incIndex < includeExt.Length; incIndex++)
                    {
                        if (ext.Equals(includeExt[incIndex], StringComparison.OrdinalIgnoreCase))
                        {
                            willInc = true;
                            break;
                        }
                    }

                    if (!willInc)
                    {
                        continue;
                    }

                    list.Add(f);
                }

                return list;
            }

            return null;
        }

        /// <summary>
        /// 读取指定路径下的全部文件的相对路径
        /// </summary>
        /// <param name="path">路径，可以为相对路径</param>
        /// <param name="includeExt">包含的文件扩展名，不能是.meta</param>
        /// <returns>如果路径不存在则返回null</returns>
        public static List<string> GetAllFileRelativePath(string path, params string[] includeExt)
        {
            if (Directory.Exists(path))
            {
                string[] files = Directory.GetFiles(path, "*", SearchOption.AllDirectories);
                List<string> list = new List<string>();

                for (int i = 0; i < files.Length; i++)
                {
                    string f = files[i];
                    string ext = Path.GetExtension(f);

                    if (ext == null || ext.Equals(".meta", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    //如果是指定的扩展名之一则进行加载
                    bool willInc = false;
                    for (int incIndex = 0; incIndex < includeExt.Length; incIndex++)
                    {
                        if (ext.Equals(includeExt[incIndex], StringComparison.OrdinalIgnoreCase))
                        {
                            willInc = true;
                            break;
                        }
                    }

                    if (!willInc)
                    {
                        continue;
                    }

                    list.Add(f);
                }

                return list;
            }

            return null;
        }

        /// <summary>
        /// 用来获取路径的控件，拖动文件夹到选择框即可获得路径，选择框内没有内容。
        /// </summary>
        /// <param name="label"></param>
        /// <param name="target"></param>
        public static void UI_PathField(string label, ref string target)
        {
            EditorGUILayout.LabelField(label + " (Folder)");
            EditorGUILayout.BeginHorizontal();
            {
                EditorGUI.BeginChangeCheck();
                UnityEngine.Object pathObj = null;
                // ReSharper disable once ExpressionIsAlwaysNull
                pathObj = EditorGUILayout.ObjectField(pathObj, typeof(UnityEngine.Object), false, GUILayout.Width(110));
                if (EditorGUI.EndChangeCheck())
                {
                    target = AssetDatabase.GetAssetPath(pathObj);
                    //判断是否为斜杠结尾，搜索的文件夹不能用斜杠结尾
                    if (target.EndsWith("/"))
                    {
                        target = target.Remove(target.Length - 2, 1);
                    }

                    // ReSharper disable once RedundantAssignment
                    pathObj = null;
                }

                target = EditorGUILayout.TextField(target);
            }
            EditorGUILayout.EndHorizontal();
        }

        public enum SavingTextureFormat
        {
            JPG,
            PNG,
            TGA,
            EXR
        }

        /// <summary>
        /// 将一个纹理对象保存为文件。要求<see cref="Texture2D.isReadable"/>开启
        /// </summary>
        /// <param name="texture"></param>
        /// <param name="filePathWithoutExt">文件路径，不包含扩展名</param>
        /// <param name="format"></param>
        /// <param name="destroyAfterSave">是否保存后销毁原texture，调用<c>DestroyImmediate</c></param>
        /// <returns>生成文件的路径</returns>
        public static string SaveTexture2DtoFile(Texture2D texture, string filePathWithoutExt, SavingTextureFormat format, bool destroyAfterSave)
        {
            string path = Path.GetDirectoryName(filePathWithoutExt);
            string fn = Path.GetFileName(filePathWithoutExt);
            return SaveTexture2DtoFile(texture, path, fn, format, destroyAfterSave);
        }
        /// <summary>
        /// 将一个纹理对象保存为文件。要求<see cref="Texture2D.isReadable"/>开启
        /// </summary>
        /// <param name="texture"></param>
        /// <param name="path">路径名，不以斜杠结尾</param>
        /// <param name="fileName">文件名，不包含扩展名</param>
        /// <param name="format"></param>
        /// <param name="destroyAfterSave">是否保存后销毁原texture，调用<c>DestroyImmediate</c></param>
        /// <returns>生成文件的路径</returns>
        public static string SaveTexture2DtoFile(Texture2D texture, string path, string fileName, SavingTextureFormat format, bool destroyAfterSave)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
            if (!Directory.Exists(path))
            {
                EditorUtility.DisplayDialog("Error", "SaveTexture2DtoFile: Cannot create path " + path, "OK");
                return null;
            }

            byte[] bytes;
            string fName;
            switch (format)
            {
                case SavingTextureFormat.JPG:
                    bytes = texture.EncodeToJPG();
                    fName = path + "/" + fileName + ".jpg";
                    break;

                case SavingTextureFormat.PNG:
                    bytes = texture.EncodeToPNG();
                    fName = path + "/" + fileName + ".png";
                    break;

                case SavingTextureFormat.TGA:
                    bytes = texture.EncodeToTGA();
                    fName = path + "/" + fileName + ".tga";
                    break;

                case SavingTextureFormat.EXR:
                    bytes = texture.EncodeToEXR();
                    fName = path + "/" + fileName + ".exr";
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(format), format, null);
            }

            FileStream file = File.Open(fName, FileMode.Create);
            BinaryWriter writer = new BinaryWriter(file);
            writer.Write(bytes);
            file.Close();
            if (destroyAfterSave)
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
            return fName;
        }

        /// <summary>
        /// 开关某材质的指定宏，可以同时设置同名的float值，避免材质GUI显示错误
        /// </summary>
        /// <param name="material"></param>
        /// <param name="keyword"></param>
        /// <param name="state"></param>
        /// <param name="alsoSetNumberValue">是否同时设置和宏同名的float值</param>
        public static void SetMaterialKeyword(Material material, string keyword, bool state, bool alsoSetNumberValue)
        {
            if (state)
            {
                material.EnableKeyword(keyword);
                if (alsoSetNumberValue)
                {
                    material.SetFloat(keyword, 1);
                }
            }
            else
            {
                material.DisableKeyword(keyword);
                if (alsoSetNumberValue)
                {
                    material.SetFloat(keyword, 0);
                }
            }
        }
    }
}