#ifndef WEILAI_LIBRARY
#define WEILAI_LIBRARY

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

// 常用Pass

// <ShadowCaster Pass>

float3 _LightDirection;

struct ShadowVertexInput
{
    float4 vertex : POSITION;
    float3 normal : NORMAL;
    float2 uv     : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID  // 适配 GPU Instance
};

struct ShadowVertexOutput
{
    float2 uv    : TEXCOORD0;
    float4 pos   : SV_POSITION;
};

// 获取阴影所需的裁剪空间
float4 GetShadowPosotipn(ShadowVertexInput input)
{
    float3 worldPos = TransformObjectToWorld(input.vertex);
    float3 nDirWS = TransformObjectToWorldNormal(input.normal);

    float3 lDirWS = _LightDirection;

    float4 positionCS = TransformWorldToHClip(ApplyShadowBias(worldPos, nDirWS, lDirWS));

    //  
    #if UNITY_REVERSED_Z
        positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
    #else
        positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
    #endif


    return positionCS;
}

ShadowVertexOutput ShadowPassVertex(ShadowVertexInput input)
{
    ShadowVertexOutput output;
    UNITY_SETUP_INSTANCE_ID(input);

    output.uv = input.uv;
    output.pos = GetShadowPosotipn(input);

    return output;
}

half4 ShadowPassFragment(ShadowVertexOutput input):SV_Target
{
    return 0;
}

// 重构世界坐标
float3 RebuildWorldPosition(float2 screenPos, half depth)
{
    // 重构NDC空间坐标，unity采用OpenGL，NDC范围为（-1， 1）
    float4 positionNDC = float4(screenPos * 2.0 - 1.0, depth , 1);

    // 需要反转y轴
    #if UNITY_UV_STARTS_AT_TOP
    // Our world space, view space, screen space and NDC space are Y-up.
    // Our clip space is flipped upside-down due to poor legacy Unity design.
    // The flip is baked into the projection matrix, so we only have to flip
    // manually when going from CS to NDC and back.
    positionNDC.y = -positionNDC.y;
    #endif
    
    // 使用逆矩阵获得
    float4 positionWS = mul(UNITY_MATRIX_I_VP, positionNDC);
    // 再次齐次除法？
    positionWS /= positionWS.w;
    // 输出
    return positionWS.xyz;
}


// NormalStrength
// 需要在解码后的切线空间中操作，因为lerp的是Z值
float3 NormalStrenght(float3 normal, float strength)
{
    normal = float3(normal.r * strength, normal.g * strength, lerp(1, normal.b, saturate(strength)));
    return  normal;
}

// UV方向运动
// -1 - 1转换为方向 0 - 360°
float2 UVDir (float dir)
{
    dir = dir * 2 - 1;
    return  normalize(float2(cos(dir * PI), sin(dir * PI)));
}

// 光照处理
float LightingSpecular(float3 L, float3 N, float3 V, float smoothness)
{
    float3 H = SafeNormalize(float3(L) + float3(V));
    float NdotH = saturate(dot(N, H));
    return pow(NdotH, smoothness);
}


// 焦散纹理采样
half3 SampleCaustics(Texture2D _CausticMap, SamplerState sampler_CausticMap, float2 uv, half split)
{
    // 向三个方向分离uv
    float2 uv0 = uv + half2(split, split);
    float2 uv1 = uv + half2(split, -split);
    float2 uv2 = uv + half2(-split, -split);

    // 向三个方向采样，并分别构成一个通道
    half r = SAMPLE_TEXTURE2D(_CausticMap, sampler_CausticMap, uv0).r;
    half g = SAMPLE_TEXTURE2D(_CausticMap, sampler_CausticMap, uv1).r;
    half b = SAMPLE_TEXTURE2D(_CausticMap, sampler_CausticMap, uv2).r;

    // 相交的地方就是白色，不相交的地方呈现该通道颜色
    return half3(r,g,b);
    
}

// 法线混合

float3 NormalBlend_WhiteOut(float3 n1, float3 n2)
{
    float3 r  = normalize(float3(n1.xy + n2.xy, n1.z*n2.z));
    return r * .5 + 0.5;
}

// unity内的方法
float3 NormalBlend_Unity(float3 n1, float3 n2)
{
    float3x3 nBasis = float3x3(
     float3(n1.z, n1.y, -n1.x), // +90 degree rotation around y axis
     float3(n1.x, n1.z, -n1.y), // -90 degree rotation around x axis
     float3(n1.x, n1.y,  n1.z));

    float3 r = normalize(n2.x*nBasis[0] + n2.y*nBasis[1] + n2.z*nBasis[2]);
    return r * 0.5 + 0.5;
}

// 较好的视觉效果
float3 NormalBlend_RNM(float3 n1, float3 n2)
{
    float3 t = n1 * float3( 2,  2, 2) + float3(-1, -1,  0);
    float3 u = n2 * float3(-2, -2, 2) + float3( 1,  1, -1);
    float3 r = t*dot(t, u)/t.z - u;

    return r * 0.5 + 0.5;
}

//PBR

// 计算法线分布函数
float D_GGX_TR(float3 n, float3 h, float a)
{
    float a2 = a * a;
    float nh = saturate(dot(n, h));
    float nh2 = nh * nh;
    float nom = a2;
    float denom = PI * pow(nh2 * (a2 - 1.0) + 1.0, 2);
                
    return nom / denom;
}

// 计算几何项
float GeometrySchlickGGX(float nv, float k)
{
    float nom = nv;
    float denom = nv * (1.0 - k) + k;

    return nom / denom;
}

// 计算 G分量
float GeometrySmith(float3 n, float3 v, float3 l, float k)
{
    float nv = saturate(dot(n, v));
    float nl = saturate(dot(n, l));

    // 分别计算视向量与光方向
    float GGX_V = GeometrySchlickGGX(nv, k);
    float GGX_L = GeometrySchlickGGX(nl, k);
                
    return  GGX_V * GGX_L;
}

// CookTorrance
float3 Specular_BRDF(float3 normal, float3 viewDir, float3 lightDir, float roughness, float F)
{
    float3 h = normalize(viewDir + lightDir);
    
    float D = D_GGX_TR(normal, h, roughness);

    // 几何项
    float k = pow(roughness + 1.0, 2) / 8.0;
    float G = GeometrySmith(normal, h, roughness, k);

    // 反射系数
    float3 ks = F;

    float nl = saturate(dot(normal, lightDir));
    float nv = saturate(dot(normal, viewDir));

    float3 SpecCol = (D * F * G * 0.25) / (nl * nv + 0.001);    // 乘0.25相当于除4的优化

    return SpecCol;
}




// 噪声

float rand(float3 co)
{
    return frac(sin(dot(co.xyz, float3(12.9898, 78.233, 53.539))) * 43758.5453);
}

float PerlinNoise(float2 st, int seed)
{
    st.y += _Time[1];
	
    float2 p = floor(st);
    float2 f = frac(st);
	
    float w00 = dot(rand(float3(p, seed)), f);
    float w10 = dot(rand(float3(p + float2(1, 0), seed)), f - float2(1, 0));
    float w01 = dot(rand(float3(p + float2(0, 1), seed)), f - float2(0, 1));
    float w11 = dot(rand(float3(p + float2(1, 1), seed)), f - float2(1, 1));
		
    float2 u = f * f * (3 - 2 * f);
	
    return lerp(lerp(w00, w10,u.x), lerp(w01, w11, u.x), u.y);
}



// 模糊

// 粒状模糊 暂未完成
half2 GrainyBlur(half _BlueIteration)
{
    float random = sin(dot(float2(0.5, 0.5), half2(1233.224, 1743.335)));
    half2 offset;
                
    for (float k = 0; k < _BlueIteration; k++)  // 进行循环模糊
        {
        random = frac(43758.5453 * random + 0.61432);   // 随机值
        offset.x = (random - 0.5) * 2.0;                // 映射至（-1， 1）
        random = frac(43758.5453 * random + 0.61432);
        offset.y = (random - 0.5) * 2.0;
        }
    return offset;
}


// 高斯模糊
float GaussWeight2D(float x, float y, float sigma)
{
    float E = 2.71828182846;
    float sigma_2 = pow(sigma, 2);

    float a = -(x*x + y*y) / (2.0 * sigma_2);
    return pow(E, a) / (2.0 * PI * sigma_2);
}

// stride为像素大小
float3 GaussNxN(Texture2D tex, SamplerState sampler_x, float2 uv, int n, float2 stride, float sigma)
{
    float3 color = float3(0,0,0);
    int r = n / 2;
    float weight = 0.0;
                
    for(int i=-r; i<=r; i++)
    {
        for(int j=-r; j<=r; j++)
        {
            // 获取权重值
            float w = GaussWeight2D(i, j, sigma);
            float2 coord = uv + float2(i, j) * stride;
            color += SAMPLE_TEXTURE2D(tex, sampler_x, coord).rgb * w;
            weight += w;
        }
    }

    color /= weight;
    return color;
}


// Kawase 模糊
half4 KawaseBlur(Texture2D tex, SamplerState sampler_name, float2 uv, float2 texelSize, half pexelOffset)
{
    half4 o = 0;
    o += SAMPLE_TEXTURE2D(tex, sampler_name, uv + float2(pexelOffset +0.5, pexelOffset +0.5) * texelSize); 
    o += SAMPLE_TEXTURE2D(tex, sampler_name, uv + float2(-pexelOffset -0.5, pexelOffset +0.5) * texelSize); 
    o += SAMPLE_TEXTURE2D(tex, sampler_name, uv + float2(-pexelOffset -0.5, -pexelOffset -0.5) * texelSize); 
    o += SAMPLE_TEXTURE2D(tex, sampler_name, uv + float2(pexelOffset +0.5, -pexelOffset -0.5) * texelSize); 
    return o * 0.25;
}






//边缘检测

// Sobel算子边缘检测
half SobelEdge(Texture2D tex, SamplerState sampler_tex, float2 uv[9])
{
    // 卷积核
    half SobelGx[9] = {-1, -2, -1,
                        0,  0,  0,
                        1,  2,  1};

    half SobelGy[9] = {-1, 0, 1,
                       -2, 0, 2,
                       -1, 0, 1};

    // 一个数组储存
    half depth[2] = {0,0};

    for (int index = 0; index <= 8; index++)
    {
        // 采样该点下的深度值
        half d = SAMPLE_TEXTURE2D_X(tex, sampler_tex, uv[index]).r;
        d = Linear01Depth(d, _ZBufferParams);
                    
        depth[0] += d * SobelGx[index];
        depth[1] += d * SobelGy[index];
                    
    }

    // 需要绝对值的相加
    half depthDiff = abs(depth[0]) + abs(depth[1]);
    return depthDiff;
}


// RGB转换为亮度
float rgbToLume(float3 color)
{
    return dot(float3(0.2126, 0.7152, 0.0722), color);
}



// 色调映射
float3 ACESToneMapping(float3 color, float adapted_lum)
{
    const float A = 2.51f;
    const float B = 0.03f;
    const float C = 2.43f;
    const float D = 0.59f;
    const float E = 0.14f;

    color *= adapted_lum;
    return (color * (A * color + B)) / (color * (C * color + D) + E);
}



// 水体渲染相关

// 正弦波
float3 SinWave(float3 worldPos, half2 Direction, half Amplitude, half Wavelength, half WaveSpeed)
{
    // 波长
    half k = 2 * PI / Wavelength;
    half2 d = normalize(Direction);
    // 函数主体
    half f = k * (dot(d, worldPos.xz) - WaveSpeed * _Time.y);
    // 振幅和正弦实现
    worldPos.y = Amplitude * sin(f);

    // 法线还没实现
    
    return worldPos;
}

// Gerstner 结构体
struct GerstnerWaveStruct
{
    // 坐标的X,Z轴需要用原来的加上这个，需要锚定在原来的坐标上
    float3 worldPos;
    float3 tangent;
    float3 binormal;
    float3 normal;
};

// Gerstner 波
// 在外面，将normal的值设为(0,1,0)
GerstnerWaveStruct GerstnerWave (float3 worldPos, half2 Direction, half Wavelength, half Steepness)
{
    float3 P,B,T,N;
    // 波长
    float k = 2 * PI / Wavelength;
    // 相位常量 除法和除法好像都行，乘法会让波慢一些
    float c = sqrt(9.8 * k);
    // 方向
    half2 d = normalize(Direction);
    // 函数主体
    float f = k * dot(d, worldPos.xz) - c * _Time.y;
    // 陡度
    float s = Steepness;
    // 振幅
    half a = s / k;
            	
    // Gerstner 波实现
    P.x = a * d.x * cos(f);
    P.y = a * sin(f);
    P.z = a * d.y * cos(f);

    T.x = d.x * d.x * k * a * -sin(f);
    T.y = d.x * k * a * cos(f);
    T.z = d.x * d.y * k * a * -sin(f);

    B.x = d.x * d.y * k * a * -sin(f);
    B.y = d.y * k * a * cos(f);
    B.z = d.y * d.y * k * a * -sin(f);

    N.x = d.x * a * k * -cos(f);
    N.y = k * a * -sin(f);
    N.z = d.y * k * a * -cos(f);
    
    GerstnerWaveStruct g;
    g.worldPos = P;
    g.tangent = float3(1 + T.x, T.y, T.z);
    g.binormal = float3(B.x, B.y, 1 + B.z);
    g.normal = float3(N.x, 1 + N.y, N.z);
    
    return g;
}

// 加入了速度控制
GerstnerWaveStruct GerstnerWave (float3 worldPos, half2 Direction, half Wavelength, half Steepness, half Speed)
{
    float3 P,B,T,N;
    // 波长
    float k = 2 * PI / Wavelength;
    // 相位常量 除法和除法好像都行，乘法会让波慢一些
    float c = sqrt(9.8 * k);
    // 方向
    half2 d = normalize(Direction);
    // 函数主体
    float f = k * dot(d, worldPos.xz) - c * _Time.y * Speed;
    // 陡度
    float s = Steepness;
    // 振幅
    half a = s / k;
            	
    // Gerstner 波实现
    P.x = a * d.x * cos(f);
    P.y = a * sin(f);
    P.z = a * d.y * cos(f);

    T.x = d.x * d.x * k * a * -sin(f);
    T.y = d.x * k * a * cos(f);
    T.z = d.x * d.y * k * a * -sin(f);

    B.x = d.x * d.y * k * a * -sin(f);
    B.y = d.y * k * a * cos(f);
    B.z = d.y * d.y * k * a * -sin(f);

    N.x = d.x * a * k * -cos(f);
    N.y = k * a * -sin(f);
    N.z = d.y * k * a * -cos(f);
    
    GerstnerWaveStruct g;
    g.worldPos = P;
    g.tangent = float3(1 + T.x, T.y, T.z);
    g.binormal = float3(B.x, B.y, 1 + B.z);
    g.normal = float3(N.x, 1 + N.y, N.z);
    
    return g;
}

// 多波混合 未完成参数的插值
GerstnerWaveStruct GerstnerWaveGroup (float3 worldPos, uint amount, half2 Direction, half Wavelength, half Steepness)
{
    float3 P,B,T,N;

    for (uint i = 0; i < amount; i++)
    {
        // 波长
        float k = 2 * PI / Wavelength;
        // 相位常量 除法和除法好像都行，乘法会让波慢一些
        float c = sqrt(9.8 * k);
        // 方向
        half2 d = normalize(Direction);
        // 函数主体
        float f = k * dot(d, worldPos.xz) - c * _Time.y;
        // 陡度
        float s = Steepness;
        // 振幅
        half a = s / k;
            	
        // Gerstner 波实现
        P.x += a * d.x * cos(f);
        P.y += a * sin(f);
        P.z += a * d.y * cos(f);

        // 使用了两种获得法线的方法，BT叉乘或者直接获得N都可以
        T.x += d.x * d.x * k * a * -sin(f);
        T.y += d.x * k * a * cos(f);
        T.z += d.x * d.y * k * a * -sin(f);

        B.x += d.x * d.y * k * a * -sin(f);
        B.y += d.y * k * a * cos(f);
        B.z += d.y * d.y * k * a * -sin(f);

        N.x += d.x * a * k * -cos(f);
        N.y += k * a * -sin(f);
        N.z += d.y * k * a * -cos(f);
    }
    
    GerstnerWaveStruct g;

    // 进行最后的混合
    g.worldPos = float3(worldPos.x + P.x, P.y, worldPos.z + P.z);
    g.tangent = float3(1 + T.x, T.y, T.z);
    g.binormal = float3(B.x, B.y, 1 + B.z);
    g.normal = float3(N.x, 1 + N.y, N.z);
    
    return g;
}

// 函数公式

float RemapRange(float value, float minSrc, float maxSrc, float minDst, float maxDst)
{
    // 将源范围的值映射到0-1的范围
    float normalizedValue = (value - minSrc) / (maxSrc - minSrc);
    
    // 将0-1的范围映射到目标范围
    float mappedValue = minDst + normalizedValue * (maxDst - minDst);
    
    return mappedValue;
}

// 风格化处理

// 色调分离
float Posterize(half col, int step)
{
    return floor(col*step) / (step-1);
}

float3 Posterize(half3 col, int step)
{
    return floor(col*step) / (step-1);
}

float4 Posterize(half4 col, int step)
{
    return floor(col*step) / (step-1);
}


// 科幻线条
float4 MultiLine(float dir)
{
    return 1 - saturate(round(abs(frac(dir * 100) * 2)));    
}

float4 MultiLine(float dir, float amount)
{
    return 1 - saturate(round(abs(frac(dir * amount) * 2)));    
}






#endif
