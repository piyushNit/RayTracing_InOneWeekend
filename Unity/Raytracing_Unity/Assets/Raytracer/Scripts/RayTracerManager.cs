using System.Collections.Generic;
using System.Linq;
using UnityEngine;

struct Sphere
{
    public Vector3 position;
    public float radius;
};

public class RayTracerManager : MonoBehaviour
{
    public RenderTexture renderTarget;
    public ComputeShader rayTracerCS;

    private List<SphereMesh> sceneSphereMeshes;
    private ComputeBuffer sphereMeshBuffers = null;

    int kernalHandle = -1;

    RayTracerCamera rayTracerCamera;

    void Awake()
    {
        var sphereMeshes = FindObjectsByType<SphereMesh>();
        sceneSphereMeshes = sphereMeshes.ToList();
    
        rayTracerCamera = Camera.main.GetComponent<RayTracerCamera>();
    }

    void Update()
    {
        if(renderTarget == null || rayTracerCS == null)
            return;
        
        if(kernalHandle == -1)
            kernalHandle = rayTracerCS.FindKernel("CSMain");

        CreateTextureIfRequired();
        DispatchComputeShader();
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

        UpdateComputeBuffers();

        int dispatchX = Mathf.CeilToInt((float)renderTarget.width / 8.0f);
        int dispatchY = Mathf.CeilToInt((float)renderTarget.height / 8.0f);
        rayTracerCS.Dispatch(kernalHandle, dispatchX, dispatchY, 1);
    }

    void UpdateComputeBuffers()
    {
        int totalItems = sceneSphereMeshes.Count;
        Sphere[] spheres = new Sphere[totalItems];
        for(int i = 0; i < totalItems; i++)
        {
            SphereMesh sphMesh = sceneSphereMeshes[i];
            spheres[i].position = sphMesh.GetPosition();
            spheres[i].radius = sphMesh.GetRadius();
        }

        int stride = System.Runtime.InteropServices.Marshal.SizeOf(typeof(Sphere)); 
        sphereMeshBuffers = new ComputeBuffer(totalItems, stride);
        sphereMeshBuffers.SetData(spheres);

        rayTracerCS.SetBuffer(kernalHandle, "sphereMeshes", sphereMeshBuffers);
        rayTracerCS.SetInt("sphereMeshCount", totalItems);
    }
}
