using System;
using System.Collections.Generic;
using System.IO;
using BeatSaberDMX;
using BeatSaberDMX.Configuration;
using Newtonsoft.Json;
using Unity.Collections;
using Unity.Jobs;
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

struct OverlapSegmentJob : IJobParallelFor
{
    [ReadOnly]
    public Vector3 segmentStart;
    [ReadOnly]
    public Vector3 segmentEnd;
    [ReadOnly]
    public Color32 segmentColor;
    [ReadOnly]
    public float radius;
    [ReadOnly]
    public NativeArray<Vector3> vertices;
    public NativeArray<Color32> runtimeColors;

    public void Execute(int vertexIndex)
    {
        Vector3 vertex = vertices[vertexIndex];

        if (DmxDeviceMath.IsPointWithinRadiusOfSegment(segmentStart, segmentEnd, radius, vertex))
        {
            Color32 color = runtimeColors[vertexIndex];

            color.r = Math.Max(color.r, segmentColor.r);
            color.g = Math.Max(color.g, segmentColor.g);
            color.b = Math.Max(color.r, segmentColor.b);

            runtimeColors[vertexIndex]= color;
        }
    }
}

struct OverlapBoxJob : IJobParallelFor
{
    [ReadOnly]
    public Vector3 boxCenter;
    [ReadOnly]
    public Vector3 boxXAxis;
    [ReadOnly]
    public Vector3 boxYAxis;
    [ReadOnly]
    public Vector3 boxZAxis;
    [ReadOnly]
    public Vector3 boxExtents;
    [ReadOnly]
    public Color32 boxColor;
    [ReadOnly]
    public NativeArray<Vector3> vertices;
    public NativeArray<Color32> runtimeColors;

    public void Execute(int vertexIndex)
    {
        Vector3 vertex = vertices[vertexIndex];

        if (DmxDeviceMath.IsPointWithinOrientedBox(boxCenter, boxXAxis, boxYAxis, boxZAxis, boxExtents, vertex))
        {
            Color32 color = runtimeColors[vertexIndex];

            color.r = Math.Max(color.r, boxColor.r);
            color.g = Math.Max(color.g, boxColor.g);
            color.b = Math.Max(color.r, boxColor.b);

            runtimeColors[vertexIndex] = color;
        }
    }
}

public abstract class DmxLayoutInstance : MonoBehaviour
{
    public byte[] dmxData = new byte[0];
    public abstract int NumChannels { get; }

    protected MeshFilter meshFilter;
    protected Color32[] runtimeColors;

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

    public void ProcessSegmentColliderOverlap(GameObject gameObject)
    {
        Vector3 worldSegmentStart;
        Vector3 worldSegmentEnd;
        Color32 segmentColor;

        if (BeatSaberDMXController.Instance.GetLedInteractionSegment(
                    gameObject,
                    out worldSegmentStart,
                    out worldSegmentEnd,
                    out segmentColor))
        {
            var jobData = new OverlapSegmentJob();
            jobData.segmentStart = this.gameObject.transform.InverseTransformPoint(worldSegmentStart);
            jobData.segmentEnd = this.gameObject.transform.InverseTransformPoint(worldSegmentEnd);
            jobData.segmentColor = segmentColor;
            jobData.radius = PluginConfig.Instance.SaberPaintRadius;
            jobData.vertices = new NativeArray<Vector3>(meshFilter.mesh.vertices, Allocator.TempJob);
            jobData.runtimeColors = new NativeArray<Color32>(runtimeColors, Allocator.TempJob);

            var batchSize = 16;
            var handle = jobData.Schedule(runtimeColors.Length, batchSize);

            handle.Complete();

            jobData.runtimeColors.CopyTo(runtimeColors);

            jobData.vertices.Dispose();
            jobData.runtimeColors.Dispose();
        }
    }
}