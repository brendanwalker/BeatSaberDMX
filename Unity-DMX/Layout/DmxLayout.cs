using System;
using System.Collections.Generic;
using System.IO;
using BeatSaberDMX;
using BeatSaberDMX.Configuration;
using Newtonsoft.Json;
using UnityEngine;

public class DmxTransform
{
    public float XPosMeters { get; set; }
    public float YPosMeters { get; set; }
    public float ZPosMeters { get; set; }
    public float YRotationAngle { get; set; }
}

public class DmxLayoutDefinition
{
    public string Name { get; set; }
    public DmxTransform Transform { get; set; }
}

public abstract class DmxLayoutInstance : MonoBehaviour
{
    public byte[] dmxData = new byte[0];
    public abstract int NumChannels { get; }

    public virtual void SetData(byte[] dmxData)
    {
        this.dmxData = dmxData;
    }

    public virtual void Patch(DmxLayoutDefinition layoutDefinition)
    {
        SetDMXTransform(layoutDefinition.Transform);
    }

    public void SetDMXTransform(DmxTransform transform)
    {
        gameObject.transform.localPosition =
            new Vector3(
                transform.XPosMeters,
                transform.YPosMeters,
                transform.ZPosMeters);
        gameObject.transform.localRotation =
            Quaternion.AngleAxis(
                transform.YRotationAngle,
                Vector3.up);
    }
}