using System;
using System.Collections;
using System.Collections.Generic;
using BeatSaberDMX;
using BeatSaberDMX.Configuration;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

public class DmxGridLayoutDefinition : DmxLayoutDefinition
{
    public enum ePixelGridLayout
    {
        HorizontalLines,
        HorizontalLinesZigZag,
        VerticalLinesZigZagMirrored,
    }

    public ePixelGridLayout Layout { get; set; }

    public float PhysicalWidthMeters { get; set; }
    public float PhysicalHightMeters { get; set; }

    public int HorizontalPanelPixelCount { get; set; }
    public int VerticalPanelPixelCount { get; set; }

    public List<DmxDeviceDefinition> Devices { get; set; }
}

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(BoxCollider))]
[RequireComponent(typeof(Rigidbody))]
public class DmxGridLayoutInstance : DmxLayoutInstance
{
    public float PhysicalWidthMeters { get; private set; }
    public float PhysicalHightMeters { get; private set; }

    public int HorizontalPixelCount { get; private set; }
    public int VerticalPixelCount { get; private set; }
    public int TotalPixelCount { get; private set; }

    public override int NumChannels { get { return TotalPixelCount * 3; } }

    public DmxDeviceInstance[] Devices { get; private set; }

    private MeshRenderer meshRenderer;
    private BoxCollider boxCollider;

    private Mesh runtimeMeshData;
    private byte[] dmxColorData;

    private int[] vertexToLEDIndexTable;

    public static DmxGridLayoutInstance SpawnInstance(DmxGridLayoutDefinition layoutDefinition, Transform gameOrigin)
    {
        List<System.Type> componentTypes = new List<System.Type>();
        componentTypes.Add(typeof(DmxGridLayoutInstance));
        foreach(DmxDeviceDefinition deviceDefinition in layoutDefinition.Devices)
        {
            componentTypes.Add(typeof(DmxDeviceInstance));
        }

        GameObject ownerGameObject = new GameObject(layoutDefinition.Name, componentTypes.ToArray());
        GameObject.DontDestroyOnLoad(ownerGameObject);

        var col = ownerGameObject.GetComponent<BoxCollider>();
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

        DmxGridLayoutInstance instance = ownerGameObject.GetComponent<DmxGridLayoutInstance>();

        instance.SetupPixelGridGeometry(layoutDefinition);

        instance.gameObject.transform.parent = gameOrigin;
        instance.SetDMXTransform(layoutDefinition.Transform);

        instance.Devices = ownerGameObject.GetComponents<DmxDeviceInstance>();

        int startChannelIndex = 0;
        for (int deviceIndex = 0; deviceIndex < instance.Devices.Length; deviceIndex++)
        {
            DmxDeviceDefinition deviceDefinition = layoutDefinition.Devices[deviceIndex];
            DmxDeviceInstance deviceInstance= instance.Devices[deviceIndex];
            int channelCount = deviceDefinition.LedCount * 3;

            deviceInstance.useBroadcast = false;
            deviceInstance.remoteIP = deviceDefinition.DeviceIP;
            deviceInstance.startUniverseId = deviceDefinition.StartUniverse;
            deviceInstance.fps = 30;
            deviceInstance.AppendDMXLayout(instance, startChannelIndex, channelCount);

            startChannelIndex += channelCount;
        }

        return instance;
    }

    void Awake()
    {
        boxCollider = GetComponent<BoxCollider>();
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
        ProcessSegmentColliderOverlap(other.gameObject);
    }

    private void OnTriggerStay(Collider other)
    {
        ProcessSegmentColliderOverlap(other.gameObject);
    }

    private void ProcessNoteProjection(Transform NoteTransform, float NoteSize, Color32 NoteColor)
    {
        // Get the note position and basis axis
        Vector3 notePosition = NoteTransform.position;
        Vector3 noteXAxis = NoteTransform.right;
        Vector3 noteYAxis = NoteTransform.up;
        Vector3 noteZAxis = NoteTransform.forward;

        // Get the grid position and basis axis
        Transform gridChannel= this.gameObject.transform;
        Vector3 gridCenter = gridChannel.position;
        Vector3 gridHorizontalAxis = gridChannel.forward; // +Z-axis
        Vector3 gridVerticalAxis = gridChannel.up; // +Y-axis
        Vector3 gridNormal = gridChannel.right; // +X-axis

        // Project the note center onto the grid
        // and compute grid surface 2D coordinates
        Vector3 noteOffset = notePosition - gridCenter;
        Vector3 localNotePosOnGrid= Vector3.ProjectOnPlane(noteOffset, gridNormal);        
        float localNoteHorizPos = Vector3.Dot(localNotePosOnGrid, gridHorizontalAxis);
        float localNoteVertPos = Vector3.Dot(localNotePosOnGrid, gridVerticalAxis);

        // If the note projection is within the bounds of the grid
        // do the work of overlapping the note box with the pixel grid
        float HorizExtents = (PhysicalWidthMeters / 2.0f) + NoteSize;
        float VertExtents = (PhysicalHightMeters / 2.0f) + NoteSize;
        if (Mathf.Abs(localNoteHorizPos) <= HorizExtents && Mathf.Abs(localNoteVertPos) <= VertExtents)
        {
            Vector3 projectedNoteCenter = gridCenter + localNotePosOnGrid; // Note center projected on grid, world space

            var jobData = new OverlapBoxJob();
            jobData.boxCenter = gridChannel.InverseTransformPoint(projectedNoteCenter); 
            jobData.boxXAxis = gridChannel.InverseTransformDirection(noteXAxis);
            jobData.boxYAxis = gridChannel.InverseTransformDirection(noteYAxis);
            jobData.boxZAxis = gridChannel.InverseTransformDirection(noteZAxis);
            jobData.boxExtents = new Vector3(NoteSize, NoteSize, NoteSize);
            jobData.boxColor= NoteColor;
            jobData.vertices = new NativeArray<Vector3>(meshFilter.mesh.vertices, Allocator.TempJob);
            jobData.runtimeColors = new NativeArray<Color32>(runtimeColors, Allocator.TempJob);

            var batchSize = 32;
            var handle = jobData.Schedule(runtimeColors.Length, batchSize);

            handle.Complete();

            jobData.runtimeColors.CopyTo(runtimeColors);

            jobData.vertices.Dispose();
            jobData.runtimeColors.Dispose();
        }
    }

    private void ProjectNotes()
    {
        Color32 ColorA = BeatSaberDMXController.Instance.ColorA;
        Color32 ColorB = BeatSaberDMXController.Instance.ColorB;
        float NoteSize = PluginConfig.Instance.NotePaintSize;

        foreach (Transform NoteTransform in BeatSaberDMXController.Instance.ColorANotes)
        {
            ProcessNoteProjection(NoteTransform, NoteSize, ColorA);
        }

        foreach (Transform NoteTransform in BeatSaberDMXController.Instance.ColorBNotes)
        {
            ProcessNoteProjection(NoteTransform, NoteSize, ColorB);
        }
    }

    private void Update()
    {
        // Project the current active set of notes to the pixel grid
        ProjectNotes();

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
        DmxGridLayoutDefinition definition)
    {
        PhysicalWidthMeters = definition.PhysicalWidthMeters;
        PhysicalHightMeters = definition.PhysicalHightMeters;
        HorizontalPixelCount = definition.HorizontalPanelPixelCount;
        VerticalPixelCount = definition.VerticalPanelPixelCount;
        TotalPixelCount = HorizontalPixelCount * VerticalPixelCount;

        if (HorizontalPixelCount < 2 || VerticalPixelCount < 2)
            return false;

        // Build a table to map from vertex indices to LED indices
        // This is used for writing out DMX data
        vertexToLEDIndexTable = new int[TotalPixelCount];
        switch (definition.Layout)
        {
            case DmxGridLayoutDefinition.ePixelGridLayout.HorizontalLines:
                for (int LedIndex = 0; LedIndex < TotalPixelCount; ++LedIndex)
                {
                    vertexToLEDIndexTable[LedIndex] = LedIndex;
                }
                break;
            case DmxGridLayoutDefinition.ePixelGridLayout.HorizontalLinesZigZag:
                {
                    int ledIndex = 0;
                    for (int rowIndex = 0; rowIndex < VerticalPixelCount; ++rowIndex)
                    {
                        for (int colOffset = 0; colOffset < HorizontalPixelCount; ++colOffset)
                        {
                            // Reverse LED direction on odd rows
                            int colIndex =
                                (rowIndex % 2 == 1)
                                ? (HorizontalPixelCount - colOffset - 1)
                                : colOffset;

                            vertexToLEDIndexTable[rowIndex * HorizontalPixelCount + colIndex] = ledIndex;
                            ++ledIndex;
                        }
                    }
                }
                break;
            case DmxGridLayoutDefinition.ePixelGridLayout.VerticalLinesZigZagMirrored:
                {
                    int ledIndex = 0;

                    // Left half of the columns
                    for (int colIndex = (HorizontalPixelCount / 2) - 1; colIndex >= 0; --colIndex)
                    {
                        for (int rowOffset = 0; rowOffset < VerticalPixelCount; ++rowOffset)
                        {
                            // Reverse LED direction on odd columns
                            int rowIndex =
                                (colIndex % 2 == 1)
                                ? (VerticalPixelCount - rowOffset - 1)
                                : rowOffset;

                            vertexToLEDIndexTable[rowIndex * HorizontalPixelCount + colIndex] = ledIndex;
                            ++ledIndex;
                        }
                    }

                    // Right half of the columns
                    for (int colIndex = (HorizontalPixelCount / 2); colIndex < HorizontalPixelCount; ++colIndex)
                    {
                        for (int rowOffset = 0; rowOffset < VerticalPixelCount; ++rowOffset)
                        {
                            // Reverse LED direction on even columns
                            int rowIndex =
                                (colIndex % 2 == 0)
                                ? (VerticalPixelCount - rowOffset - 1)
                                : rowOffset;

                            vertexToLEDIndexTable[rowIndex * HorizontalPixelCount + colIndex] = ledIndex;
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

        {
            int vertIndex = 0;
            float x = 0.0f;

            // yz-plane facing down +x
            for (int j = 0; j < VerticalPixelCount; ++j)
            {
                float v = (float)j / (float)(VerticalPixelCount - 1);
                float y = (0.5f - v) * PhysicalHightMeters;

                for (int i = 0; i < HorizontalPixelCount; ++i)
                {
                    float u = (float)i / (float)(HorizontalPixelCount - 1);
                    float z = (u - 0.5f) * PhysicalWidthMeters;

                    vertices[vertIndex] = new Vector3(x, y, z);
                    normals[vertIndex] = new Vector3(1.0f, 0.0f, 0.0f);
                    uv[vertIndex] = new Vector2(u, v);
                    runtimeColors[vertIndex] = new Color32(0, 0, 0, 255);

                    ++vertIndex;
                }
            }

            // Create a box that encapsulates the rectangle
            boxCollider.size = new Vector3(0.1f, PhysicalHightMeters, PhysicalWidthMeters);
        }

        // Create a triangle index array from the grid of vertices
        int horizQuadCount = (HorizontalPixelCount - 1);
        int vertQuadCount = (VerticalPixelCount - 1);
        int[] tris = new int[horizQuadCount * vertQuadCount * 6]; // 2 tris per quad * 3 indices per tri

        int writeIndex = 0;
        int rowStartVertIndex = 0;
        for (int vertQuadIndex = 0; vertQuadIndex < vertQuadCount; ++vertQuadIndex)
        {
            for (int horizQuadIndex = 0; horizQuadIndex < horizQuadCount; ++horizQuadIndex)
            {
                int upperLeftVertIndex = rowStartVertIndex + horizQuadIndex;
                int upperRightVertIndex = upperLeftVertIndex + 1;
                int lowerLeftVertIndex = upperLeftVertIndex + HorizontalPixelCount;
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

            rowStartVertIndex += HorizontalPixelCount;
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