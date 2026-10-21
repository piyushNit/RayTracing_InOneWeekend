using System;
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
    public int perPixelSampelCount = 1;
    public RenderTexture renderTarget;
    public ComputeShader rayTracerCS;
    public ComputeShader colorAccuCS;
    public bool useAccumulationTexture = true;

    private List<SphereMesh> sceneSphereMeshes;
    private List<QuadMesh> sceneQuadMeshes;
    private List<RayTraceLight> rayTraceLights;
    private List<QuadLight> rayQuadLights;

    private ComputeBuffer sphereMeshBuffers = null;
    private ComputeBuffer quadMeshBuffer = null;
    private ComputeBuffer sceneLightBuffers = null;
    private ComputeBuffer sceneQuadLightBuffer = null;

    int kernalHandle = -1;
    int colorAccuKernalHandle = -1;

    RayTracerCamera rayTracerCamera;

    int iFrame = 0;
    RaySphere[] sceneSpheres;
    RayQuad[] sceneQuades;
    RayLight[] sceneLights;
    RayQuad[] sceneQuadLights;

    RenderTexture tempRenderTex;

    public float4 defEmissive = new float4(1, 1, 1, 1);

    void Awake()
    {
        FindSphereMeshes();
        FindQuadMeshes();

        FindSphereLights();
        FindQuadLights();

        rayTracerCamera = Camera.main.GetComponent<RayTracerCamera>();

        InitializeBuffers();

        tempRenderTex = new RenderTexture(renderTarget);
    }

    void InitializeBuffers()
    {
        int totalItems = Mathf.Max(sceneSphereMeshes.Count, 1);
        int totalQuads = Mathf.Max(sceneQuadMeshes.Count, 1);
        int totalShpLight = Mathf.Max(rayTraceLights.Count, 1);
        int totalQuadLights = Mathf.Max(rayQuadLights.Count, 1);

        sceneSpheres = new RaySphere[totalItems];
        sceneQuades = new RayQuad[totalQuads];

        int sphStride = System.Runtime.InteropServices.Marshal.SizeOf(typeof(RaySphere));
        sphereMeshBuffers = new ComputeBuffer(totalItems, sphStride);

        int quadStride = System.Runtime.InteropServices.Marshal.SizeOf(typeof(RayQuad));
        quadMeshBuffer = new ComputeBuffer(totalQuads, quadStride);

        int lightStride = System.Runtime.InteropServices.Marshal.SizeOf(typeof(RayLight));
        sceneLightBuffers = new ComputeBuffer(totalShpLight, lightStride);
        sceneLights = new RayLight[totalShpLight];

        sceneQuadLightBuffer = new ComputeBuffer(totalQuadLights, quadStride);
        sceneQuadLights = new RayQuad[totalQuadLights];
    }

    void FindSphereMeshes()
    {
        var sphereMeshes = FindObjectsByType<SphereMesh>();
        sceneSphereMeshes = sphereMeshes.ToList();
    }

    void FindSphereLights()
    {
        var sphLights = FindObjectsByType<RayTraceLight>();
        rayTraceLights = sphLights.ToList();
    }

    void FindQuadLights()
    {
        var sphLights = FindObjectsByType<QuadLight>();
        rayQuadLights = sphLights.ToList();
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
        sceneQuadLightBuffer.Release();
        tempRenderTex.Release();
    }

    void Update()
    {
        if(renderTarget == null || rayTracerCS == null)
            return;
        
        if(kernalHandle == -1)
            kernalHandle = rayTracerCS.FindKernel("CSMain");

        if(colorAccuKernalHandle == -1)
            colorAccuKernalHandle = colorAccuCS.FindKernel("CSMain");

        CreateTextureIfRequired();
        DispatchRayTraceCS();
        DispatchColorAccuCS();

        iFrame += 1;
    }

    void CreateTextureIfRequired()
    {
        if(!renderTarget.IsCreated())
        {
            renderTarget.enableRandomWrite = true;
            renderTarget.Create();
        }

        if(!tempRenderTex.IsCreated())
        {
            tempRenderTex.enableRandomWrite = true;
            tempRenderTex.Create();
        }
    }

    void DispatchRayTraceCS()
    {
        if(kernalHandle == -1 || rayTracerCamera == null)
            return;

        if(useAccumulationTexture)
            rayTracerCS.SetTexture(kernalHandle, "Result", tempRenderTex);
        else
            rayTracerCS.SetTexture(kernalHandle, "Result", renderTarget);
        rayTracerCS.SetMatrix("_CameraToWorld", Camera.main.cameraToWorldMatrix);
        rayTracerCS.SetMatrix("_CameraInverseProjection", Camera.main.projectionMatrix.inverse);
        rayTracerCS.SetFloat("focalLength", rayTracerCamera.focalLength);
        rayTracerCS.SetVector("cameraCenter", rayTracerCamera.Position);
        rayTracerCS.SetInt("iFrame", iFrame);
        rayTracerCS.SetInt("rayBounceCount", Mathf.Max(rayBounceCount, 1));
        rayTracerCS.SetInt("perPixelSampleCount", Mathf.Max(perPixelSampelCount, 1));

        UpdateSphereComputeBuffers();
        UpdateQuadComputeBuffers();
        UpdateSceneLight();
        UpdateQuadLightBuffer();

        if(useAccumulationTexture)
        {
            int dispatchX = Mathf.CeilToInt((float)tempRenderTex.width / 8.0f);
            int dispatchY = Mathf.CeilToInt((float)tempRenderTex.height / 8.0f);
            rayTracerCS.Dispatch(kernalHandle, dispatchX, dispatchY, 1);
        }else
        {
            int dispatchX = Mathf.CeilToInt((float)renderTarget.width / 8.0f);
            int dispatchY = Mathf.CeilToInt((float)renderTarget.height / 8.0f);
            rayTracerCS.Dispatch(kernalHandle, dispatchX, dispatchY, 1);
        }
    }

    void DispatchColorAccuCS()
    {
        if(!useAccumulationTexture)
            return;

        if(colorAccuKernalHandle == -1)
            return;

        colorAccuCS.SetTexture(colorAccuKernalHandle, "Result", renderTarget);
        colorAccuCS.SetTexture(colorAccuKernalHandle, "SrcTarget", tempRenderTex);
        int dispatchX = Mathf.CeilToInt((float)renderTarget.width / 8.0f);
        int dispatchY = Mathf.CeilToInt((float)renderTarget.height / 8.0f);
        colorAccuCS.Dispatch(kernalHandle, dispatchX, dispatchY, 1);
    }

    void UpdateSceneLight()
    {
        int totalSceneLights = rayTraceLights.Count;
        if(totalSceneLights <= 0)
        {
            sceneLights[0].position = float3.zero;
            sceneLights[0].radius = 0;
            sceneLights[0].color = float4.zero;
            sceneLights[0].emissive = float4.zero;
        }else
        {
            for(int i = 0; i < totalSceneLights; i++)
            {
                RayTraceLight qLight = rayTraceLights[i];
                sceneLights[i].position = qLight.Position;
                sceneLights[i].radius = qLight.Radius;
                // color drives the diffuse lighting, emissive is the color of the
                // visible light sphere
                sceneLights[i].color = new float4(qLight.Color.r, qLight.Color.g, qLight.Color.b, 1.0f);
                sceneLights[i].emissive = new float4(qLight.Color.r, qLight.Color.g, qLight.Color.b, 1.0f);
                sceneLights[i].emissive *= qLight.EmissiveMultiplier;
            }
        }

        sceneLightBuffers.SetData(sceneLights);
        rayTracerCS.SetBuffer(kernalHandle, "sceneLights", sceneLightBuffers);
        rayTracerCS.SetInt("totalSceneLightCount", totalSceneLights);
    }

    void UpdateQuadLightBuffer()
    {
        int totalSceneLights = rayQuadLights.Count;
        if(totalSceneLights <= 0)
        {
            sceneQuadLights[0].center_sizeX = float4.zero;
            sceneQuadLights[0].normal_sizeY = float4.zero;
            sceneQuadLights[0].tangent = float4.zero;
            sceneQuadLights[0].bitangent = float4.zero;
            sceneQuadLights[0].color = float4.zero;
            sceneQuadLights[0].emissive = float4.zero;
        }else
        {
            for(int i = 0; i < totalSceneLights; i++)
            {
                QuadLight qLight = rayQuadLights[i];
                float4 emission = new float4(qLight.color.r, qLight.color.g, qLight.color.b, 1.0f);
                emission *= qLight.explosion;
                sceneQuadLights[i] = GetQuad(qLight.transform, qLight.color, emission);
            }
        }

        sceneQuadLightBuffer.SetData(sceneQuadLights);
        rayTracerCS.SetBuffer(kernalHandle, "sceneQuadLights", sceneQuadLightBuffer);
        rayTracerCS.SetInt("totalSceneQuadLightCount", totalSceneLights);
    }

    void UpdateSphereComputeBuffers()
    {
        int totalItems = sceneSphereMeshes.Count;
        if(totalItems <= 0)
        {
            sceneSpheres[0].position = float3.zero;
            sceneSpheres[0].radius = 0.0f;
            sceneSpheres[0].color = float4.zero;
            sceneSpheres[0].emissive = float4.zero;
        }else
        {
            for(int i = 0; i < totalItems; i++)
            {
                SphereMesh sphMesh = sceneSphereMeshes[i];
                sceneSpheres[i].position = sphMesh.GetPosition();
                sceneSpheres[i].radius = sphMesh.GetRadius();
                sceneSpheres[i].color = new float4(sphMesh.Color.r, sphMesh.Color.g, sphMesh.Color.b, 1.0f);
                sceneSpheres[i].emissive = defEmissive;
            }
        }

        sphereMeshBuffers.SetData(sceneSpheres);

        rayTracerCS.SetBuffer(kernalHandle, "sphereMeshes", sphereMeshBuffers);
        rayTracerCS.SetInt("sphereMeshCount", totalItems);
    }

    void UpdateQuadComputeBuffers()
    {
        int totalItems = sceneQuadMeshes.Count;
        if(totalItems <= 0)
        {
            sceneQuades[0].center_sizeX = float4.zero;
            sceneQuades[0].normal_sizeY = float4.zero;
            sceneQuades[0].tangent = float4.zero;
            sceneQuades[0].bitangent = float4.zero;
            sceneQuades[0].color = float4.zero;
            sceneQuades[0].emissive = float4.zero;
        }else
        {
            for(int i = 0; i < totalItems; i++)
            {
                QuadMesh quadMesh = sceneQuadMeshes[i];
                sceneQuades[i] = GetQuad(quadMesh.transform, quadMesh.color, defEmissive);
            }
        }
        quadMeshBuffer.SetData(sceneQuades);
        rayTracerCS.SetBuffer(kernalHandle, "quadMeshes", quadMeshBuffer);
        rayTracerCS.SetInt("quadMeshCount", totalItems);
    }

    RayQuad GetQuad(Transform t, Color color, float4 emission)
    {
        RayQuad quad;
        Vector3 pos = t.position;
        Vector2 size = new Vector2(t.localScale.x, t.localScale.y);
        quad.center_sizeX =  new float4(pos.x, pos.y, pos.z, size.x);
        Vector3 normal = -t.forward;
        quad.normal_sizeY = new float4(normal.x, normal.y, normal.z, size.y);
        Vector3 tangent = t.right;
        Vector3 biTangent = t.up;
        quad.tangent = new float4(tangent.x, tangent.y, tangent.z, 1.0f);
        quad.bitangent = new float4(biTangent.x, biTangent.y, biTangent.z, 1.0f);
        quad.color = new float4(color.r, color.g, color.b, 1.0f);
        quad.emissive = emission;
        return quad;
    }
}
