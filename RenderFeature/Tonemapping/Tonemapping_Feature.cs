using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class Tonemapping_Feature : ScriptableRendererFeature
{
    class CustomRenderPass : ScriptableRenderPass
    {
        private Shader shader;
        private Material material;
        
        //构造函数
        public CustomRenderPass()
        {
            shader = Shader.Find("Unlit/Tonemapping");   // 需自己填写
            if (shader == null)
            {
                Debug.LogWarningFormat("没有找到后处理Shader");
                return;
            }

            material = CoreUtils.CreateEngineMaterial(shader);
            if (material == null)
            {
                Debug.LogWarningFormat("Material创建失败");
                return;
            }
            
            
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (renderingData.postProcessingEnabled == false)
            {
                return;
            }

            var volume = VolumeManager.instance.stack.GetComponent<Tonemapping_Volume>();

            // 如果未开启volume，则不进行feature
            if (!volume.IsActive())
            {
                return;
            }
            
            // Debug.Log(vaolume._test);
            
            material.SetFloat("_lumeValue", volume._lumeValue.value);

            if (!volume.isTonemapping.value)
            {
                material.EnableKeyword("_TONEMAPPING_OFF");
                material.DisableKeyword("_TONEMAPPING_ACES");
            }
            else
            {
                material.EnableKeyword("_TONEMAPPING_ACES");
                material.DisableKeyword("_TONEMAPPING_OFF");
            }
            
            var sour = renderingData.cameraData.renderer.cameraColorTargetHandle;
            var descriptor = renderingData.cameraData.cameraTargetDescriptor;
            int dest = Shader.PropertyToID("_TonemappingTex"); // 需自己填写
            
            CommandBuffer cmd = CommandBufferPool.Get("Tonemapping"); // 需自己填写
            
            cmd.GetTemporaryRT(dest, descriptor);
            
            cmd.Blit(sour, dest);
            
            cmd.Blit(dest, sour, material);
            
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public override void OnCameraCleanup(CommandBuffer cmd)
        {
        }
    }

    CustomRenderPass m_ScriptablePass;
    
    public override void Create()
    {
        m_ScriptablePass = new CustomRenderPass();

        m_ScriptablePass.renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing -1;
    }
    
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        
        renderer.EnqueuePass(m_ScriptablePass);
    }
}


