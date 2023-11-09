using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class Bloom_Feature : ScriptableRendererFeature
{
    class CustomRenderPass : ScriptableRenderPass
    {
        private Shader shader;
        private Material material;
        
        
        //构造函数
        public CustomRenderPass()
        {
            shader = Shader.Find("Unlit/Bloom");   // 需自己填写
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
            
            var volume = VolumeManager.instance.stack.GetComponent<Bloom_Volume>();

            // 如果Volume为开启则退出
            if (!volume.IsActive())
            {
                return;
            }
            
            // 获取
            var sour = renderingData.cameraData.renderer.cameraColorTargetHandle;
            
            CommandBuffer cmd = CommandBufferPool.Get("Bloom"); // 需自己填写


            // 进行像素筛选
            RenderTexture RT_Threshold = RenderTexture.GetTemporary(Screen.width, Screen.height, 0,
                RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            RT_Threshold.filterMode = FilterMode.Bilinear;
            
            material.SetFloat("_luminanceThreshole", volume._luminanceThreshole.value); 
            material.SetFloat("_bloomIntensity", volume._bloomIntensity.value);  // 传递阈值
            
            // material.SetFloat("_downSampleBlurSize", volume._downSampleBlurSize.value);
            // material.SetFloat("_downSampleBlurSigma", volume._downSampleBlurSigma.value);
            // material.SetFloat("_upSampleBlurSize", volume._upSampleBlurSize.value);
            // material.SetFloat("_upSampleBlurSigma", volume._upSampleBlurSigma.value);

            
            // 用来存储最开始的Color
            RenderTexture dest = RenderTexture.GetTemporary(
                Screen.width, Screen.height, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            
            // 用于存储颜色信息
            cmd.Blit(sour, dest);
            
            // 通过shader进行亮度阈值判断
            cmd.Blit(sour, RT_Threshold, material, 0);
            
            // 设置步数
            int step = volume._Steps.value;
            int downSize = 2;   // 降采样的大小
            RenderTexture[] RT_BloomDown = new RenderTexture[step]; // 创建纹理组
            
            // 创建低分辨率RT
            for (int i = 0; i < step; i++)
            {
                // 降低分辨率
                int width = Screen.width / downSize;
                int height = Screen.height / downSize;

                RT_BloomDown[i] = RenderTexture.GetTemporary(
                    width, height, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
                RT_BloomDown[i].filterMode = FilterMode.Bilinear;
                
                downSize *= 2;  // 每次降低两倍分辨率
            }
            
            // 进行降采样,第一个需要单独出来
            cmd.Blit(RT_Threshold, RT_BloomDown[0]);
            for (int i = 1; i < RT_BloomDown.Length; i++)
            {
                // 传递kawase Blur
                material.SetFloat("_bloomDownOffset", i / 2 + volume._bloomDownOffset.value);  

                cmd.Blit(RT_BloomDown[i -1], RT_BloomDown[i], material, 1);
            }
            
            
            // 创建上采样RT
            RenderTexture[] RT_BloomUp = new RenderTexture[step];
            for(int i = 0; i < step - 1; i++)
            {
                int w = RT_BloomDown[step-2-i].width;   // 与 Down对应 
                int h = RT_BloomDown[step-2-i].height;
                RT_BloomUp[i] = RenderTexture.GetTemporary(
                    w, h, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
                RT_BloomUp[i].filterMode = FilterMode.Bilinear;   // 启用双线性滤波
            }
            
            // 进行上采样
            material.SetFloat("_bloomUpOffset", volume._bloomUpOffset.value);

            // 将前一个RT传递给Shader，这个RT的分辨率为当前的两倍，由Shader变为一半
            cmd.SetGlobalTexture("_PreTex", RT_BloomDown[step - 1]);
            
            // 将同分辨率的 Down传递过来，这种方法并不是升采样，而是平采样
            // 总体是，将对应分辨率的down blit给 up，然后加上前一张的RT颜色
            cmd.Blit(RT_BloomDown[step - 2], RT_BloomUp[0], material, 2);
            
            for (int i = 1; i < step - 1; i++)
            {
                RenderTexture pre_tex = RT_BloomUp[i - 1];   // 前一个的RT
                RenderTexture curr_tex = RT_BloomDown[step - 2 - i]; // 对应分辨率的down
                
                material.SetFloat("_bloomUpOffset", i / 2 + volume._bloomUpOffset.value);

                cmd.SetGlobalTexture("_PreTex", pre_tex);
                cmd.Blit(curr_tex, RT_BloomUp[i], material, 2);
            }
            
            // 合并输出，使用shader采样最后的效果，和开始的颜色相加混合
            cmd.SetGlobalTexture("_BloomTex", RT_BloomUp[step -2]);
            
            cmd.Blit(dest, sour, material, 3);
            
            // cmd.Blit(RT_BloomUp[step -2], sour);
            
            // 进行上传与释放
            RenderTexture.ReleaseTemporary(RT_Threshold);
            RenderTexture.ReleaseTemporary(dest);
            for (int i = 0; i < RT_BloomDown.Length; i++)
            {
                RenderTexture.ReleaseTemporary(RT_BloomDown[i]);
                RenderTexture.ReleaseTemporary(RT_BloomUp[i]);
            }
            
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

        // 比雾早一些
        m_ScriptablePass.renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing - 3;
    }
    
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {

        renderer.EnqueuePass(m_ScriptablePass);
    }
}


