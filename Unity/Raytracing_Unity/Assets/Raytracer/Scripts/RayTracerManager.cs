using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using UnityEngine;

[System.Serializable]
struct Material
{
    public int matType;
    public float3 albedo;
    public float3 emissive;
    public float emissiveStrength;

    public float reflectionFuzz;
    public float RefractionIndex;
    public float2 pad0;

    public static Material ZeroMat()
    {
        Material material = new Material();
        material.matType = 0;
        material.albedo = float3.zero;
        material.emissive = float3.zero;
        material.emissiveStrength = 0;
        material.reflectionFuzz = 0;
        material.RefractionIndex = 0;
        material.pad0 = float2.zero;
        return material;
    }
}

[System.Serializable]
public struct Triangle
{
    public float3 v0;
    public float3 v1;
    public float3 v2;

    public float3 n0;
    public float3 n1;
    public float3 n2;
};

[System.Serializable]
struct MeshInfo
{
    public uint triIndexStart;
    public uint triCount;
    public float3 boundMin;
    public float3 boundMax;
    public Material material;
};

[System.Serializable]
struct RaySphere
{
    public Vector3 position;
    public float radius;
    public Material material;
};

[System.Serializable]
struct RayQuad
{
    public float4 center_sizeX;
    public float4 normal_sizeY;
    public float4 tangent;   // xyz : Right direction of the quad, w : pad
    public float4 bitangent; // xyz : Up direction of the quad, w : pad
    public Material material;
};

struct RayLight
{
    public Vector3 position;
    public float radius;
    public Material material;
};

public class RayTracerManager : MonoBehaviour
{
    public int rayBounceCount = 1;
    public int perPixelSampelCount = 1;
    public RenderTexture renderTarget;
    public ComputeShader rayTracerCS;
    public ComputeShader colorAccuCS;
    public bool useAccumulationTexture = true;
    public bool applySkyboxColor = true;
    [Range(0.0f, 1.0f)]
    public float skyColorApplyRatio = 1.0f;

    private List<SphereMesh> sceneSphereMeshes;
    private List<QuadMesh> sceneQuadMeshes;
    private List<RayTraceLight> rayTraceLights;
    private List<QuadLight> rayQuadLights;
    private List<MeshTriangle> meshTriangleList;

    private ComputeBuffer sphereMeshBuffers = null;
    private ComputeBuffer quadMeshBuffer = null;
    private ComputeBuffer sceneLightBuffers = null;
    private ComputeBuffer sceneQuadLightBuffer = null;
    private ComputeBuffer triangleListBuffer = null;
    private ComputeBuffer meshListBuffer = null;

    int kernalHandle = -1;
    int colorAccuKernalHandle = -1;

    RayTracerCamera rayTracerCamera;

    int iFrame = 0;
    RaySphere[] sceneSpheres;
    RayQuad[] sceneQuades;
    RayLight[] sceneLights;
    RayQuad[] sceneQuadLights;

    RenderTexture tempRenderTex;

    void Awake()
    {
        FindSphereMeshes();
        FindQuadMeshes();

        FindSphereLights();
        FindQuadLights();

        FindMeshTriangles();

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
        int totalMeshCount = Mathf.Max(meshTriangleList.Count, 1);

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

        int triangleCount = 0;
        foreach(var mesh in meshTriangleList)
        {
            triangleCount += mesh.TriangleCount;
        }
        int triangleStride = System.Runtime.InteropServices.Marshal.SizeOf(typeof(Triangle));
        triangleListBuffer = new ComputeBuffer(Mathf.Max(triangleCount, 1), triangleStride);

        int meshStride = System.Runtime.InteropServices.Marshal.SizeOf(typeof(MeshInfo));
        meshListBuffer = new ComputeBuffer(totalMeshCount, meshStride);
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

    void FindMeshTriangles()
    {
        var meshTriangles = FindObjectsByType<MeshTriangle>();
        meshTriangleList = meshTriangles.ToList();
    }

    private void OnDestroy()
    {
        sphereMeshBuffers.Release();
        quadMeshBuffer.Release();
        sceneLightBuffers.Release();
        sceneQuadLightBuffer.Release();
        triangleListBuffer.Release();
        meshListBuffer.Release();
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
        rayTracerCS.SetInt("ApplySkyBoxColor", applySkyboxColor ? 1 : 0);
        rayTracerCS.SetFloat("SkyColorApplyRatio", skyColorApplyRatio);

        UpdateSphereComputeBuffers();
        UpdateQuadComputeBuffers();
        UpdateSceneLight();
        UpdateQuadLightBuffer();
        UpdateTriangleBuffer();

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
        colorAccuCS.SetFloat("deltaTime", Time.deltaTime);
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
            sceneLights[0].material = Material.ZeroMat();
        }else
        {
            for(int i = 0; i < totalSceneLights; i++)
            {
                RayTraceLight qLight = rayTraceLights[i];
                sceneLights[i].position = qLight.Position;
                sceneLights[i].radius = qLight.Radius;
                // color drives the diffuse lighting, emissive is the color of the
                // visible light sphere
                Material material = new Material();
                material.matType = 0;
                material.albedo = new float3(qLight.Color.r, qLight.Color.g, qLight.Color.b);
                material.emissive = new float3(qLight.Emissive.r, qLight.Emissive.g, qLight.Emissive.b);
                material.emissive = qLight.emissiveStrength;
                material.emissive *= qLight.EmissiveMultiplier;
                material.reflectionFuzz = qLight.ReflectionFuzz;
                material.RefractionIndex = qLight.RefractionIndex;
                material.pad0 = float2.zero;
                sceneLights[i].material = material;
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
            sceneQuadLights[0].material = Material.ZeroMat();
        }else
        {
            for(int i = 0; i < totalSceneLights; i++)
            {
                QuadLight qLight = rayQuadLights[i];
                sceneQuadLights[i] = GetQuad(qLight.transform, qLight.MaterialType, qLight.Color, qLight.Emission, qLight.EmissionStrength, qLight.ReflectionFuzz, qLight.RefractionIndex);
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
            sceneSpheres[0].material = Material.ZeroMat();
        }else
        {
            for(int i = 0; i < totalItems; i++)
            {
                SphereMesh sphMesh = sceneSphereMeshes[i];
                sceneSpheres[i].position = sphMesh.GetPosition();
                sceneSpheres[i].radius = sphMesh.GetRadius();
                Material material = new Material();
                material.matType = (int)sphMesh.MaterialType;
                material.albedo = new float3(sphMesh.Color.r, sphMesh.Color.g, sphMesh.Color.b);
                material.emissive = new float3(sphMesh.Emission.r, sphMesh.Emission.g, sphMesh.Emission.b);
                material.emissiveStrength = sphMesh.EmissionStrength;
                material.reflectionFuzz = sphMesh.ReflectionFuzz;
                material.RefractionIndex = sphMesh.RefractionIndex;
                material.pad0 = float2.zero;
                sceneSpheres[i].material = material;
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
            sceneQuades[0].material = Material.ZeroMat();
        }else
        {
            for(int i = 0; i < totalItems; i++)
            {
                QuadMesh quadMesh = sceneQuadMeshes[i];
                sceneQuades[i] = GetQuad(quadMesh.transform, quadMesh.MaterialType, quadMesh.Color, Color.black, 0, quadMesh.ReflectionFuzz, quadMesh.RefractionIndex);
            }
        }
        quadMeshBuffer.SetData(sceneQuades);
        rayTracerCS.SetBuffer(kernalHandle, "quadMeshes", quadMeshBuffer);
        rayTracerCS.SetInt("quadMeshCount", totalItems);
    }

    RayQuad GetQuad(Transform t, MaterialType matType, Color color, Color emission, float emissiveStrength, float reflectionFuzz, float RefractionIndex)
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
        Material material = new Material();
        material.matType = (int)matType;
        material.albedo = new float3(color.r, color.g, color.b);
        material.emissive = new float3(emission.r, emission.g, emission.b);
        material.emissiveStrength = emissiveStrength;
        material.reflectionFuzz = reflectionFuzz;
        material.RefractionIndex = RefractionIndex;
        material.pad0 = float2.zero;
        quad.material = material;
        return quad;
    }

    void UpdateTriangleBuffer()
    {
        int totalTriangleCount = 0;
        int totalMeshCount = meshTriangleList.Count;
        if(totalMeshCount <= 0)
        {
            Triangle[] triList = new Triangle[1];
            triList[0].v0 = float3.zero;
            triList[0].v1 = float3.zero;
            triList[0].v2 = float3.zero;
            triList[0].n0 = float3.zero;
            triList[0].n1 = float3.zero;
            triList[0].n2 = float3.zero;
            triangleListBuffer.SetData(triList);

            MeshInfo[] meshInfo = new MeshInfo[1];
            meshInfo[0].triIndexStart = 0;
            meshInfo[0].triCount = 0;
            meshInfo[0].boundMin = float3.zero;
            meshInfo[0].boundMax = float3.zero;
            meshInfo[0].material = Material.ZeroMat();
            meshListBuffer.SetData(meshInfo);
        }else
        {
            int triStartIndex = 0;
            MeshInfo[] meshInfoList = new MeshInfo[totalMeshCount];
            List<Triangle> trianglList = new List<Triangle>();
            for(int i = 0; i < totalMeshCount; i++)
            {
                MeshTriangle mesh = meshTriangleList[i];
                MeshInfo meshInfo = new MeshInfo();
                meshInfo.triIndexStart = (uint)triStartIndex;
                List<Triangle> triangles = mesh.GetTriangleList();
                trianglList.AddRange(triangles);

                meshInfo.triCount = (uint)triangles.Count;
                float3 minBound, maxBound;
                mesh.GetMinMaxBounds(out minBound, out maxBound);

                meshInfo.boundMin = minBound;
                meshInfo.boundMax = maxBound;

                Material material = new Material();
                material.matType = (int)mesh.MaterialType;
                material.albedo = new float3(mesh.Color.r, mesh.Color.g, mesh.Color.b);
                material.emissive = new float3(mesh.Emission.r, mesh.Emission.g, mesh.Emission.b);
                material.emissiveStrength = mesh.EmissionStrength;
                material.reflectionFuzz = mesh.ReflectionFuzz;
                material.RefractionIndex = mesh.RefractionIndex;
                material.pad0 = float2.zero;
                meshInfo.material = material;
                meshInfoList[i] = meshInfo;
                triStartIndex += triangles.Count;
            }

            totalTriangleCount = trianglList.Count;
            triangleListBuffer.SetData(trianglList.ToArray());
            meshListBuffer.SetData(meshInfoList);
        }

        rayTracerCS.SetBuffer(kernalHandle, "triangleList", triangleListBuffer);
        rayTracerCS.SetInt("totalTriangleCount", totalTriangleCount);
        rayTracerCS.SetInt("totalMeshCount", totalMeshCount);
        rayTracerCS.SetBuffer(kernalHandle, "meshList", meshListBuffer);
    }
}
