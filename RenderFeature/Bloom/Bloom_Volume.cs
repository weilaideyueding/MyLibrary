using UnityEditor.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class Bloom_Volume : VolumeComponent,IPostProcessComponent
{
    public BoolParameter isOn = new BoolParameter(false);
    public ClampedIntParameter _Steps = new ClampedIntParameter(3, 2, 7);

    public ClampedFloatParameter _luminanceThreshole = new ClampedFloatParameter(0.1f, 0, 2);
    public ClampedFloatParameter _bloomDownOffset = new ClampedFloatParameter(1, 0.1f, 2);
    public ClampedFloatParameter _bloomUpOffset = new ClampedFloatParameter(1, 0.1f, 2);
    
    
    public ClampedFloatParameter _bloomIntensity = new ClampedFloatParameter(1, 0.1f, 2);
    // public ClampedFloatParameter _downSampleBlurSize = new ClampedFloatParameter(3, 3, 10);
    // public ClampedFloatParameter _downSampleBlurSigma = new ClampedFloatParameter(1, 0.1f, 10);
    //
    // public ClampedFloatParameter _upSampleBlurSize = new ClampedFloatParameter(3, 3, 10);
    // public ClampedFloatParameter _upSampleBlurSigma = new ClampedFloatParameter(1, 0.1f, 10);
    
    
    public bool IsTileCompatible()
    {
        return false;
    }

    public bool IsActive() => isOn.value;
}