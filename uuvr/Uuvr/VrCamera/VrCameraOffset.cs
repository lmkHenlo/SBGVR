using System;
using UnityEngine;

namespace Uuvr.VrCamera;

// TODO: add manual offsets.
public class VrCameraOffset: UuvrBehaviour
{
    // SBG VR: extra rotation applied under every VR camera, used to recenter the view (for example to
    // cancel the headset's current yaw). Identity means no change.
    public static Quaternion RecenterRotation = Quaternion.identity;

#if CPP
    public VrCameraOffset(System.IntPtr pointer) : base(pointer)
    {
    }
#endif

    protected override void OnBeforeRender()
    {
        base.OnBeforeRender();
        UpdateTransform();
    }

    protected override void OnSettingChanged()
    {
        base.OnSettingChanged();
        var config = ModConfiguration.Instance;
        
        transform.localPosition = new Vector3(
            config.CameraPositionOffsetX.Value,
            config.CameraPositionOffsetY.Value,
            config.CameraPositionOffsetZ.Value);
    }

    private void Update()
    {
        UpdateTransform();
    }
    
    private void LateUpdate()
    {
        UpdateTransform();
    }

    private void UpdateTransform()
    {
        if (ModConfiguration.Instance.AlignCameraToHorizon.Value)
        {
            var forward = Vector3.ProjectOnPlane(transform.parent.forward, Vector3.up);
            transform.LookAt(transform.position + forward, Vector3.up);
            transform.rotation *= RecenterRotation;
        }
        else
        {
            transform.localRotation = RecenterRotation;
        }
    }
}
