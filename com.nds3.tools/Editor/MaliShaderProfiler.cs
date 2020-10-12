//#define DEBUG_ON

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace EditorExt.Tools
{
    public class MaliShaderProfiler: EditorWindow
    {
        private static MaliShaderProfiler s_activeWindow = null;

        [MenuItem("[美术用]/S3插件/MaliShaderProfiler")]
        private static void ShowWindow()
        {
            s_activeWindow = GetWindow<MaliShaderProfiler>("ShaderProfiler");
        }

        ///////////////////////////////////////////////////////////////
        ///////////////////////////////////////////////////////////////

        private struct ShaderSource
        {
            public string VertCode;
            public string FragCode;
            public string KeyWords;

            /// <inheritdoc />
            public override string ToString()
            {
                string r = "=== [ ShaderSource ] ===";
                r += "\n--- KeyWords ---\n" + KeyWords;
                r += "\n--- VertCode ---\n" + VertCode;
                r += "\n--- FragCode ---\n" + FragCode;
                //r += "\nKeyWords: \n" + KeyWords;
                return r;
            }
        }

        private struct ShaderFunctionResult
        {
            public string WorkRegisters;
            public string UniformRegisters;
            public string A;
            public string LS;
            public string T;

            /// <inheritdoc />
            public override string ToString()
            {
                //return $"WR: {WorkRegisters}, UR: {UniformRegisters}, A: {A}, LS: {LS}, T: {T}";
                return $"\tWR: {WorkRegisters},\tUR: {UniformRegisters},\tA: {A},\tLS: {LS},\tT: {T}";
            }
        }

        private const string SHADER_NAME_TOKEN = "##ShaderName";

        private string m_exePath;
        private string m_logPath;
        private string m_tempPath;
        private string m_vertCodeFilePath;
        private string m_fragCodeFilePath;
        private string m_cmdArgsHead;

        private bool m_insertShaderName;
        private bool m_useSourceFormat;
        private Vector2 m_scrollViewRect;

        //Internal data
        private Shader m_selectedShader = null;
        private string m_compiledShaderFilePath;
        private StringBuilder m_logFileSB = new StringBuilder();

        private static readonly Type ShaderUtilType = typeof(ShaderUtil);

        void OnEnable()
        {
            titleContent = new GUIContent("MSP", EditorGUIUtility.FindTexture("Shader Icon"));
            m_exePath = "malisc.exe";
            m_logPath = "D:/msp_" + SHADER_NAME_TOKEN + "_log.log";
            m_tempPath = Application.dataPath.Substring(0, Application.dataPath.LastIndexOf('/')) + "/Temp/";
            m_vertCodeFilePath = m_tempPath + "msp_vertCode.vert";
            m_fragCodeFilePath = m_tempPath + "msp_fragCode.frag";
            m_cmdArgsHead = "--core Mali-G72 --revision r0p3 --driver Mali-Gxx_r11p0-00rel0";
            m_insertShaderName = true;
            m_useSourceFormat = false;
        }

        void OnGUI()
        {
            EditorGUILayout.LabelField("Exe Path");
            m_exePath = EditorGUILayout.TextField(m_exePath);
            EditorGUILayout.LabelField("Log Path");
            m_logPath = EditorGUILayout.TextField(m_logPath);

            EditorGUILayout.Space();//----------------------------------------------

            UI_ReadOnlyPathField("Temp Path", ref m_tempPath);
            UI_ReadOnlyPathField("Vert Code File Path", ref m_vertCodeFilePath);
            UI_ReadOnlyPathField("Frag Code File Path", ref m_fragCodeFilePath);

            EditorGUILayout.Space();//----------------------------------------------

            EditorGUILayout.LabelField("Cmd Args Head");
            m_cmdArgsHead = EditorGUILayout.TextField(m_cmdArgsHead);

            EditorGUILayout.LabelField("Shader");
            m_selectedShader = EditorGUILayout.ObjectField(m_selectedShader, typeof(Shader), false) as Shader;

            EditorGUILayout.Space();//----------------------------------------------

            m_insertShaderName = EditorGUILayout.ToggleLeft("Insert shader name in log file's name", m_insertShaderName);
            m_useSourceFormat = EditorGUILayout.ToggleLeft("Use source format", m_useSourceFormat);

            UI_Button("Compile", OnCompileBtnClick);
            UI_Button("Profile", OnProfileBtnClick);

            EditorGUILayout.BeginHorizontal();
            {
                if (GUILayout.Button("Help", GUI.skin.GetStyle("buttonLeft"), GUILayout.MinHeight(30)))
                {
                    GetWindowWithRect<MSP_HelpWindow>(new Rect(0, 0, 400, 400), true, "MSP_Help");
                }
                if (GUILayout.Button("Log File", GUI.skin.GetStyle("buttonMid"), GUILayout.MinHeight(30)))
                {
                    WriteToFile(m_logPath, m_logFileSB);
                    EditorUtility.DisplayDialog("MSP", "WriteToFile: " + m_logPath, "OK");
                }
                if (GUILayout.Button("Test", GUI.skin.GetStyle("buttonRight"), GUILayout.MinHeight(30)))
                {
                    OnTestBtnClick();
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();//----------------------------------------------

            m_scrollViewRect = EditorGUILayout.BeginScrollView(m_scrollViewRect);
            {
                EditorGUILayout.TextArea(m_logFileSB.ToString());
            }
            EditorGUILayout.EndScrollView();
        }

        private void OnCompileBtnClick()
        {
            if (m_selectedShader == null)
            {
                EditorUtility.DisplayDialog("Error", "Shader is null.", "OK");
                return;
            }

            OpenCompiledShader(m_selectedShader, 3, 736973, false);
        }

        private void OnProfileBtnClick()
        {
            if (m_logFileSB == null)
            {
                m_logFileSB = new StringBuilder();
            }
            else
            {
                m_logFileSB.Clear();
            }
            if (m_selectedShader == null)
            {
                EditorUtility.DisplayDialog("Error", "Shader is null.", "OK");
                Debug.LogError("m_selectedShader is null");
                return;
            }
            LogFile("Selected Shader: " + m_selectedShader);
            m_compiledShaderFilePath = m_tempPath + "Compiled-" + ParseShaderNameToCommon(m_selectedShader.name) + ".shader";
            LogFile("Compiled Shader File: " + m_compiledShaderFilePath);
            LogFile("");

            if (!File.Exists(m_compiledShaderFilePath))
            {
                EditorUtility.DisplayDialog("Error", "CompiledShaderFile not exist.", "OK");
                Debug.LogError("CompiledShaderFile not exist");
                return;
            }

            EditorUtility.DisplayProgressBar("MSP", "Profiling Shader", 0);
            List<ShaderSource> sourceList = DealShader(m_compiledShaderFilePath);

            if (sourceList == null)
            {
                EditorUtility.ClearProgressBar();
                EditorUtility.DisplayDialog("Error", "Cannot get pass code.", "OK");
                return;
            }

            //for (int i = 0; i < sourceList.Count; i++)
            //{
            //    Debug.Log(sourceList[i]);
            //}

            ProfileShader(sourceList, m_exePath, m_cmdArgsHead, m_vertCodeFilePath, m_fragCodeFilePath);
            //在Log文件名中加入Shader名
            if (m_insertShaderName)
            {
                m_logPath = m_logPath.Replace(SHADER_NAME_TOKEN, ParseShaderNameToCommon(m_selectedShader.name));
            }
            else
            {
                m_logPath = m_logPath.Replace("_" + SHADER_NAME_TOKEN, "");
            }
            
            EditorUtility.ClearProgressBar();
        }

        private void OnTestBtnClick()
        {
            //string result = "Instructions Emitted:   26.6      5       0       A";
            //Regex regex = new Regex("(((\\d+|no) work registers used)|(work registers not used))(.|\n)*?(?=\nA = Arithmetic)");
            //MatchCollection matches = regex.Matches(result);

            //const string p_num = "(\\d*\\.\\d*|\\d*|N/A)";
            //regex = new Regex("Instructions Emitted:\\s*" + p_num + "\\s*" + p_num + "\\s*" + p_num);
            //matches = regex.Matches(result);
            //if (matches.Count > 0)
            //{
            //    Match m = matches[0];

            //    for (int i = 0; i < m.Groups.Count; i++)
            //    {
            //        LogGreen(m.Groups[i].Value);
            //    }
            //}



            const string testResultPath = "D:/testResult.txt";

            string content = File.ReadAllText("D:/fuck.txt");
            string testResult = "";

            Regex regex = new Regex(" Pass {(.|\n)*?( Pass {){1}");
            MatchCollection matches = regex.Matches(content);

            if (matches.Count > 0)
            {
                testResult = matches[0].ToString();
            }

            //覆盖旧文件
            if (File.Exists(testResultPath))
            {
                FileStream file = File.Create(testResultPath);
                file.Close();
            }
            File.WriteAllBytes(testResultPath, Encoding.Default.GetBytes(testResult));

        }

        /// <summary>
        /// 把编译好的Shader拆分成每个变种，并提取出vs和ps代码
        /// </summary>
        /// <param name="compiledShaderFilePath">Unity编译的Shader文件</param>
        /// <returns></returns>
        private List<ShaderSource> DealShader(string compiledShaderFilePath)
        {
            /** 参考：编译的shader文件结构
             *
             * 每个变种代码结构如下：
             * //////////////////////////////////////////////////////
             * No keywords set in this variant. 或
             * Keywords set in this variant: _XXX _XX _XXX_XXX_ON
             * -- Hardware tier variant: Tier 1
             * -- Vertex shader for "gles3":
             * Shader Disassembly:
             * #ifdef VERTEX
             * #version 300 es
             * 
             * 顶点声明
             * void main()
             * {
             *     顶点函数
             * }
             * 
             * #endif
             * #ifdef FRAGMENT
             * #version 300 es
             * 
             * precision highp float;
             * precision highp int;
             * 
             * 片段声明
             * void main()
             * {
             *     片段函数
             * }
             * 
             * #endif
             * 
             * 
             * -- Hardware tier variant: Tier 1
             * -- Fragment shader for "gles3":
             * Shader Disassembly:
             * // All GLSL source is contained within the vertex program
             *
             *
             * 没有vs或ps的变种如下：
             * //////////////////////////////////////////////////////
             * Keywords set in this variant: DIRECTIONAL _ALPHATEST_MODE2 
             * -- Vertex shader for "gles3":
             * // No shader variant for this keyword set. The closest match will be used instead.
             *
             * -- Hardware tier variant: Tier 1
             * -- Fragment shader for "gles3":
             * Shader Disassembly:
             * // All GLSL source is contained within the vertex program
             *
             */

            /*
             *  regex匹配从`Keywords set in this variant: _XXX`到结尾`-- Fragment shader`的所有代码，
             *  所以从第一个冒号到换行就能取出该变种所有开启的宏
             *  #endif最后有四行多余：
             *  \n
             *  \n  
             *  --Hardware tier variant: Tier 1
             *  --Fragment shader
             *
             *  关键字用'\n'分割，没有为NoKeyWords
             */

            string content = File.ReadAllText(compiledShaderFilePath);

            content = GetFirstPass(content);
            if (content == null)
            {
                return null;
            }

            Regex regex = new Regex("((Keywords)|(No keywords))(.|\n)*?-- Fragment shader");
            MatchCollection matches = regex.Matches(content);

            List<ShaderSource> sourceList = new List<ShaderSource>();
            EditorUtility.DisplayProgressBar("MSP", "Profiling Shader", 1f/(2 * matches.Count + 1));
            for (int i = 0; i < matches.Count; i++)
            {
                ShaderSource source = new ShaderSource();

                string text = matches[i].ToString();
                int startIndex = text.IndexOf(": ", StringComparison.Ordinal);
                int endNum = text.IndexOf("\n", StringComparison.Ordinal);
                if (startIndex != -1 && startIndex < endNum)
                {
                    startIndex += 2;
                    string result = text.Substring(startIndex, endNum - startIndex);
                    string[] allKeys = result.Split(new[] { " " }, StringSplitOptions.RemoveEmptyEntries);
                    source.KeyWords = allKeys[0];
                    for (int j = 1; j < allKeys.Length; j++)
                    {
                        source.KeyWords += "\n" + allKeys[j];
                    }
                    //si.keyWords = si.keyWords.Replace(" ", "\n");
                }
                else
                {
                    source.KeyWords = "NoKeyWords";
                }

                //提取顶点代码
                startIndex = text.IndexOf("#ifdef VERTEX", StringComparison.Ordinal) + 13;
                endNum = text.IndexOf("#ifdef FRAGMENT", StringComparison.Ordinal) - 7;
                //如果没有顶点函数则不进行分析
                if (startIndex == 12 || endNum == -8)
                {
                    EditorUtility.DisplayProgressBar("MSP", "Profiling Shader", (1f + i) / (2 * matches.Count + 1));
                    continue;
                }
                source.VertCode = text.Substring(startIndex, endNum - startIndex);
                //去除文件开头可能存在的空行
                //ERROR: 0:2: P0005: #version must be on the first line in a program and only whitespace are allowed in the declaration
                if (source.VertCode.StartsWith("\n"))
                {
                    source.VertCode = source.VertCode.Remove(0, 1);
                }

                //提取片段代码
                startIndex = text.IndexOf("#ifdef FRAGMENT", StringComparison.Ordinal) + 16;
                endNum = text.LastIndexOf("#endif", StringComparison.Ordinal);
                //如果没有顶点函数则不进行分析
                if (startIndex == 15 || endNum == -1)
                {
                    EditorUtility.DisplayProgressBar("MSP", "Profiling Shader", (1f + i) / (2 * matches.Count + 1));
                    continue;
                }
                source.FragCode = text.Substring(startIndex, endNum - startIndex);
                //如果不存在" float;"，则在开头添加"precision highp float;"
                if (!source.FragCode.Contains("float;\n"))
                {
                    string first = source.FragCode.Substring(0, source.FragCode.IndexOf('\n') + 1);
                    startIndex = source.FragCode.IndexOf("precision", StringComparison.Ordinal);
                    string third = source.FragCode.Substring(startIndex);
                    source.FragCode = first + "precision highp float;\n" + third;
                }

                sourceList.Add(source);
                EditorUtility.DisplayProgressBar("MSP", "Profiling Shader", (1f + i) / (2 * matches.Count + 1));
            }
            return sourceList;
        }

        /// <summary>
        /// 执行malisc进行Shader分析，并处理和输出结果
        /// </summary>
        /// <param name="sourceList">处理过的Shader源码数据</param>
        /// <param name="exePath">malisc执行路径</param>
        /// <param name="cmdArgsHead">命令行参数头</param>
        /// <param name="vertCodeFilePath">顶点代码文件缓存路径</param>
        /// <param name="fragCodeFilePath">片段代码文件缓存路径</param>
        private void ProfileShader(List<ShaderSource> sourceList, string exePath, string cmdArgsHead, string vertCodeFilePath, string fragCodeFilePath)
        {
            //参数验证
            if (sourceList == null)
            {
                Debug.LogError("ProfileShader.sourceList is null");
                return;
            }
            if (sourceList.Count <= 0)
            {
                Debug.LogError("ProfileShader.sourceList is empty");
                return;
            }

            //覆盖旧文件
            if (File.Exists(vertCodeFilePath))
            {
                FileStream file = File.Create(vertCodeFilePath);
                file.Close();
            }
            if (File.Exists(fragCodeFilePath))
            {
                FileStream file = File.Create(fragCodeFilePath);
                file.Close();
            }

            LogFile("# Profile Result");
            LogFile("------------------------");

            for (int i = 0; i < sourceList.Count; i++)
            {
                EditorUtility.DisplayProgressBar("MSP", "Profiling Shader", (1f + sourceList.Count + i - 1f) / (2 * sourceList.Count + 1));
                ShaderSource source = sourceList[i];

                File.WriteAllBytes(vertCodeFilePath, Encoding.Default.GetBytes(source.VertCode));
                File.WriteAllBytes(fragCodeFilePath, Encoding.Default.GetBytes(source.FragCode));

                string vsR = BeginSCProcess(exePath, cmdArgsHead + " --vertex " + vertCodeFilePath);
                string psR = BeginSCProcess(exePath, cmdArgsHead + " --fragment " + fragCodeFilePath);

                string vsDR = DealResult(vsR);
                string psDR = DealResult(psR);

                if (vsR.Contains("Compilation failed"))
                {
                    vsDR = DealError(vsR);
                }
                else
                {
                    vsDR = m_useSourceFormat ? DealResult(vsR) : DealResult2(vsR);
                }
                if (psR.Contains("Compilation failed"))
                {
                    psDR = DealError(psR);
                }
                else
                {
                    psDR = m_useSourceFormat ? DealResult(psR) : DealResult2(psR);
                }

                //输出结果
                if (m_useSourceFormat)
                {
                    LogFile("## KW: " + source.KeyWords.Replace("\n", " "));
                    LogFile("VertexFunction: ");
                    LogFile("");
                    LogFile(vsDR);
                    LogFile("");
                    LogFile("FragmentFunction: ");
                    LogFile("");
                    LogFile(psDR);
                    LogFile("");
                }
                else
                {
                    LogFile("## KW: " + source.KeyWords.Replace("\n", " "));
                    LogFile("VS-- " + vsDR);
                    LogFile("PS-- " + psDR);
                    LogFile("");
                }

                EditorUtility.DisplayProgressBar("MSP", "Profiling Shader", (1f + sourceList.Count + i) / (2 * sourceList.Count + 1));
            }

            LogFile("# Note");
            LogFile("------------------------");
            LogFile("- WR = Work registers used, UR = Uniform registers used");
            LogFile("- A = Arithmetic, L/S = Load/Store, T = Texture");
            LogFile("- The cycles counts do not include possible stalls due to cache misses.");
            LogFile("- Shaders with loops may return \"N/A\" for cycle counts if the number of cycles cannot be statically determined.");
            LogFile("- Spilling not used.");
            LogFile("");
            LogFile("# CopyRight");
            LogFile("------------------------");
            LogFile("Arm Mali Offline Compiler v6.4.0");
            LogFile("(C) Copyright 2007 - 2018 Arm, Ltd.");
            LogFile("All rights reserved.");

            EditorUtility.DisplayProgressBar("MSP", "Profiling Shader", 1);
        }

        /// <summary>
        /// 创建进程执行分析，并返回程序的输出结果，执行完后关闭进程
        /// </summary>
        /// <param name="exePath">执行路径</param>
        /// <param name="args">传入参数</param>
        /// <returns>程序的输出结果</returns>
        private string BeginSCProcess(string exePath, string args)
        {
            Process myProcess = new Process
            {
                StartInfo =
                {
                    FileName = exePath,
                    Arguments = args,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardInput = true,  // 重定向输入    
                    RedirectStandardOutput = true, // 重定向标准输出    
                    RedirectStandardError = true,  // 重定向错误输出
                }
            };
            myProcess.Start();

            string result = myProcess.StandardOutput.ReadToEnd();
            myProcess.WaitForExit();//参数单位毫秒，在指定时间内没有执行完则强制结束，不填写则无限等待
            myProcess.Close();

            //LogGreen("SCProcess exit, args: " + args);
            //LogGreen(result);

            return result;
        }

        /// <summary>
        /// 获取编译完成的shader的第一个pass，即从第一个 Pass {到第二个 Pass {。如果只有一个pass则返回原值
        /// </summary>
        private string GetFirstPass(string content)
        {
            Regex regex = new Regex(" Pass {(.|\n)*?( Pass {){1}");
            MatchCollection matches = regex.Matches(content);

            if (matches.Count > 0)
            {
                return matches[0].ToString();
            }
            else
            {
                return content;
            }
        }

        /// <summary>
        /// 对输出结果进行处理，提取出分析结果
        /// </summary>
        /// <param name="result">输出结果原文</param>
        /// <returns>分析结果</returns>
        private string DealResult(string result)
        {
            /** 参考：输出结果结构（不指定硬件和驱动）
             *
             *  > malisc msp_vertCode.vert
             *
             *  Arm Mali Offline Compiler v6.4.0
             *  (C) Copyright 2007-2018 Arm, Ltd.
             *  All rights reserved.
             *
             *  Source type was inferred as OpenGL ES Vertex Shader.
             *  No driver specified, using "Mali-Gxx_r11p0-00rel0" as default.
             *
             *  No core specified, using "Mali-G72" as default.
             *
             *  No core revision specified, using "r0p3" as default.
             *
             *
             *  64 work registers used, 70 uniform registers used, spilling not used.
             *
             *                          A       L/S     T
             *  Instructions Emitted:   17.5    14      0
             *  Shortest Path Cycles:   17.5    14      0
             *  Longest Path Cycles:    17.5    14      0
             *
             *  A = Arithmetic, L/S = Load/Store, T = Texture
             *  Note: The cycles counts do not include possible stalls due to cache misses.
             *  Note: Shaders with loops may return "N/A" for cycle counts if the number of cycles cannot be statically determined.
             *
             *  Compilation succeeded.
             *
             */

            /** 参考：输出结果结构（指定运行环境）
             *
             *  > malisc --core Mali-T600 --revision r0p0_15dev0 --driver Mali-T600_r23p0-00rel0 msp_vertCode.vert
             *
             *  Arm Mali Offline Compiler v6.4.0
             *  (C) Copyright 2007-2018 Arm, Ltd.
             *  All rights reserved.
             *
             *  Source type was inferred as OpenGL ES Vertex Shader.
             *
             *  7 work registers used, 16 uniform registers used, spilling not used.
             *
             *                          A       L/S     T       Bound
             *  Instructions Emitted:   26      5       0       A
             *  Shortest Path Cycles:   13.5    5       0       A
             *  Longest Path Cycles:    13.5    5       0       A
             *
             *  A = Arithmetic, L/S = Load/Store, T = Texture
             *  Note: The cycles counts do not include possible stalls due to cache misses.
             *  Note: Shaders with loops may return "N/A" for cycle counts if the number of cycles cannot be statically determined.
             *
             *  Compilation succeeded.
             *
             */

            /*
             *  匹配
             *  ？或 no work registers used 或 work registers not used, 16 uniform registers used, spilling not used.
             *
             *                          A       L/S     T       Bound
             *  Instructions Emitted:   26      5       0       A
             *  Shortest Path Cycles:   13.5    5       0       A
             *  Longest Path Cycles:    13.5    5       0       A
             *
             */

            //TODO:处理RenderTexture x 的问题，将(.|\n)*?中的问号去掉即可获取
            Regex regex = new Regex("(((\\d+|no) work registers used)|(work registers not used))(.|\n)*(?=\nA = Arithmetic)");
            MatchCollection matches = regex.Matches(result);
            string final = "";
            if (matches.Count > 0)
            {
                final = matches[0].ToString();

                //将表头中A前面的三个制表符换成固定的21个空格和一个制表符，
                //用来解决Log文件在不同配置的文本编辑器中打开时，表头和值错位的问题。
                final = final.Replace("\n\t\t\tA", "\n                     \tA");
                //移除尾随空行
                if (final.EndsWith("\n"))
                {
                    final = final.Remove(final.Length - 1, 1);
                }
            }
            else
            {
                final = null;
            }

            return final;
        }

        private string DealResult2(string result)
        {
            Regex regex = new Regex("(((\\d+|no) work registers used)|(work registers not used))(.|\n)*?(?=\nA = Arithmetic)");
            MatchCollection matches = regex.Matches(result);
            
            ShaderFunctionResult data = new ShaderFunctionResult
            {
                WorkRegisters = "0",
                UniformRegisters = "0",
                A = "0",
                LS = "0",
                T = "0",
            };
            if (matches.Count > 0)
            {
                string result2 = matches[0].ToString();

                if (!result2.Contains("work registers not used"))
                {
                    regex = new Regex("(\\d+|no)\\s*work registers used");
                    matches = regex.Matches(result2);
                    if (matches.Count > 0)
                    {
                        data.WorkRegisters = matches[0].Groups[1].Value;
                    }
                }
                if (!result2.Contains("uniform registers not used"))
                {
                    regex = new Regex("(\\d+|no)\\s*uniform registers used");
                    matches = regex.Matches(result2);
                    if (matches.Count > 0)
                    {
                        data.UniformRegisters = matches[0].Groups[1].Value;
                    }
                }

                const string p_num = "(\\d*\\.\\d*|\\d*|N/A)";
                regex = new Regex("Instructions Emitted:\\s*" + p_num + "\\s*" + p_num + "\\s*" + p_num);
                matches = regex.Matches(result2);
                if (matches.Count > 0)
                {
                    Match m = matches[0];

                    data.A = m.Groups[1].Value;
                    data.LS = m.Groups[2].Value;
                    data.T = m.Groups[3].Value;
                }
            }
            else
            {
                return null;
            }

            return data.ToString();
        }

        private string DealError(string result)
        {
            Regex regex = new Regex("(?<=All rights reserved\\.)(.|\n)*?(?=Compilation failed)");
            MatchCollection matches = regex.Matches(result);

            if (matches.Count > 0)
            {
                string final = matches[0].ToString();
                //if (final.StartsWith("\n"))
                //{
                //    final = final.Remove(0, 1);
                //}
                //if (final.EndsWith("\n"))
                //{
                //    final = final.Remove(final.Length - 1, 1);
                //}

                return final;
            }
            else
            {
                return null;
            }
        }

        private static void OpenCompiledShader(Shader s, int mode, int customPlatformsMask, bool includeAllVariants)
        {
            ShaderUtilType.InvokeMember("OpenCompiledShader",
                BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.InvokeMethod, null, null,
                new object[] { s, mode, customPlatformsMask, includeAllVariants });
        }


        #region --------- UI ---------

        private const int OBJ_BUFFER_MAX_COUNT = 14;
        private UnityEngine.Object[] m_objBuffer = new UnityEngine.Object[OBJ_BUFFER_MAX_COUNT];

        private void UI_FilePathField(string label, ref string result, int bufferIndex, Type objTypeFilter = null, bool isFolder = false)
        {
            if (objTypeFilter == null)
            {
                objTypeFilter = typeof(UnityEngine.Object);
            }

            GUILayout.Label(label);

            if (bufferIndex >= OBJ_BUFFER_MAX_COUNT - 1)
            {
                Debug.LogError("bufferIndex(" + bufferIndex + ") out of range(" + OBJ_BUFFER_MAX_COUNT + ")");
                GUILayout.Label("bufferIndex(" + bufferIndex + ") out of range(" + OBJ_BUFFER_MAX_COUNT + ")");
                return;
            }

            EditorGUILayout.BeginHorizontal();
            {
                m_objBuffer[bufferIndex] = EditorGUILayout.ObjectField(m_objBuffer[bufferIndex], objTypeFilter, false, GUILayout.Width(180));
                result = EditorGUILayout.TextField(result);
            }
            EditorGUILayout.EndHorizontal();
            if (m_objBuffer[bufferIndex + 1] != m_objBuffer[bufferIndex])
            {
                result = AssetDatabase.GetAssetPath(m_objBuffer[bufferIndex]);
                //如果确定选择的对象是文件夹，则判断是否为斜杠结尾，不是则补充
                if (isFolder && !result.EndsWith("/"))
                {
                    result = result + "/";
                }

                m_objBuffer[bufferIndex + 1] = m_objBuffer[bufferIndex];
            }
        }

        private void UI_ReadOnlyPathField(string label, ref string value)
        {
            GUILayout.Label("[ReadOnly]" + label);
            EditorGUILayout.TextField(value);
        }

        private void UI_Button(string label, Action callback)
        {
            if (GUILayout.Button(label, GUILayout.MinHeight(30)))
            {
                LogGreen("MaliShaderProfiler." + label + " --> begin");
                callback();
                LogGreen("MaliShaderProfiler." + label + " --> done");
            }
        }

        #endregion



        #region --------- Util ---------

        private string ParseShaderNameToCommon(string shaderName)
        {
            return shaderName.Replace('/', '-');
        }

        private void LogGreen(string s)
        {
#if DEBUG_ON
            Debug.Log("@Green " + s);
#endif
        }

        private void LogFile(string s)
        {
            m_logFileSB.AppendLine(s);
        }

        private void WriteToFile(string path, StringBuilder sb)
        {
            FileStream fs = new FileStream(path, FileMode.Create);
            StreamWriter sw = new StreamWriter(fs);
            //开始写入
            sw.Write(sb.ToString());
            //清空缓冲区
            sw.Flush();
            //关闭流
            sw.Close();
            fs.Close();
            ////清空SB
            //sb.Remove(0, sb.Length);
        }

        #endregion

    }

    /// <summary>
    /// Shader检测器的帮助页面
    /// </summary>
    public class MSP_HelpWindow: EditorWindow
    {
        private string m_helpText;
        private Vector2 m_scrollPos;

        void OnEnable()
        {
            m_helpText = @"使用说明：
1.安装`Mali Offline Compiler`，开发用版本号为`v6.4.0`
https://developer.arm.com/tools-and-software/graphics-and-gaming/graphics-development-tools/mali-offline-compiler

2.将`malisc.exe`添加到系统path中，方便使用

3.在选择框中选择一个Shader，按下编译（Compile）按钮，Unity会进行Shader编译。
编译结束后会自动在当前配置的文本编辑器中打开

4.将打开的界面关闭或切换回面板，按下检测（Profile）按钮，开始执行检测

5.检测结果会显示在下方文本框中，参数注释在最结尾处

6.可以将结果输出到指定的Log文件中，使用任意一种文本编辑器即可打开查看

参考资料：
- https://community.arm.com/developer/tools-software/graphics/b/blog/posts/mali-gpu-tools-a-case-study-part-3-static-analysis-with-the-mali-offline-shader-compiler

- https://blog.csdn.net/smallhujiu/article/details/80964249

";
        }

        void OnGUI()
        {
            m_scrollPos = EditorGUILayout.BeginScrollView(m_scrollPos, GUILayout.Width(400), GUILayout.Height(400));
            EditorGUILayout.TextArea(m_helpText, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }
    }

}