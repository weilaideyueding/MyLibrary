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
        public static Matrix4x4 RayReBuildWorldPos_Quad(Camera camera)
        {
            Matrix4x4 frustumCorners = Matrix4x4.identity;
            
            Transform cameraTransform = camera.transform;
            
            float fov    = camera.fieldOfView;
            float far   = camera.farClipPlane;
            float aspect = camera.aspect;
            
            float halfHeight = far * Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);    // 将度数转换为弧度，然后求出tan值，再乘以near，得到半高，三角函数 对边/邻边
            Vector3 toRight = cameraTransform.right * halfHeight * aspect;      // 乘以aspect，得到半宽，再乘以相机右方向，得到右方向的半宽
            Vector3 toTop = cameraTransform.up * halfHeight;      
            
            
            Vector3 topLeft = cameraTransform.forward * far + toTop - toRight; // 获取到相机到左上角的向量
            Vector3 topRight = cameraTransform.forward * far + toTop + toRight;
            Vector3 bottomLeft = cameraTransform.forward * far - toTop - toRight;
            Vector3 bottomRight = cameraTransform.forward * far - toTop + toRight;

            // 行矩阵
            frustumCorners.SetRow(0, bottomLeft);
            frustumCorners.SetRow(1, bottomRight);
            frustumCorners.SetRow(2, topRight);
            frustumCorners.SetRow(3, topLeft);
            
            return frustumCorners;
        }
        
        // 还未完成，也好像无需完成了
        public static Matrix4x4 RayReBuildWorldPos_Triangle(Camera camera)
        {
            Matrix4x4 frustumCorners = Matrix4x4.identity;
            
            Transform cameraTransform = camera.transform;
            
            float fov    = camera.fieldOfView;
            float far   = camera.farClipPlane;
            float aspect = camera.aspect;
            
            float halfHeight = far * Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);    // 将度数转换为弧度，然后求出tan值，再乘以near，得到半高，三角函数 对边/邻边
            Vector3 toRight = cameraTransform.right * halfHeight * aspect;      // 乘以aspect，得到半宽，再乘以相机右方向，得到右方向的半宽
            Vector3 toTop = cameraTransform.up * halfHeight;


            Vector3 bottomLeft = cameraTransform.forward;
            Vector3 bottomRight = cameraTransform.forward;
            Vector3 topRight = cameraTransform.forward;
            
            // 行矩阵
            frustumCorners.SetRow(0, bottomLeft);
            frustumCorners.SetRow(1, bottomRight);
            frustumCorners.SetRow(2, topRight);

            return frustumCorners;
        }
    }
}
