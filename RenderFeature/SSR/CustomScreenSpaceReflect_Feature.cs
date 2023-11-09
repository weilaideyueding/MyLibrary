using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System;
using UnityEngine.Experimental.Rendering;
using ProfilingScope = UnityEngine.Rendering.ProfilingScope;


public class CustomScreenSpaceReflect_Feature : ScriptableRendererFeature
{
    
    public enum SSRStyles
    {
        RayTracing,
        DDA
    }
    
    public enum MaskStysles
    {
        Stencil,
        Layer
    }
    
    // GUI
    public bool isShaowBase = false;
    public bool isShaowRay = false;
    public bool isShaowDDA = false;
    public bool isShowBlur = false;

    [Serializable]
    public class Settings
    {
        public Shader ssrShader;
        
        public MaskStysles maskStysles = MaskStysles.Stencil;
        public LayerMask ReflectionLayer;
        public SSRStyles ssrStyles;

        [Range(1000, 5000)] public int queueMin = 2000;
        [Range(1000, 5000)] public int queueMax = 3000;
        
        [Header("Stencil"), Space(5)]   // 暂时不行，传值的话，影响不到Stencil块内的值
        public int stencilRef = 1;
        public CompareFunction stencilFunc = CompareFunction.NotEqual;

        [Header("Output"), Space(5)]
        [Range(0, 1)] public float ssrIntensity = 1f; 

        [Header("Ray"), Space(5)]
        public int rayStepCount;
        public float rayStepThickness;
        public float rayStepSize;
        
        [Header("DDA"), Space(5)]
        public float ddaMaxDistance;
        public float ddaStepSize;
        public int ddaStepCount;
        public float ddaThickness;

        [Header("Hiz"), Space(5)]
        public bool isHiz = true;
        [Range(1, 6)]
        public int MipCount = 5;
        
        [Header("Blur"), Space(5)]
        [Range(0,10)]
        public int blurCount = 1;
        public float pexelsOffset = 1.0f;

    }
    
    // SSR
    class CustomScreenSpaceReflectPass : ScriptableRenderPass
    {
        private Material ssrMaterial;
        ProfilingSampler m_ProfilingSampler = new ProfilingSampler("SSR");

        private RTHandle sour;
        private RTHandle temp0;
        private RTHandle temp1;

        public Settings settings;
        
        FilteringSettings filter;
        private LayerMask ssrLayerMask;
        ShaderTagId shaderTag = new ShaderTagId("UniversalForward");
        
        public void Setup()
        {
            // 声明需要深度图和法线图
            // ConfigureInput(ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal);
        }
        
        public CustomScreenSpaceReflectPass(Settings settings)
        {
            // 创建 Material
            if (settings.ssrShader == null)
            {
                Debug.LogWarning("没有SSR Shader");
                return;
            }
            ssrMaterial = CoreUtils.CreateEngineMaterial(settings.ssrShader);
            this.settings = settings;
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            var descriptor = renderingData.cameraData.cameraTargetDescriptor;
            descriptor.depthBufferBits = 0; // 使用颜色时，不需要深度
            
            RenderingUtils.ReAllocateIfNeeded(ref temp0, descriptor, name: "_TempTex_0");
            RenderingUtils.ReAllocateIfNeeded(ref temp1, descriptor, name: "_TempTex_1");

            var depthDescriptor = renderingData.cameraData.cameraTargetDescriptor;
            depthDescriptor.colorFormat = RenderTextureFormat.RFloat;
        }
        

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {


            if (settings == null)
            {
                Debug.LogError("Settings not create");
                return;
            }
            // 设置参数
            ssrMaterial.SetFloat("_SSR_Intensity", settings.ssrIntensity);
            
            ssrMaterial.SetInt("_Ray_StepCount", settings.rayStepCount);
            ssrMaterial.SetFloat("_Ray_Thickness", settings.rayStepThickness);
            ssrMaterial.SetFloat("_Ray_StepSize", settings.rayStepSize);
            
            ssrMaterial.SetFloat("_DDA_MaxDistance", settings.ddaMaxDistance);
            ssrMaterial.SetFloat("_DDA_StepSize", settings.ddaStepSize);
            ssrMaterial.SetInt("_DDA_StepCount", settings.ddaStepCount);
            ssrMaterial.SetFloat("_DDA_Thickness", settings.ddaThickness);
            
            ssrMaterial.SetInt("_StencilRef", settings.stencilRef);
            ssrMaterial.SetInt("", (int)settings.stencilFunc);
            
            IsKeyworldEnable(ssrMaterial, "_SSR_RAY", settings.ssrStyles == SSRStyles.RayTracing);
            IsKeyworldEnable(ssrMaterial, "_SSR_DDA", settings.ssrStyles == SSRStyles.DDA);
            IsKeyworldEnable(ssrMaterial, "_HIZ_BUFFER", settings.isHiz);
            
            // 进行后处理
            CommandBuffer cmd = CommandBufferPool.Get("CustomScreenSpaceReflect");

            sour = renderingData.cameraData.renderer.cameraColorTargetHandle;

            using (new ProfilingScope(cmd, m_ProfilingSampler))
            {

                if (ssrMaterial == null)
                {
                    Debug.LogError("Material not create");
                    return;
                }
                
                // 传递RT
                if (sour.rt != null && temp0.rt != null)
                {
                    // 进行SSR
                    Blitter.BlitCameraTexture(cmd, sour, temp0, ssrMaterial,0);
                    // cmd.Blit(temp0, sour);

                // 模糊
                if (settings.blurCount != 0)
                {
                    for (int i = 1; i <= settings.blurCount; i++)   // 从1开始，<=
                    {
                        ssrMaterial.SetFloat("_PexelOffset", i * 0.5f + settings.pexelsOffset);
                        Blitter.BlitCameraTexture(cmd, temp0, temp1, ssrMaterial, 1);
                        cmd.Blit(temp1, temp0);
                    }
                }

                // 混合输出
                Blitter.BlitCameraTexture(cmd, temp0, sour, ssrMaterial, 4);
                }
            
            }

            context.ExecuteCommandBuffer(cmd);
            
            
            
            cmd.Clear();
            
            CommandBufferPool.Release(cmd);
        }
        

        public void Disposed()
        {
            temp0?.Release();
            temp1?.Release();
        }
        
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
    }

    // 传递颜色缓冲用的
    class CameraColorTargetPass : ScriptableRenderPass
    {
        private RTHandle sour;
        private RTHandle color;
        
        // private int 
        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            var descriptor = renderingData.cameraData.cameraTargetDescriptor;
            descriptor.width = 1920;
            descriptor.height = 1080;
            descriptor.depthBufferBits = 0;
            
            
            // RenderingUtils.ReAllocateIfNeeded(ref color, descriptor, name: "SSR_ColorRT");
            if (color == null)
            {
                color = RTHandles.Alloc(descriptor, name: "SSR_ColorRT");
            }
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            var cmd = CommandBufferPool.Get("SSR_Color");

            sour = renderingData.cameraData.renderer.cameraColorTargetHandle;

            if (sour.rt != null)
            {
                // Blitter.BlitCameraTexture(cmd, sour, color);
                cmd.Blit(sour, color);
                cmd.SetGlobalTexture("_SSR_ColorTexture", color);   // ^
            }
            
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
            CommandBufferPool.Release(cmd);
        }

        public void Disposed()
        {
            color?.Release();
        }
    }
    
    // 绘制Mask
    class MaskPass : ScriptableRenderPass
    {
        private Settings settings;
        private ShaderTagId shaderTag= new ShaderTagId("UniversalForward");
        private Material ssrMaterial;

        private RTHandle ssrMask_Layer;
        private RTHandle ssrMask;
        private RTHandle transDepth;
        public MaskPass(Settings settings)
        {
            this.settings = settings;
            ssrMaterial = CoreUtils.CreateEngineMaterial(settings.ssrShader);
            if (ssrMaterial == null)
            {
                Debug.Log("获取不到Material");
                return;
            }
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            transDepth = renderingData.cameraData.renderer.cameraDepthTargetHandle;
            
            var descriptor = renderingData.cameraData.cameraTargetDescriptor;
            descriptor.depthStencilFormat = GraphicsFormat.D32_SFloat_S8_UInt;
            descriptor.depthBufferBits = 24;
            
            // 要变成和深度缓存一样的格式
            if (transDepth.rt != null)
            {
                descriptor = transDepth.rt.descriptor;
            }
            
            RenderingUtils.ReAllocateIfNeeded(ref ssrMask, descriptor, name: "SSR_Mask");
            

            var des = renderingData.cameraData.cameraTargetDescriptor;
            des.depthBufferBits = 0;
            RenderingUtils.ReAllocateIfNeeded(ref ssrMask_Layer, des, name: "SSR_Mask_Layer");
            
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            var cmd = CommandBufferPool.Get("SSR_Mask");
            
            // 如果使用 Stencil的方法
            if (settings.maskStysles == MaskStysles.Stencil)
            {

                cmd.SetRenderTarget(ssrMask);

                if (transDepth.rt != null)
                {
                    // 无敌
                    cmd.CopyTexture(transDepth, ssrMask);
                    
                    cmd.Blit(ssrMask, ssrMask, ssrMaterial, 8); 
                }

                
                cmd.SetGlobalTexture("_SSR_MaskTex", ssrMask);

                context.ExecuteCommandBuffer(cmd);
                
            }
            
            // 如果使用 Layer的方法
            if (settings.maskStysles == MaskStysles.Layer)
            {
                    
                cmd.SetRenderTarget(ssrMask_Layer);
                cmd.SetGlobalTexture("_SSR_MaskTex", ssrMask_Layer);
                cmd.ClearRenderTarget(true, true, Color.black); // 需要清理缓存
                
                context.ExecuteCommandBuffer(cmd);


                RenderQueueRange queue = new RenderQueueRange(Mathf.Min(settings.queueMax, settings.queueMin),
                    Mathf.Max(settings.queueMax, settings.queueMin));
            
                var filter = new FilteringSettings(queue, settings.ReflectionLayer);

                // 绘制通过部分 Mask
                var draw = CreateDrawingSettings(
                    shaderTag, ref renderingData, renderingData.cameraData.defaultOpaqueSortFlags);
                draw.overrideMaterial = ssrMaterial;
                draw.overrideMaterialPassIndex = 2;
                context.DrawRenderers(renderingData.cullResults, ref draw, ref filter);
                
                // 绘制黑色部分 Mask
            }
            
            cmd.Clear();
            CommandBufferPool.Release(cmd);
        }

        public void Disposed()
        {
            ssrMask?.Release();
            ssrMask_Layer?.Release();
        }
    }
    
    // 绘制Hiz缓存的
    class HizBufferPass : ScriptableRenderPass
    {
        private Settings settings;
        private Material ssrMaterial;
        
        RTHandle mCameraDepthTexture;
        
        // Material
        int mHiZBufferFromMiplevelID, mHiZBufferToMiplevelID, mMaxHiZBufferTextureipLevelID, mHiZBufferTextureID, mSourceSizeID;

        // Hiz
        private RenderTextureDescriptor mHiZBufferDescriptor;
        private RTHandle mHiZBufferTexture;
        private string mHiZBufferTextureName = "_SSR_HiZBuffer";

        // 其他的mip
        private RenderTextureDescriptor[] mHiZBufferDescriptors;
        // private RTHandle[] mHiZBufferTextures;
        private RenderTexture[] mHiZBufferTextures;
        

        public HizBufferPass(Settings settings)
        {
            this.settings = settings;
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            
            if (settings.ssrShader == null)
            {
                Debug.LogWarning("没有SSR Shader");
                return;
            }
            ssrMaterial = CoreUtils.CreateEngineMaterial(settings.ssrShader);
            
            // 进行 HIZ缓存
            var renderer = renderingData.cameraData.renderer;

            // 分配RTHandle
            var desc = renderingData.cameraData.cameraTargetDescriptor;
            // 将高和宽向上取2的整次幂，如何除2，获取 mip0的大小
            var width = Math.Max((int)Math.Ceiling(Mathf.Log(desc.width, 2) - 1.0f), 1);    // 获取向上的2的指数？
            var height = Math.Max((int)Math.Ceiling(Mathf.Log(desc.height, 2) - 1.0f), 1);
            
            width = 1 << width; // 1 << width 等价于 2^width
            height = 1 << height; // Math.Pow()这个应该也行

            mHiZBufferDescriptor = new RenderTextureDescriptor(width, height, RenderTextureFormat.RFloat, 0, settings.MipCount);    // 注意mipmap
            mHiZBufferDescriptor.msaaSamples = 1;
            mHiZBufferDescriptor.useMipMap = true;
            mHiZBufferDescriptor.sRGB = false;// linear
            
            RenderingUtils.ReAllocateIfNeeded(ref mHiZBufferTexture, mHiZBufferDescriptor, FilterMode.Bilinear, TextureWrapMode.Clamp, name: mHiZBufferTextureName);

            
            // 获取其他的mip
            mHiZBufferDescriptors = new RenderTextureDescriptor[settings.MipCount];
            
            // mHiZBufferTextures = new RTHandle[settings.MipCount];
            
            // for (int i = 0; i < mHiZBufferTextures.Length; i++) {
            //     mHiZBufferDescriptors[i] = new RenderTextureDescriptor(width, height, RenderTextureFormat.RFloat, 0, 1);
            //     mHiZBufferDescriptors[i].msaaSamples = 1;
            //     mHiZBufferDescriptors[i].useMipMap = false;
            //     mHiZBufferDescriptors[i].sRGB = false;
            //     RenderingUtils.ReAllocateIfNeeded(ref mHiZBufferTextures[i], mHiZBufferDescriptors[i], FilterMode.Bilinear, TextureWrapMode.Clamp, name: "YAOZHALE" + i);
            //
            //     // 缩小一半
            //     width = Math.Max(width / 2, 1);
            //     height = Math.Max(height / 2, 1);
            //
            //     if (width == 1 && height == 1)
            //     {
            //         Debug.Log("已经到达0了，不用再缩小了");
            //     }
            // }

            mHiZBufferTextures = new RenderTexture[settings.MipCount];
            
            for (int i = 0; i < mHiZBufferTextures.Length; i++)
            {            
                mHiZBufferDescriptors[i] = new RenderTextureDescriptor(width, height, RenderTextureFormat.RFloat, 0, 1);
                mHiZBufferDescriptors[i].msaaSamples = 1;
                mHiZBufferDescriptors[i].useMipMap = false;
                mHiZBufferDescriptors[i].sRGB = false;// linear
            
                mHiZBufferTextures[i] = RenderTexture.GetTemporary(mHiZBufferDescriptors[i]);
                
                // 缩小一半
                width = Math.Max(width / 2, 1);
                height = Math.Max(height / 2, 1);
                
                if (width == 1 && height == 1)
                {
                    Debug.Log("已经到达1了，不用再缩小了");
                }
            }
            
            // Material
            // mHiZBufferFromMiplevelID = Shader.PropertyToID("_HiZBufferFromMiplevel");
            // mHiZBufferToMiplevelID = Shader.PropertyToID("_HiZBufferToMiplevel");
            mMaxHiZBufferTextureipLevelID = Shader.PropertyToID("_MaxHizBufferMipLevel");
            mHiZBufferTextureID = Shader.PropertyToID("_HiZBufferTexture");
            mSourceSizeID = Shader.PropertyToID("_HiZBufferSourceSize");

            ConfigureTarget(renderer.cameraColorTargetHandle);  // 设置颜色缓存
            ConfigureClear(ClearFlag.None, Color.white);    // 不剔除，并将背景设为白色
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            var cmd = CommandBufferPool.Get("SSR_Hiz");

            // 获取
            mCameraDepthTexture = renderingData.cameraData.renderer.cameraDepthTargetHandle;
            cmd.SetGlobalTexture("SSR_TransparentDepthTex", mCameraDepthTexture);

            using (new ProfilingScope(cmd, new ProfilingSampler("SSR_Hiz")))
            {
                // 让Hiz那张RT开启Mipmap，然后将每一层Mipmap放进去就好了
                // mip 0d
                if (mCameraDepthTexture.rt != null)
                {
                    cmd.Blit(mCameraDepthTexture, mHiZBufferTextures[0]);
                    if (mHiZBufferTextures[0] != null)
                    {
                        cmd.CopyTexture(mHiZBufferTextures[0], 0, 0, 
                            mHiZBufferTexture, 0, 0);
                    }
                }
                
                // // 1 - max
                // for (int i = 1; i < settings.MipCount; i++)
                // {
                //     cmd.SetGlobalFloat(mHiZBufferFromMiplevelID, i - 1);
                //     cmd.SetGlobalFloat(mHiZBufferToMiplevelID, i);
                //     cmd.SetGlobalVector(mSourceSizeID, new Vector4(mHiZBufferDescriptors[i - 1].width, mHiZBufferDescriptors[i - 1].height, 1.0f / mHiZBufferDescriptors[i - 1].width, 1.0f / mHiZBufferDescriptors[i - 1].height));
                //
                //     // 传递，分辨率会自动适应
                //     if (mHiZBufferTextures[i-1].rt != null)
                //     {
                //         Blitter.BlitCameraTexture(cmd, mHiZBufferTextures[i-1], mHiZBufferTextures[i], ssrMaterial, 5);
                //     }
                //
                //     if (mHiZBufferTextures[i].rt != null)
                //     {
                //         // 将Mip放在对应的mip中就好了
                //         cmd.CopyTexture(mHiZBufferTextures[i], 0,0,
                //             mHiZBufferTexture, 0, i);
                //     }
                // }

                // rendertexture的方法
                
                for (int i = 1; i < settings.MipCount; i++)
                {
                    cmd.SetGlobalVector(mSourceSizeID, new Vector4(mHiZBufferDescriptors[i - 1].width, mHiZBufferDescriptors[i - 1].height, 1.0f / mHiZBufferDescriptors[i - 1].width, 1.0f / mHiZBufferDescriptors[i - 1].height));
                
                    // 传递，分辨率会自动适应
                    if (mHiZBufferTextures[i-1] != null)
                    {
                        cmd.Blit(mHiZBufferTextures[i-1], mHiZBufferTextures[i], ssrMaterial, 6);
                    }
                
                    if (mHiZBufferTextures[i] != null)
                    {
                        // 将Mip放在对应的mip中就好了
                        cmd.CopyTexture(mHiZBufferTextures[i], 0,0,
                            mHiZBufferTexture, 0, i);
                    }
                }

                // 这两个传递给SSR计算的Shader
                cmd.SetGlobalFloat(mMaxHiZBufferTextureipLevelID, settings.MipCount - 1);
                // 传递给Shader
                cmd.SetGlobalTexture(mHiZBufferTextureID, mHiZBufferTexture);
            }
            
            context.ExecuteCommandBuffer(cmd);
            
            for (int i = 0; i < mHiZBufferTextures.Length; i++)
            {
                RenderTexture.ReleaseTemporary(mHiZBufferTextures[i]);
            }
            
            cmd.Clear();
            CommandBufferPool.Release(cmd);
        }
        

        // 释放销毁
        public void Disposed()
        {
            mHiZBufferTexture?.Release();
        }
    }

    // 进行透明物体的法线的渲染
    class TransParentNormalPass : ScriptableRenderPass
    {
        private Settings settings;
        private RTHandle transNormal;
        private Material ssrMaterial;
        private ShaderTagId shaderTag = new ShaderTagId("UniversalForward");
        public TransParentNormalPass(Settings settings)
        {
            this.settings = settings;
            ssrMaterial = CoreUtils.CreateEngineMaterial(settings.ssrShader);
            if (ssrMaterial == null)
            {
                Debug.Log("获取不到Material");
                return;
            }
        }
        
        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            var descriptor = renderingData.cameraData.cameraTargetDescriptor;
            descriptor.depthBufferBits = 0;

            if (transNormal == null)
            {
                transNormal = RTHandles.Alloc(descriptor, name:"SSR_TransparentNormalTex");
            }
        }
        
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            var cmd = CommandBufferPool.Get("SSR_TransparentNormal");
            
            cmd.SetRenderTarget(transNormal);
            cmd.SetGlobalTexture("_SSR_TransparentNormalTex", transNormal);
            cmd.ClearRenderTarget(RTClearFlags.ColorDepth, Color.clear, 0, 0);
            context.ExecuteCommandBuffer(cmd);
            
            
            var drawSetting = CreateDrawingSettings(
                shaderTag, ref renderingData, renderingData.cameraData.defaultOpaqueSortFlags);
            drawSetting.overrideMaterial = ssrMaterial;
            drawSetting.overrideMaterialPassIndex = 7;
            RenderQueueRange queue = new RenderQueueRange(Mathf.Min(settings.queueMax, settings.queueMin),
                Mathf.Max(settings.queueMax, settings.queueMin));
            
            var filter = new FilteringSettings(queue, settings.ReflectionLayer);
            // if (settings.maskStysles == MaskStysles.Stencil)
            // {
            //     filter = new FilteringSettings(queue, int.MaxValue);
            // }
            
            context.DrawRenderers(renderingData.cullResults, ref drawSetting, ref filter);
            
            
            
            cmd.Clear();
            CommandBufferPool.Release(cmd);
        }
        
        public void Disposed()
        {
            transNormal?.Release();
        }


    }
    
    // 法线，Hiz，颜色缓存，SSR，Mask
    TransParentNormalPass transParentNormalPass;
    MaskPass maskPass;
    HizBufferPass hizBufferPass;
    CameraColorTargetPass cameraColorTargetPass;
    CustomScreenSpaceReflectPass customScreenSpaceReflectPass;
    
    public Settings settings = new Settings();

    public override void Create()
    {
        cameraColorTargetPass = new CameraColorTargetPass();
        transParentNormalPass = new TransParentNormalPass(settings);
        maskPass = new MaskPass(settings);
        hizBufferPass = new HizBufferPass(settings);
        customScreenSpaceReflectPass = new CustomScreenSpaceReflectPass(settings);
        
        
        // 获取颜色缓存，需要不透明物体
        cameraColorTargetPass.renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
        
        // 需要获取透明物体的法线
        transParentNormalPass.renderPassEvent = RenderPassEvent.AfterRenderingTransparents + 1;
        
        // 绘制Hiz缓存
        hizBufferPass.renderPassEvent = RenderPassEvent.AfterRenderingTransparents + 2;
        
        // 绘制Mask
        maskPass.renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing - 1;
        
        // 进行 SSR
        customScreenSpaceReflectPass.renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
    }
    
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        customScreenSpaceReflectPass.Setup();
        
        renderer.EnqueuePass(cameraColorTargetPass);       
        renderer.EnqueuePass(hizBufferPass);
        renderer.EnqueuePass(maskPass);
        renderer.EnqueuePass(transParentNormalPass);
        renderer.EnqueuePass(customScreenSpaceReflectPass);
    }
    
    

    // 进行销毁
    protected override void Dispose(bool disposing)
    {
        transParentNormalPass.Disposed();
        hizBufferPass.Disposed();
        cameraColorTargetPass.Disposed();
        customScreenSpaceReflectPass.Disposed();
        maskPass.Disposed();
    }
}


