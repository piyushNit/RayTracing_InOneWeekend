using System.Drawing;
using System.Runtime.InteropServices;
using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
public class WaveManager : MonoBehaviour
{
    [System.Serializable]
    public struct Wave
    {
        public Vector2 direction;
        [Range(0.0f, 1.0f)]
        public float steepness;
        public float length;

        Vector4 output;

        public Wave(float dirX, float dirY, float steep, float len)
        {
            direction = new Vector2(dirX, dirY);
            steepness = steep;
            length = len;

            output = new Vector4(dirX, dirY, steep, len);
        }

        public Vector4 GetVec4()
        {
            output.x = direction.x;
            output.y = direction.y;
            output.z = steepness;
            output.w = length;
            return output;
        }

    }

    [Header("Plane Mesh Settings")]
    public int xSegments = 10;
    public int ySegments = 10;
    public Vector2 scale = Vector2.one;

    [Header("Compute Shader")]
    public ComputeShader waveCompute;

    [Header("Wave Properties")]
    public float waveHeight = 1.0f;
    public Wave waveA = new Wave (1.0f, 0.0f, 0.5f, 10);
    public Wave waveB = new Wave (0.0f, 1.0f, 0.25f, 20);
    public Wave waveC = new Wave(1.0f, 1.0f, 0.15f, 10);

    Mesh mesh;

    [Header("Draw debug")]
    public bool DrawWaveDebugMaterial;

    UnityEngine.Material waveMaterial;

    GraphicsBuffer restPositionBuffer;
    GraphicsBuffer positionBuffer;
    GraphicsBuffer normalBuffer;
    GraphicsBuffer indexBuffer;
    MaterialPropertyBlock props;

    public GraphicsBuffer PositionBufferGB { get { return positionBuffer; } }
    public GraphicsBuffer NormalBufferGB { get { return normalBuffer; } }
    public GraphicsBuffer IndexBufferGB { get { return indexBuffer; } }

    int totalVertexCount = 0;
    int totalIndexCount = 0;
    public int TotalVertexCount { get { return totalVertexCount; } }
    public int TotalIndexCount { get { return totalIndexCount; } }

    int waveKarnel = -1;

    private void Awake()
    {
        CreatePlaneMesh();

        props = new MaterialPropertyBlock();
        waveMaterial = GetComponent<MeshRenderer>().sharedMaterial;

        waveKarnel = waveCompute.FindKernel("CSMain");
    }

    void CreatePlaneMesh()
    {
        int xSegs = Mathf.Max(1, xSegments);
        int ySegs = Mathf.Max(1, ySegments);

        int xVerts = xSegs + 1;
        int yVerts = ySegs + 1;

        totalVertexCount = xVerts * yVerts;
        Vector3[] vertices = new Vector3[totalVertexCount];
        Vector2[] uvs = new Vector2[totalVertexCount];
        Vector3[] normals = new Vector3[totalVertexCount];
        int[] triangles = new int[xSegs * ySegs * 6];

        for (int y = 0; y < yVerts; y++)
        {
            for (int x = 0; x < xVerts; x++)
            {
                int vertIndex = y * xVerts + x;

                float u = (float)x / xSegs;
                float v = (float)y / ySegs;

                vertices[vertIndex] = new Vector3((u - 0.5f) * scale.x, 0f, (v - 0.5f) * scale.y);
                uvs[vertIndex] = new Vector2(u, v);
                normals[vertIndex] = Vector3.up;
            }
        }

        int triIndex = 0;
        for (int y = 0; y < ySegs; y++)
        {
            for (int x = 0; x < xSegs; x++)
            {
                int vertIndex = y * xVerts + x;

                triangles[triIndex++] = vertIndex;
                triangles[triIndex++] = vertIndex + xVerts;
                triangles[triIndex++] = vertIndex + 1;

                triangles[triIndex++] = vertIndex + 1;
                triangles[triIndex++] = vertIndex + xVerts;
                triangles[triIndex++] = vertIndex + xVerts + 1;
            }
        }

        totalIndexCount = triIndex;

        mesh = new Mesh();
        mesh.name = "ProceduralPlane";
        mesh.indexFormat = vertices.Length > 65535
            ? UnityEngine.Rendering.IndexFormat.UInt32
            : UnityEngine.Rendering.IndexFormat.UInt16;

        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.normals = normals;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();

        GetComponent<MeshFilter>().mesh = mesh;

        restPositionBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, totalVertexCount, Marshal.SizeOf<Vector3>());
        positionBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, totalVertexCount, Marshal.SizeOf<Vector3>());
        normalBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, totalVertexCount, Marshal.SizeOf<Vector3>());
        indexBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, triIndex, Marshal.SizeOf<int>());

        restPositionBuffer.SetData(vertices);
        positionBuffer.SetData(vertices);
        normalBuffer.SetData(normals);
        indexBuffer.SetData(triangles);
    }

    void OnDisable() => Release();

    void Release()
    {
        restPositionBuffer?.Release(); restPositionBuffer = null;
        positionBuffer?.Release(); positionBuffer = null;
        normalBuffer?.Release(); normalBuffer = null;
        indexBuffer?.Release(); indexBuffer = null;
    }

    private void Update()
    {
        UpdateWave();
        RenderDebugMesh();
    }

    void UpdateWave()
    {
        waveCompute.SetBuffer(waveKarnel, "restPositions", restPositionBuffer);
        waveCompute.SetBuffer(waveKarnel, "positions", positionBuffer);
        waveCompute.SetBuffer(waveKarnel, "normals", normalBuffer);

        waveCompute.SetInt("XSegments", xSegments);
        waveCompute.SetInt("YSegments", ySegments);
        waveCompute.SetFloat("time", Time.time);
        waveCompute.SetFloat("deltaTime", Time.deltaTime);
        waveCompute.SetFloat("waveHeight", waveHeight);
        waveCompute.SetVector("waveA", waveA.GetVec4());
        waveCompute.SetVector("waveB", waveB.GetVec4());
        waveCompute.SetVector("waveC", waveC.GetVec4());

        int dispatchX = Mathf.CeilToInt((xSegments + 1) / 8.0f);
        int dispatchY = Mathf.CeilToInt((ySegments + 1) / 8.0f);
        waveCompute.Dispatch(waveKarnel, dispatchX, dispatchY, 1);
    }

    void RenderDebugMesh()
    {
        if (!DrawWaveDebugMaterial)
            return;

        props.SetBuffer("_Positions", positionBuffer);
        props.SetBuffer("_Normals", normalBuffer);
        props.SetBuffer("_Indices", indexBuffer);
        props.SetMatrix("_ObjectToWorld", transform.localToWorldMatrix);

        var rp = new RenderParams(waveMaterial)
        {
            matProps = props,
            // Bounds are needed for culling since there is no Mesh.
            worldBounds = new Bounds(transform.position,
                Vector3.Scale(transform.lossyScale, new Vector3(scale.x, 2, scale.y)))
        };
        Graphics.RenderPrimitives(rp, MeshTopology.Triangles, totalIndexCount);
    }
}
