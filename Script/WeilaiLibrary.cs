using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace WeilaiLibrary
{
    public class MyShaderGUI : ShaderGUI
    {
        // 一个简便的控制Keyworld的方法
        void IsKeyworldEnable(Material target, string keyworld, bool statu)
        {
            if (statu)
            {
                target.EnableKeyword(keyworld);
            }
            else
            {
                target.DisableKeyword(keyworld);
            }
        }

        // 一个能够保存值的折叠标签组
        public static bool FoldoutHeaderGroup(Material target, bool isShow, string ShaerProperty, string name)
        {
            // 获取Shader中保存的值
            isShow = target.GetInt(ShaerProperty) == 1 ? true : false;
        
            EditorGUI.BeginChangeCheck();
            isShow = EditorGUILayout.BeginFoldoutHeaderGroup(isShow, name);
        
            if (EditorGUI.EndChangeCheck())
            {
                // 将这个值保存至Shader中
                target.SetInt(ShaerProperty, isShow ? 1 : 0);
            }
            
            EditorGUILayout.EndFoldoutHeaderGroup();

            return isShow;
        }
        
        // Content版本
        public static bool FoldoutHeaderGroup(Material target, bool isShow, string ShaerProperty, GUIContent content)
        {
            // 获取Shader中保存的值
            isShow = target.GetInt(ShaerProperty) == 1 ? true : false;
        
            EditorGUI.BeginChangeCheck();
            isShow = EditorGUILayout.BeginFoldoutHeaderGroup(isShow, content);
        
            if (EditorGUI.EndChangeCheck())
            {
                // 将这个值保存至Shader中
                target.SetInt(ShaerProperty, isShow ? 1 : 0);
            }
            
            EditorGUILayout.EndFoldoutHeaderGroup();

            return isShow;
        }


        // 偷懒用，将属性名和 label存入对应的数组中，直接批量使用，说实话，也不怎么偷懒
        public static void BatchShaderProperties(MaterialEditor editor,MaterialProperty[] properties, string[] property, string[] label)
        {
            for (int i = 0; i < property.Length; i++)
            {
                editor.ShaderProperty(FindProperty(property[i], properties), label[i]);
            }
        }
    }


    public class MyScript
    {
        
    }
}
