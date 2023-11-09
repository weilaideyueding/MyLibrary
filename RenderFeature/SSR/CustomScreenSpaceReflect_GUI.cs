using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

[CustomEditor(typeof(CustomScreenSpaceReflect_Feature))]
public class CustomScreenSpaceReflect_GUI : Editor
{
    private CustomScreenSpaceReflect_Feature controller;
    private CustomScreenSpaceReflect_Feature.Settings settings;
    
    private void OnEnable()
    {
        this.controller = target as CustomScreenSpaceReflect_Feature;
        if (controller == null) return;

        settings = controller.settings;
    }

    public override void OnInspectorGUI()
    {
        DrawBase();
    }
    
    void DrawBase()
    {
        controller.isShaowBase = EditorGUILayout.Foldout(controller.isShaowBase, new GUIContent("Base"));
        if (controller.isShaowBase)
        {
            settings.ssrShader = (Shader) EditorGUILayout.ObjectField("SSR Shader", settings.ssrShader, typeof(Shader), true);
            
            settings.maskStysles = (CustomScreenSpaceReflect_Feature.MaskStysles)EditorGUILayout.EnumPopup(
                "Mask Style", settings.maskStysles);
            

            settings.ReflectionLayer = EditorGUILayout.MaskField(
                "SSR Reflect Layer", settings.ReflectionLayer, UnityEditorInternal.InternalEditorUtility.layers);


            // if (settings.maskStysles == CustomScreenSpaceReflect_Feature.MaskStysles.Stencil)
            // {
            //     settings.stencilRef = EditorGUILayout.IntField("Stencil Ref", settings.stencilRef);
            //     settings.stencilFunc = (CompareFunction)EditorGUILayout.EnumPopup("Stencil Func", settings.stencilFunc);
            // }

            settings.ssrStyles = (CustomScreenSpaceReflect_Feature.SSRStyles)EditorGUILayout.EnumPopup("SSR Style", settings.ssrStyles);
            
            settings.queueMin = EditorGUILayout.IntSlider(
                "Min Queue",settings.queueMin, 1000, 5000);
            settings.queueMax = EditorGUILayout.IntSlider(
                "Max Queue",settings.queueMax, 1000, 5000);
            settings.ssrIntensity = EditorGUILayout.Slider(
                "SSR Intensity",settings.ssrIntensity, 0.1f, 1f);
        }

        if (settings.ssrStyles == CustomScreenSpaceReflect_Feature.SSRStyles.RayTracing)
        {
            controller.isShaowRay = EditorGUILayout.Foldout(controller.isShaowRay, new GUIContent("Ray Tracing"));
            if (controller.isShaowRay)
            {
                settings.rayStepCount = EditorGUILayout.IntSlider(
                    "Step Count",settings.rayStepCount, 1, 100);
                settings.rayStepThickness = EditorGUILayout.Slider(
                    "Step Thickness",settings.rayStepThickness, 0.1f, 1f);
                settings.rayStepSize = EditorGUILayout.Slider(
                    "Step Size",settings.rayStepSize, 0.01f, 0.2f);
            }
        }

        if (settings.ssrStyles == CustomScreenSpaceReflect_Feature.SSRStyles.DDA)
        {
            controller.isShaowDDA = EditorGUILayout.Foldout(controller.isShaowDDA, new GUIContent("DDA"));
            if (controller.isShaowDDA)
            {
                settings.isHiz = EditorGUILayout.Toggle(
                    "Open Hiz",settings.isHiz);
                settings.ddaMaxDistance = EditorGUILayout.Slider(
                    "Max Distance",settings.ddaMaxDistance, 1.0f, 50.0f);
                settings.ddaStepSize = EditorGUILayout.Slider(
                    "Step Size",settings.ddaStepSize, 0.1f, 5.0f);
                settings.ddaStepCount = EditorGUILayout.IntSlider(
                    "Step Count",settings.ddaStepCount, 1, 200);
                settings.ddaThickness = EditorGUILayout.Slider(
                    "Thickness",settings.ddaThickness, 0.1f, 1f);

            }
        }
        
        controller.isShowBlur = EditorGUILayout.Foldout(controller.isShowBlur, new GUIContent("Blur"));
        if (controller.isShowBlur)
        {
            settings.blurCount = EditorGUILayout.IntSlider(
                "Blur Count",settings.blurCount, 0, 10);
            settings.pexelsOffset = EditorGUILayout.Slider(
                "Pexels Offset",settings.pexelsOffset, 0.1f, 1f);
        }
        
    }
    
    
}
