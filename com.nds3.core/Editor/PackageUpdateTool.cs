using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace EditorExt
{
    /// <summary>
    /// Delete `lock` in json file to manually update git package
    /// </summary>
    public class PackageUpdateTool: EditorWindow
    {
        private const string ManifestPath = "Packages/manifest.json";
        private const string IndependentLockFilePath = "Packages/packages-lock.json";

        private bool useIndependentLockFile = false;
        private string lockFilePath;
        private string lockObjNodeName;
        private Dictionary<string, object> manifestObj;
        private Dictionary<string, object> lockObj;
        /// <summary>
        /// GUI toggle bool values
        /// </summary>
        private List<bool> toggles;
        /// <summary>
        /// The index of packages (GUI toggles) in lock file
        /// </summary>
        private List<int> togglesIndexList;
        private List<KeyValuePair<string, object>> lockObjList;


        [MenuItem("S3插件/PackageUpdateTool")]
        private static void ShowUpdatePackageWindow()
        {
            GetWindow<PackageUpdateTool>("PackageUpdateTool");
        }

        void OnEnable()
        {
            ReadJson();
            if (manifestObj != null)
            {
                if (manifestObj.ContainsKey(lockObjNodeName))
                {
                    lockObj = manifestObj[lockObjNodeName] as Dictionary<string, object>;
                    if (lockObj != null)
                    {
                        //lockObjList[i].Key: com.xxx.xxx
                        //lockObjList[i].Value: {}
                        lockObjList = lockObj.ToList();

                        toggles = new List<bool>();
                        togglesIndexList = new List<int>();

                        for (int i = 0; i < lockObj.Count; i++)
                        {
                            if (useIndependentLockFile)
                            {
                                if (((Dictionary<string, object>) lockObjList[i].Value)["source"].ToString().Equals("git", StringComparison.OrdinalIgnoreCase))
                                {
                                    toggles.Add(false);
                                    togglesIndexList.Add(i);
                                }
                            }
                            else
                            {
                                toggles.Add(false);
                                togglesIndexList.Add(i);
                            }
                        }
                    }
                }
            }
        }

        void OnGUI()
        {
            if (lockObj != null)
            {
                for (int i = 0; i < toggles.Count; i++)
                {
                    toggles[i] = EditorGUILayout.ToggleLeft(lockObjList[togglesIndexList[i]].Key, toggles[i]);
                }

                EditorGUILayout.BeginHorizontal();
                {
                    if (GUILayout.Button("All"))
                    {
                        for (int i = 0; i < toggles.Count; i++)
                        {
                            toggles[i] = true;
                        }
                    }
                    if (GUILayout.Button("None"))
                    {
                        for (int i = 0; i < toggles.Count; i++)
                        {
                            toggles[i] = false;
                        }
                    }
                }
                EditorGUILayout.EndHorizontal();

                if (GUILayout.Button("Update"))
                {
                    for (int i = 0; i < toggles.Count; i++)
                    {
                        if (toggles[i] == true)
                        {
                            Debug.Log("Update package: " + lockObjList[togglesIndexList[i]].Key);
                            lockObj.Remove(lockObjList[togglesIndexList[i]].Key);
                        }
                    }

                    manifestObj[lockObjNodeName] = lockObj;
                    UpdateJson();
                }
            }
            else
            {
                EditorGUILayout.LabelField("No package from Git");
            }
        }

        private void ReadJson()
        {
            //Start since Unity 2019.4.1
            string projectPath = Application.dataPath.Remove(Application.dataPath.Length - 6, 6);
            if (File.Exists(projectPath + IndependentLockFilePath))
            {
                lockFilePath = IndependentLockFilePath;
                useIndependentLockFile = true;
                lockObjNodeName = "dependencies";
            }
            else
            {
                lockFilePath = projectPath + ManifestPath;
                useIndependentLockFile = false;
                lockObjNodeName = "lock";
            }
            manifestObj = Json.Deserialize(File.ReadAllText(lockFilePath)) as Dictionary<string, object>;
        }

        private void UpdateJson()
        {
            File.WriteAllText(lockFilePath, Json.Serialize(manifestObj, true));
            EditorApplication.delayCall += () => AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
        }
    }
}

/**
 * FileREF: new Packages\packages-lock.json
    {
      "dependencies": {
        "com.nds3.008shader": {
          "version": "http://192.168.243.133:10080/S3Unity/com.nds3.008shader.git",
          "depth": 0,
          "source": "git",
          "dependencies": {},
          "hash": "b3c24b908376cb6398e1c5ace98ada5a887743be"
        },
        "com.nds3.c3skin": {
          "version": "http://192.168.243.133:10080/S3Unity/com.nds3.c3skin.git",
          "depth": 0,
          "source": "git",
          "dependencies": {},
          "hash": "f17068057243c7caad6c66c803fe02d00b0fcdc1"
        },
        "com.unity.collab-proxy": {
          "version": "1.2.16",
          "depth": 0,
          "source": "registry",
          "dependencies": {},
          "url": "https://packages.unity.com"
        },
        "com.unity.ide.vscode": {
          "version": "1.2.1",
          "depth": 0,
          "source": "registry",
          "dependencies": {},
          "url": "https://packages.unity.com"
        },
        "com.unity.postprocessing": {
          "version": "2.2.2",
          "depth": 1,
          "source": "registry",
          "dependencies": {},
          "url": "https://packages.unity.com"
        },
        "com.unity.ugui": {
          "version": "1.0.0",
          "depth": 0,
          "source": "builtin",
          "dependencies": {
            "com.unity.modules.ui": "1.0.0"
          }
        },
        "com.unity.modules.xr": {
          "version": "1.0.0",
          "depth": 0,
          "source": "builtin",
          "dependencies": {
            "com.unity.modules.physics": "1.0.0",
            "com.unity.modules.jsonserialize": "1.0.0",
            "com.unity.modules.subsystems": "1.0.0"
          }
        }
      }
    }

 */

/**
 * FileREF: old Packages\manifest.json
    {
      "dependencies": {
        "com.nds3.008shader": "http://192.168.243.133:10080/S3Unity/com.nds3.008shader.git",
        "com.nds3.core": "http://192.168.243.133:10080/S3Unity/com.nds3.core.git",
        "com.nds3.custom_material_editor": "http://192.168.243.133:10080/S3Unity/com.nds3.custom_material_editor.git",
        "com.unity.collab-proxy": "1.2.16",
        "com.unity.ide.rider": "1.1.4",
        "com.unity.ide.vscode": "1.2.0",
        "com.unity.test-framework": "1.1.14",
        "com.unity.textmeshpro": "2.0.1",
        "com.unity.timeline": "1.2.14",
        "com.unity.ugui": "1.0.0",
        "com.unity.modules.ai": "1.0.0",
        "com.unity.modules.xr": "1.0.0"
      },
      "lock": {
        "com.nds3.core": {
          "revision": "HEAD",
          "hash": "7b810ff90f79a7f1ec2f0de8846635018aaf10ea"
        },
        "com.nds3.008shader": {
          "revision": "HEAD",
          "hash": "0f5efc846916475b32e6b122439e21c8f2ffe14e"
        },
        "com.nds3.custom_material_editor": {
          "revision": "HEAD",
          "hash": "8bc65c2304e922c5144bf23e05fdf39996c7f166"
        }
      }
    }
 */
