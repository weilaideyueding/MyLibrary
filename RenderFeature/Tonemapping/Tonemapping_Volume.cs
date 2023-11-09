using System;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[Serializable, VolumeComponentMenu("My Postprocess/ Tonemapping")]
public class Tonemapping_Volume : VolumeComponent,IPostProcessComponent
{
    public BoolParameter isTonemapping = new BoolParameter(false);

    public ClampedFloatParameter _lumeValue = new ClampedFloatParameter(1, 0.1f, 2);
    
    public bool IsTileCompatible()
    {
        return false;
    }

    public bool IsActive() => isTonemapping.value;

}

