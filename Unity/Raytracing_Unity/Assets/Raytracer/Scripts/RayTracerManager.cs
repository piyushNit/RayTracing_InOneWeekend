using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using UnityEngine;

[System.Serializable]
struct RaySphere
{
    public Vector3 position;
    public float radius;
    public float4 color;
    public float4 emissive;
};

[System.Serializable]
struct RayQuad
{
    public float4 center_sizeX;
    public float4 normal_sizeY;
    public float4 tangent;   // xyz : Right direction of the quad, w : pad
    public float4 bitangent; // xyz : Up direction of the quad, w : pad
    public float4 color;//xyz : color, w : pad
    public float4 emissive;//xyz : color, w : pad
};

struct RayLight
{
    public Vector3 position;
    public float radius;
    public float4 color;
    public float4 emissive;
};

public class RayTracerManager : MonoBehaviour
{
    public int rayBounceCount = 1;
    public RenderTexture renderTarget;
    public ComputeShader rayTracerCS;

    private List<SphereMesh> sceneSphereMeshes;
    private List<QuadMesh> sceneQuadMeshes;
    RayTraceLight rayTraceLight;

    private ComputeBuffer sphereMeshBuffers = null;
    private ComputeBuffer quadMeshBuffer = null;
    private ComputeBuffer sceneLightBuffers = null;

    int kernalHandle = -1;

    RayTracerCamera rayTracerCamera;

    int iFrame = 0;
    RaySphere[] sceneSpheres;
    RayQuad[] sceneQuades;
    RayLight[] sceneLights;

    void Awake()
    {
        FindSphereMeshes();
        FindQuadMeshes();

        rayTraceLight = FindAnyObjectByType<RayTraceLight>();
        rayTracerCamera = Camera.main.GetComponent<RayTracerCamera>();

        InitializeBuffers();
    }

    void InitializeBuffers()
    {
        int totalItems = sceneSphereMeshes.Count;
        sceneSpheres = new RaySphere[totalItems];

        int totalQuads = sceneQuadMeshes.Count;
        sceneQuades = new RayQuad[totalQuads];

        int sphStride = System.Runtime.InteropServices.Marshal.SizeOf(typeof(RaySphere));
        sphereMeshBuffers = new ComputeBuffer(totalItems, sphStride);

        int quadStride = System.Runtime.InteropServices.Marshal.SizeOf(typeof(RayQuad));
        quadMeshBuffer = new ComputeBuffer(totalQuads, quadStride);

        int lightStride = System.Runtime.InteropServices.Marshal.SizeOf(typeof(RayLight));
        sceneLightBuffers = new ComputeBuffer(1, lightStride);
        sceneLights = new RayLight[1];
    }

    void FindSphereMeshes()
    {
        var sphereMeshes = FindObjectsByType<SphereMesh>();
        sceneSphereMeshes = sphereMeshes.ToList();
    }

    void FindQuadMeshes()
    {
        var quadMeshes = FindObjectsByType<QuadMesh>();
        sceneQuadMeshes = quadMeshes.ToList();
    }

    private void OnDestroy()
    {
        sphereMeshBuffers.Release();
        quadMeshBuffer.Release();
        sceneLightBuffers.Release();
    }

    void Update()
    {
        if(renderTarget == null || rayTracerCS == null)
            return;
        
        if(kernalHandle == -1)
            kernalHandle = rayTracerCS.FindKernel("CSMain");

        CreateTextureIfRequired();
        DispatchComputeShader();

        iFrame += 1;
    }

    void CreateTextureIfRequired()
    {
        if(!renderTarget.IsCreated())
        {
            renderTarget.enableRandomWrite = true;
            renderTarget.Create();
        }
    }

    void DispatchComputeShader()
    {
        if(kernalHandle == -1 || rayTracerCamera == null)
            return;

        rayTracerCS.SetTexture(kernalHandle, "Result", renderTarget);
        rayTracerCS.SetMatrix("_CameraToWorld", Camera.main.cameraToWorldMatrix);
        rayTracerCS.SetMatrix("_CameraInverseProjection", Camera.main.projectionMatrix.inverse);
        rayTracerCS.SetFloat("focalLength", rayTracerCamera.focalLength);
        rayTracerCS.SetVector("cameraCenter", rayTracerCamera.Position);
        rayTracerCS.SetInt("iFrame", iFrame);
        rayTracerCS.SetInt("rayBounceCount", Mathf.Max(rayBounceCount, 1));

        UpdateSphereComputeBuffers();
        UpdateQuadComputeBuffers();
        UpdateSceneLight();

        int dispatchX = Mathf.CeilToInt((float)renderTarget.width / 8.0f);
        int dispatchY = Mathf.CeilToInt((float)renderTarget.height / 8.0f);
        rayTracerCS.Dispatch(kernalHandle, dispatchX, dispatchY, 1);
    }

    void UpdateSceneLight()
    {
        int totalSceneLights = 0;
        if(rayTraceLight == null)
        {
            sceneLights[0].position = float3.zero;
            sceneLights[0].radius = 0;
            sceneLights[0].color = float4.zero;
            sceneLights[0].emissive = float4.zero;
        }else
        {
            sceneLights[0].position = rayTraceLight.Position;
            sceneLights[0].radius = rayTraceLight.Radius;
            // color drives the diffuse lighting, emissive is the color of the
            // visible light sphere
            sceneLights[0].color = new float4(rayTraceLight.Color.r, rayTraceLight.Color.g, rayTraceLight.Color.b, 1.0f);
            sceneLights[0].emissive = new float4(rayTraceLight.Color.r, rayTraceLight.Color.g, rayTraceLight.Color.b, 1.0f);
            sceneLights[0].emissive *= rayTraceLight.EmissiveMultiplier;

            totalSceneLights = 1;
        }

        sceneLightBuffers.SetData(sceneLights);
        rayTracerCS.SetBuffer(kernalHandle, "sceneLights", sceneLightBuffers);
        rayTracerCS.SetInt("totalSceneLightCount", totalSceneLights);
    }

    void UpdateSphereComputeBuffers()
    {
        int totalItems = sceneSphereMeshes.Count;
        for(int i = 0; i < totalItems; i++)
        {
            SphereMesh sphMesh = sceneSphereMeshes[i];
            sceneSpheres[i].position = sphMesh.GetPosition();
            sceneSpheres[i].radius = sphMesh.GetRadius();
            sceneSpheres[i].color = new float4(sphMesh.Color.r, sphMesh.Color.g, sphMesh.Color.b, 1.0f);
            sceneSpheres[i].emissive = float4.zero;
        }

        sphereMeshBuffers.SetData(sceneSpheres);

        rayTracerCS.SetBuffer(kernalHandle, "sphereMeshes", sphereMeshBuffers);
        rayTracerCS.SetInt("sphereMeshCount", totalItems);
    }

    void UpdateQuadComputeBuffers()
    {
        int totalItems = sceneQuadMeshes.Count;
        for(int i = 0; i < totalItems; i++)
        {
            QuadMesh quadMesh = sceneQuadMeshes[i];
            Transform qT = quadMesh.transform; 
            Vector3 pos = qT.position;
            Vector2 size = new Vector2(qT.localScale.x, qT.localScale.y);
            sceneQuades[i].center_sizeX =  new float4(pos.x, pos.y, pos.z, size.x);
            Vector3 normal = qT.rotation * Vector3.back;
            sceneQuades[i].normal_sizeY = new float4(normal.x, normal.y, normal.z, size.y);
            Vector3 tangent = qT.rotation * Vector3.right;
            Vector3 biTangent = qT.rotation * Vector3.up;
            sceneQuades[i].tangent = new float4(tangent.x, tangent.y, tangent.z, 1.0f);
            sceneQuades[i].bitangent = new float4(biTangent.x, biTangent.y, biTangent.z, 1.0f);
            sceneQuades[i].color = new float4(quadMesh.color.r, quadMesh.color.g, quadMesh.color.b, 1.0f);
            sceneQuades[i].emissive = float4.zero;
        }
        quadMeshBuffer.SetData(sceneQuades);
        rayTracerCS.SetBuffer(kernalHandle, "quadMeshes", quadMeshBuffer);
        rayTracerCS.SetInt("quadMeshCount", totalItems);
    }
}
