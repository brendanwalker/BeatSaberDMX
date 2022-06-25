using System;
using System.Collections;
using System.Collections.Generic;
using BeatSaberDMX;
using BeatSaberDMX.Configuration;
using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(CapsuleCollider))]
[RequireComponent(typeof(Rigidbody))]
public class DmxPixelGrid : DmxChannelLayout
{
    public enum ePixelGridLayout
    {
        HorizontalLines,
        HorizontalLinesZigZag,
        VerticalLinesZigZagMirrored,
    }

    public float PhysicalArcLengthMeters { get; private set; }
    public float PhysicalRadiusMeters { get; private set; }
    public float PhysicalHightMeters { get; private set; }

    public int HorizontalPixelCount { get; private set; }
    public int VerticalPixelCount { get; private set; }
    public int TotalPixelCount { get; private set; }

    public override int NumChannels { get { return TotalPixelCount * 3; } }

    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private CapsuleCollider capsuleCollider;

    private Mesh runtimeMeshData;
    private Color32[] runtimeColors;
    private byte[] dmxColorData;

    private int[] vertexToLEDIndexTable;

    public static DmxPixelGrid InstantateGameObject(string Name)
    {
        GameObject ownerGameObject = new GameObject(
            Name,
            new System.Type[] { typeof(DmxPixelGrid) });

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

        return ownerGameObject.GetComponent<DmxPixelGrid>();
    }

    void Awake()
    {
        capsuleCollider = GetComponent<CapsuleCollider>();
        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();
    }

    void OnDestroy()
    {
        Plugin.Log?.Error($"DMXPixelGrid getting destroyed");
        //Plugin.Log?.Error(UnityEngine.StackTraceUtility.ExtractStackTrace());
    }

    private void OnTriggerEnter(Collider other)
    {
        HandleColliderOverlap(other.gameObject);
    }

    private void OnTriggerStay(Collider other)
    {
        HandleColliderOverlap(other.gameObject);
    }

    public void HandleColliderOverlap(GameObject gameObject)
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
            Vector3 localSegmentStart = this.gameObject.transform.InverseTransformPoint(worldSegmentStart);
            Vector3 localSegmentEnd = this.gameObject.transform.InverseTransformPoint(worldSegmentEnd);
            float radius = PluginConfig.Instance.SaberPaintRadius;

            for (int vertexIndex = 0; vertexIndex < runtimeColors.Length; ++vertexIndex)
            {
                Vector3 vertex = meshFilter.mesh.vertices[vertexIndex];

                if (DmxDeviceMath.IsPointWithinRadiusOfSegment(localSegmentStart, localSegmentEnd, radius, vertex))
                {
                    runtimeColors[vertexIndex].r = Math.Max(runtimeColors[vertexIndex].r, segmentColor.r);
                    runtimeColors[vertexIndex].g = Math.Max(runtimeColors[vertexIndex].g, segmentColor.g);
                    runtimeColors[vertexIndex].b = Math.Max(runtimeColors[vertexIndex].r, segmentColor.b);
                }
            }

        }
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

    public bool SetupPixelGridGeometry(
        ePixelGridLayout layout,
        float physArcLength, float physRadius, float physHeight,
        int horizPixelCount, int vertPixelCount)
    {
        PhysicalArcLengthMeters = physArcLength;
        PhysicalRadiusMeters = physRadius;
        PhysicalHightMeters = physHeight;
        HorizontalPixelCount = horizPixelCount;
        VerticalPixelCount = vertPixelCount;
        TotalPixelCount = horizPixelCount * vertPixelCount;

        if (horizPixelCount < 2 || vertPixelCount < 2)
            return false;

        // Build a table to map from vertex indices to LED indices
        // This is used for writing out DMX data
        vertexToLEDIndexTable = new int[TotalPixelCount];
        switch (layout)
        {
            case ePixelGridLayout.HorizontalLines:
                for (int LedIndex = 0; LedIndex < TotalPixelCount; ++LedIndex)
                {
                    vertexToLEDIndexTable[LedIndex] = LedIndex;
                }
                break;
            case ePixelGridLayout.HorizontalLinesZigZag:
                {
                    int ledIndex = 0;
                    for (int rowIndex = 0; rowIndex < vertPixelCount; ++rowIndex)
                    {
                        for (int colOffset = 0; colOffset < horizPixelCount; ++colOffset)
                        {
                            // Reverse LED direction on odd rows
                            int colIndex =
                                (rowIndex % 2 == 1)
                                ? (horizPixelCount - colOffset - 1)
                                : colOffset;

                            vertexToLEDIndexTable[rowIndex * horizPixelCount + colIndex] = ledIndex;
                            ++ledIndex;
                        }
                    }
                }
                break;
            case ePixelGridLayout.VerticalLinesZigZagMirrored:
                {
                    int ledIndex = 0;

                    // Left half of the columns
                    for (int colIndex = (horizPixelCount / 2) - 1; colIndex >= 0; --colIndex)
                    {
                        for (int rowOffset = 0; rowOffset < vertPixelCount; ++rowOffset)
                        {
                            // Reverse LED direction on odd columns
                            int rowIndex =
                                (colIndex % 2 == 1)
                                ? (vertPixelCount - rowOffset - 1)
                                : rowOffset;

                            vertexToLEDIndexTable[rowIndex * horizPixelCount + colIndex] = ledIndex;
                            ++ledIndex;
                        }
                    }

                    // Right half of the columns
                    for (int colIndex = (horizPixelCount / 2); colIndex < horizPixelCount; ++colIndex)
                    {
                        for (int rowOffset = 0; rowOffset < vertPixelCount; ++rowOffset)
                        {
                            // Reverse LED direction on even columns
                            int rowIndex =
                                (colIndex % 2 == 0)
                                ? (vertPixelCount - rowOffset - 1)
                                : rowOffset;

                            vertexToLEDIndexTable[rowIndex * horizPixelCount + colIndex] = ledIndex;
                            ++ledIndex;
                        }
                    }
                }
                break;
        }

        // Static mesh data
        Vector3[] vertices = new Vector3[TotalPixelCount];
        Vector3[] normals = new Vector3[TotalPixelCount];
        Vector2[] uv = new Vector2[TotalPixelCount];
        // Dynamic mesh data
        runtimeColors = new Color32[TotalPixelCount];

        if (physRadius > 0.0f)
        {
            // ArcLength = Radius * Angular Span
            float angularSpanRadians = physArcLength / physRadius;

            // Cylinder around Y-axis, starting LED on +X axis
            int vertIndex = 0;
            for (int j = 0; j < vertPixelCount; ++j)
            {
                float v = (float)j / (float)(vertPixelCount - 1);
                float y = (v - 0.5f) * physHeight;

                for (int i = 0; i < horizPixelCount; ++i)
                {
                    float u = (float)i / (float)(horizPixelCount - 1);
                    float theta = Mathf.Lerp(-0.5f * angularSpanRadians, 0.5f * angularSpanRadians, u);
                    float nx = Mathf.Cos(theta);
                    float nz = Mathf.Sin(theta);
                    float x = physRadius * nx;
                    float z = physRadius * nz;

                    vertices[vertIndex] = new Vector3(x, y, z);
                    normals[vertIndex] = new Vector3(nx, 0.0f, nz);
                    uv[vertIndex] = new Vector2(u, v);
                    runtimeColors[vertIndex] = new Color32(0, 0, 0, 255);

                    ++vertIndex;
                }
            }

            // Create a pill that encapsulated the cylinder
            capsuleCollider.radius = physRadius;
            capsuleCollider.height = physHeight + 2.0f * physRadius;
            capsuleCollider.direction = 1; // y-axis
        }
        else
        {
            int vertIndex = 0;
            float x = 0.0f;

            // yz-plane facing down +x
            for (int j = 0; j < vertPixelCount; ++j)
            {
                float v = (float)j / (float)(vertPixelCount - 1);
                float y = (v - 0.5f) * physHeight;

                for (int i = 0; i < horizPixelCount; ++i)
                {
                    float u = (float)i / (float)(horizPixelCount - 1);
                    float z = (u - 0.5f) * physArcLength;

                    vertices[vertIndex] = new Vector3(x, y, z);
                    normals[vertIndex] = new Vector3(1.0f, 0.0f, 0.0f);
                    uv[vertIndex] = new Vector2(u, v);
                    runtimeColors[vertIndex] = new Color32(0, 0, 0, 255);

                    ++vertIndex;
                }
            }

            // Create a pill that encapsulated the rectangle
            capsuleCollider.radius = physArcLength * 0.5f;
            capsuleCollider.height = physHeight + 2.0f * capsuleCollider.radius;
            capsuleCollider.direction = 1; // y-axis
        }

        // Create a triangle index array from the grid of vertices
        int horizQuadCount = (horizPixelCount - 1);
        int vertQuadCount = (vertPixelCount - 1);
        int[] tris = new int[horizQuadCount * vertQuadCount * 6]; // 2 tris per quad * 3 indices per tri

        int writeIndex = 0;
        int rowStartVertIndex = 0;
        for (int vertQuadIndex = 0; vertQuadIndex < vertQuadCount; ++vertQuadIndex)
        {
            for (int horizQuadIndex = 0; horizQuadIndex < horizQuadCount; ++horizQuadIndex)
            {
                int upperLeftVertIndex = rowStartVertIndex + horizQuadIndex;
                int upperRightVertIndex = upperLeftVertIndex + 1;
                int lowerLeftVertIndex = upperLeftVertIndex + horizPixelCount;
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

            rowStartVertIndex += horizPixelCount;
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