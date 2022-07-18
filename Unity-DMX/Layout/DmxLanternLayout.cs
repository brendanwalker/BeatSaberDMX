using System;
using System.Collections;
using System.Collections.Generic;
using BeatSaberDMX;
using BeatSaberDMX.Configuration;
using UnityEngine;

public class DmxLanternLayoutDefinition : DmxLayoutDefinition
{
    public float PhysicalRadiusMeters { get; set; }
    public float PhysicalHightMeters { get; set; }

    public int HorizontalPanelPixelCount { get; set; }
    public int VerticalPanelPixelCount { get; set; }
    public int PanelCount { get; set; }

    public DmxDeviceDefinition Device { get; set; }
}


[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(CapsuleCollider))]
[RequireComponent(typeof(Rigidbody))]
public class DmxLanternLayoutInstance : DmxLayoutInstance
{
    public float PhysicalArcLengthMeters { get; private set; }
    public float PhysicalRadiusMeters { get; private set; }
    public float PhysicalHightMeters { get; private set; }

    public int HorizontalPixelCount { get; private set; }
    public int VerticalPixelCount { get; private set; }
    public int TotalPixelCount { get; private set; }

    public override int NumChannels { get { return TotalPixelCount * 3; } }

    private MeshRenderer meshRenderer;
    private CapsuleCollider capsuleCollider;
    
    public DmxDeviceInstance Device { get; private set; }

    private Mesh runtimeMeshData;
    private byte[] dmxColorData;

    private int[] vertexToLEDIndexTable;

    public static DmxLanternLayoutInstance SpawnInstance(DmxLanternLayoutDefinition definition, Transform gameOrigin)
    {
        GameObject ownerGameObject = new GameObject(
            definition.Name,
            new System.Type[] { 
                typeof(DmxLanternLayoutInstance), 
                typeof(DmxDeviceInstance) }
            );

        var col = ownerGameObject.GetComponent<CapsuleCollider>();
        col.isTrigger = true;

        var rb = ownerGameObject.GetComponent<Rigidbody>();
        rb.isKinematic = true;

        var mr = ownerGameObject.GetComponent<MeshRenderer>();
        string shaderName = "Legacy Shaders/Particles/Alpha Blended";
        //string shaderName = "Hidden/GIDebug/VertexColors";
        Shader shader = Shader.Find(shaderName);
        if (shader != null)
        {
            mr.sharedMaterial = new Material(shader);

        }
        else
        {
            Plugin.Log?.Error($"Failed to find '{shaderName}' shader");
        }

        DmxLanternLayoutInstance instance = ownerGameObject.GetComponent<DmxLanternLayoutInstance>();

        instance.SetupPixelGeometry(definition);

        instance.gameObject.transform.parent = gameOrigin;
        instance.SetDMXTransform(definition.Transform);

        instance.Device = ownerGameObject.GetComponent<DmxDeviceInstance>();
        instance.Device.useBroadcast = false;
        instance.Device.remoteIP = definition.Device.DeviceIP;
        instance.Device.startUniverseId = definition.Device.StartUniverse;
        instance.Device.fps = 30;
        instance.Device.AppendDMXLayout(instance, 0, instance.NumChannels);

        return instance;
    }

    void Awake()
    {
        capsuleCollider = GetComponent<CapsuleCollider>();
        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();
        Device = GetComponent<DmxDeviceInstance>();
    }

    void OnDestroy()
    {
        Plugin.Log?.Error($"DMXPixelGrid getting destroyed");
        //Plugin.Log?.Error(UnityEngine.StackTraceUtility.ExtractStackTrace());
    }

    private void OnTriggerEnter(Collider other)
    {
        ProcessSegmentColliderOverlap(other.gameObject);
    }

    private void OnTriggerStay(Collider other)
    {
        ProcessSegmentColliderOverlap(other.gameObject);
    }

    private void Update()
    {
        //Plugin.Log?.Error($"Pixel Grid update");
        float decayParam = Mathf.Clamp01(PluginConfig.Instance.SaberPaintDecayRate * Time.deltaTime);

        // Update colors for all vertices
        for (int vertexIndex = 0; vertexIndex < runtimeColors.Length; ++vertexIndex)
        {
            // Fade the colors toward black
            runtimeColors[vertexIndex] = Color32.Lerp(runtimeColors[vertexIndex], Color.black, decayParam);

            // Push the updated color data to the DMX buffer
            {
                int ledIndex = vertexToLEDIndexTable[vertexIndex];
                int channelStartIndex = ledIndex * 3;

                dmxColorData[channelStartIndex] = runtimeColors[vertexIndex].r;
                dmxColorData[channelStartIndex + 1] = runtimeColors[vertexIndex].g;
                dmxColorData[channelStartIndex + 2] = runtimeColors[vertexIndex].b;
            }
        }

        // Push data to dmx device
        SetData(dmxColorData);

        // Update visible mesh
        runtimeMeshData.colors32 = runtimeColors;
        runtimeMeshData.UploadMeshData(false);
    }

    public override void Patch(DmxLayoutDefinition layoutDefinition)
    {
        base.Patch(layoutDefinition);

        DmxLanternLayoutDefinition lanternDefinition = (DmxLanternLayoutDefinition)layoutDefinition;
        Device.Patch(lanternDefinition.Device);

        SetDMXTransform(lanternDefinition.Transform);
    }

    private bool SetupPixelGeometry(DmxLanternLayoutDefinition definition)
    {
        int panelHorizPixelCount = definition.HorizontalPanelPixelCount;
        int panelVertPixelCount = definition.VerticalPanelPixelCount;
        float physArcLength = (float)Math.PI * definition.PhysicalRadiusMeters; // half circle circumference

        PhysicalArcLengthMeters = physArcLength;
        PhysicalRadiusMeters = definition.PhysicalRadiusMeters;
        PhysicalHightMeters = definition.PhysicalHightMeters;
        HorizontalPixelCount = panelHorizPixelCount;
        VerticalPixelCount = panelVertPixelCount * definition.PanelCount;
        TotalPixelCount = HorizontalPixelCount * VerticalPixelCount; 

        // Build a table to map from vertex indices to LED indices
        // This is used for writing out DMX data
        vertexToLEDIndexTable = new int[TotalPixelCount];
        {
            int ledIndex = 0;

            for (int panelIndex= 0; panelIndex < definition.PanelCount; ++panelIndex)
            {
                int panelOffset = panelHorizPixelCount * panelVertPixelCount * panelIndex;

                for (int colIndex = panelHorizPixelCount - 1; colIndex >= 0; --colIndex)
                {
                    for (int rowOffset = panelVertPixelCount - 1; rowOffset >= 0 ; --rowOffset)
                    {
                        // Reverse LED direction on odd columns
                        int rowIndex =
                            (colIndex % 2 == 1)
                            ? (panelVertPixelCount - rowOffset - 1)
                            : rowOffset;

                        vertexToLEDIndexTable[rowIndex * panelHorizPixelCount + colIndex + panelOffset] = ledIndex;
                        ++ledIndex;
                    }
                }
            }
        }

        // Static mesh data
        Vector3[] vertices = new Vector3[TotalPixelCount];
        Vector3[] normals = new Vector3[TotalPixelCount];
        Vector2[] uv = new Vector2[TotalPixelCount];
        // Dynamic mesh data
        runtimeColors = new Color32[TotalPixelCount];

        if (PhysicalRadiusMeters > 0.0f)
        {
            // ArcLength = Radius * Angular Span
            float angularSpanRadians = physArcLength / PhysicalRadiusMeters;

            // Cylinder around Y-axis, starting LED on +X axis
            int vertIndex = 0;
            for (int j = 0; j < panelVertPixelCount; ++j)
            {
                float v = (float)j / (float)(panelVertPixelCount - 1);
                float y = (v - 0.5f) * PhysicalHightMeters;

                for (int i = 0; i < panelHorizPixelCount; ++i)
                {
                    float u = (float)i / (float)(panelHorizPixelCount - 1);
                    float theta = Mathf.Lerp(-0.5f * angularSpanRadians, 0.5f * angularSpanRadians, u);
                    float nx = Mathf.Cos(theta);
                    float nz = Mathf.Sin(theta);
                    float x = PhysicalRadiusMeters * nx;
                    float z = PhysicalRadiusMeters * nz;

                    vertices[vertIndex] = new Vector3(x, y, z);
                    normals[vertIndex] = new Vector3(nx, 0.0f, nz);
                    uv[vertIndex] = new Vector2(u, v);
                    runtimeColors[vertIndex] = new Color32(0, 0, 0, 255);

                    ++vertIndex;
                }
            }

            // Create a pill that encapsulated the cylinder
            capsuleCollider.radius = PhysicalRadiusMeters;
            capsuleCollider.height = PhysicalHightMeters + 2.0f * PhysicalRadiusMeters;
            capsuleCollider.direction = 1; // y-axis
        }

        // Create a triangle index array from the grid of vertices
        int horizQuadCount = (panelHorizPixelCount - 1);
        int vertQuadCount = (panelVertPixelCount - 1);
        int[] tris = new int[horizQuadCount * vertQuadCount * 6]; // 2 tris per quad * 3 indices per tri

        int writeIndex = 0;
        int rowStartVertIndex = 0;
        for (int vertQuadIndex = 0; vertQuadIndex < vertQuadCount; ++vertQuadIndex)
        {
            for (int horizQuadIndex = 0; horizQuadIndex < horizQuadCount; ++horizQuadIndex)
            {
                int upperLeftVertIndex = rowStartVertIndex + horizQuadIndex;
                int upperRightVertIndex = upperLeftVertIndex + 1;
                int lowerLeftVertIndex = upperLeftVertIndex + panelHorizPixelCount;
                int lowerRightVertIndex = lowerLeftVertIndex + 1;

                // upper left triangle
                tris[writeIndex + 0] = lowerLeftVertIndex;
                tris[writeIndex + 1] = upperRightVertIndex;
                tris[writeIndex + 2] = upperLeftVertIndex;

                // lower right triangle
                tris[writeIndex + 3] = lowerRightVertIndex;
                tris[writeIndex + 4] = upperRightVertIndex;
                tris[writeIndex + 5] = lowerLeftVertIndex;

                writeIndex += 6;
            }

            rowStartVertIndex += panelHorizPixelCount;
        }

        // Setup the initial mesh data on the mesh filter
        {
            Mesh mesh = new Mesh();
            mesh.vertices = vertices;
            mesh.triangles = tris;
            mesh.normals = normals;
            mesh.uv = uv;
            mesh.colors32 = runtimeColors;
            mesh.RecalculateNormals();

            meshFilter.mesh = mesh;
        }

        // Setup the additional vertex streams used at runtime
        {
            runtimeMeshData = new Mesh();
            runtimeMeshData.vertices = vertices;
            runtimeMeshData.triangles = tris;
            runtimeMeshData.normals = normals;
            runtimeMeshData.uv = uv;
            runtimeMeshData.colors32 = runtimeColors;
            runtimeMeshData.RecalculateNormals();

            meshRenderer.additionalVertexStreams = runtimeMeshData;
        }

        // Setup the DMX data buffer
        {
            dmxColorData = new byte[NumChannels];

            for (int i = 0; i < NumChannels; ++i)
            {
                dmxColorData[i] = 0;
            }

            SetData(dmxColorData);
        }

        return true;
    }

    void OnDrawGizmos()
    {
#if UNITY_EDITOR
    UnityEditor.Handles.BeginGUI();

    var restoreColor = GUI.color;
    GUI.color = Color.white;

    for (int vertIndex = 0; vertIndex < runtimeMeshData.vertexCount; ++vertIndex)
    {
      Vector3 ledLocation = gameObject.transform.TransformPoint(runtimeMeshData.vertices[vertIndex]);
      int ledIndex = vertexToLEDIndexTable[vertIndex];

      UnityEditor.Handles.Label(ledLocation, ledIndex.ToString());
    }
    GUI.color = restoreColor;
    UnityEditor.Handles.EndGUI();
#endif
    }
}