using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;


namespace WeiLai_Editor
{
    public class WeiLai_Editor_Outline : EditorWindow
    {
        public static void Window()
        {
            var window = GetWindow<WeiLai_Editor_Outline>();

        }
    }

    public class Multi_Floder_Selection : EditorWindow
    {
        private static int valueLength;

        public static void Window(int length)
        {
            var window = GetWindow<Multi_Floder_Selection>();
            window.minSize = new Vector2(500, 200);
            valueLength = length;
        }

        public delegate void DataReceivedDelegate(String[] date);
        public event DataReceivedDelegate OnDataReceived;


        private static String[] filePath = new string[0];

        private string stringValue;
        private string folderPath;
        private string sonFolderName;
        
        private string[] searchfromSonFolder;
        private int searchfromSonFolderIndex;
        private string[] searchFileName;
        private int searchFileNameIndex;

        private float perWidth;

        private bool isSonFolder;
        private bool isRelativePath;

        private String[] folderStyles = { "No", "_0 排序", "_1 排序" };
        private int folderStylesIndex = 0;
        private int fileStylestIndex = 0;

        private String[] fileFormat = { "No", ".mat", ".png", ".tga" };
        private int fileFormatindex = 0;

        private GUIStyle yellowLabelStyle;
        private GUIStyle yellowPopupStyle;
        

        private void Awake()
        {
            yellowLabelStyle = new GUIStyle(EditorStyles.label);
            yellowLabelStyle.normal.textColor = Color.yellow;

            yellowPopupStyle = new GUIStyle(EditorStyles.popup);
            yellowPopupStyle.stretchWidth = true;
            yellowPopupStyle.alignment = TextAnchor.MiddleRight;
            yellowPopupStyle.normal.textColor = Color.yellow;
            // yellowPopupStyle.normal.background = MakeTexture(1, 1, Color.yellow); // 设置背景颜色
            
        }

        private void OnGUI()
        {
            EditorGUIUtility.labelWidth = 70;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("当前所需要文件数量：");
            EditorGUILayout.LabelField($"{valueLength}", yellowLabelStyle);
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(10);

            using (new EditorGUILayout.VerticalScope("box"))
            {
                EditorGUILayout.BeginHorizontal();

                // 获取文件夹路径
                folderPath = EditorGUILayout.TextField("文件夹：", folderPath);
                if (GUILayout.Button("Load", GUILayout.Width(50)))
                {
                    folderPath = EditorUtility.OpenFolderPanel("Load As", "", "");
                }

                EditorGUILayout.EndHorizontal();

                if (!String.IsNullOrEmpty(folderPath))
                {
                     GUILayout.Space(10);

                    isSonFolder = EditorGUILayout.ToggleLeft("是否有子文件夹", isSonFolder);

                    if (isSonFolder)
                    {
                        GUILayout.Space(5);
                        EditorGUILayout.BeginHorizontal();
                        sonFolderName = EditorGUILayout.TextField("子文件夹名：", sonFolderName);
                        folderStylesIndex = EditorGUILayout.Popup(folderStylesIndex, folderStyles, yellowPopupStyle, GUILayout.Width(70));

                        // 绘制子目录的目录
                        EditorGUI.BeginChangeCheck();
                        searchfromSonFolder = searchFolder(folderPath, searchfromSonFolder);
                        searchfromSonFolderIndex = EditorGUILayout.Popup(searchfromSonFolderIndex, searchfromSonFolder, GUILayout.Width(80));
                        if (EditorGUI.EndChangeCheck())
                        {
                            String[] split  = searchfromSonFolder[searchfromSonFolderIndex].Split("_");
                            sonFolderName = string.Join("_", split, 0, split.Length - 1);
                        }
                        
                        EditorGUILayout.EndHorizontal();
                    }

                    // 文件名相关
                    GUILayout.Space(5);
                    EditorGUILayout.BeginHorizontal();
                    stringValue = EditorGUILayout.TextField("文件名：", stringValue);
                    fileStylestIndex = EditorGUILayout.Popup(fileStylestIndex, folderStyles, yellowPopupStyle, GUILayout.Width(70));
                    fileFormatindex = EditorGUILayout.Popup(fileFormatindex, fileFormat, yellowPopupStyle, GUILayout.Width(50));
                    
                    // 寻找文件夹下的文件
                    if (isSonFolder)
                    {
                        searchFileName = Directory.GetFiles(folderPath + "/" + searchfromSonFolder[searchfromSonFolderIndex]);
                        for (int i = 0; i < searchFileName.Length; i++)
                        {
                            searchFileName[i] = Path.GetFileNameWithoutExtension(searchFileName[i]);
                        }
                    }
                    else
                    {
                        searchFileName = Directory.GetFiles(folderPath);
                        for (int i = 0; i < searchFileName.Length; i++)
                        {
                            searchFileName[i] = Path.GetFileNameWithoutExtension(searchFileName[i]);
                        }
                    }

                    if (searchFileName != null)
                    {
                        EditorGUI.BeginChangeCheck();
                        searchFileNameIndex = EditorGUILayout.Popup(searchFileNameIndex, searchFileName, GUILayout.Width(80));
                        if (EditorGUI.EndChangeCheck())
                        {
                            String[] split = searchFileName[searchFileNameIndex].Split("_");
                            stringValue = string.Join("_", split, 0, split.Length - 1);
                        }
                    }

                    
                    
                    EditorGUILayout.EndHorizontal();

                    // 绘制参考路径
                    GUILayout.Space(10);
                    isRelativePath = EditorGUILayout.ToggleLeft("是否是相对路径", isRelativePath);
                
                    if (filePath.Length > 0)
                    {
                        EditorGUILayout.Space(5);
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField("参考", GUILayout.Width(30));
                        EditorGUILayout.LabelField(filePath[0]);
                        EditorGUILayout.EndHorizontal();
                    }
                    

                    EditorGUIUtility.labelWidth = perWidth;
                }



            }
            
            
            filePath = new string[valueLength];
            
            // 进行路径整合
            for (int i = 0; i < valueLength; i++)
            {
                // 进行一个增量，如果选择了_0和_1，就会进行增加
                int sonFolderIndex = folderStylesIndex == 1 ? i : i + 1; 
                int fileIndex = fileStylestIndex == 1 ? i : i + 1; 

                string sonFolder;
                if (folderStylesIndex == 0)
                {
                    sonFolder = sonFolderName;
                }
                else
                {
                    sonFolder =  sonFolderName + $"_{sonFolderIndex}";
                }
                
                string file;
                if (fileStylestIndex == 0)
                {
                    file = stringValue;
                }
                else
                {
                    file =  stringValue + $"_{fileIndex}";
                }
                

                
                // 整理文件位置
                string path = "";

                
                
                string fileFormatName = fileFormat[fileFormatindex];
                
                if (fileFormatindex == 0)
                {
                    fileFormatName = "";
                }


                if (isRelativePath)
                {
                    path = "Assets" + folderPath.Substring(Application.dataPath.Length) + "/" + file +
                           fileFormatName;
                    if (isSonFolder)
                    {
                        path = "Assets" + folderPath.Substring(Application.dataPath.Length) + "/" + sonFolder + "/" +
                               file + fileFormatName;
                    }

                    filePath[i] = path;
                }
                else
                {
                    path = folderPath + "/" + file + fileFormatName;
                    if (isSonFolder)
                    {
                        path = folderPath + "/" + sonFolder + "/" + file + fileFormatName;
                    }

                    filePath[i] = path;
                }

            }
            
            
            GUILayout.Space(5);
            if (GUILayout.Button("确定位置"))
            {
                OnDataReceived?.Invoke(filePath);
                Close();
            }
            
        }

        private String[] searchFolder(string path, String[] folderName)
        {

            String[] folders = Directory.GetDirectories(path);
            folderName = new string[folders.Length];

            for (int i = 0; i < folders.Length; i++)
            {
                folderName[i] = Path.GetFileNameWithoutExtension(folders[i]);
            }
            
            return folderName;
        }
        
        
        private Texture2D MakeTexture(int width, int height, Color color)
        {
            Color[] pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = color;
            }
            Texture2D texture = new Texture2D(width, height);
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }


    }
    
    
}

