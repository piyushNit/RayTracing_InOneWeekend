using UnityEngine;

public class RayTracerManager : MonoBehaviour
{
    public RenderTexture renderTarget;
    public ComputeShader rayTracerCS;

    int kernalHandle = -1;

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
        if(kernalHandle == -1)
            return;

        rayTracerCS.SetTexture(kernalHandle, "Result", renderTarget);
        int dispatchX = Mathf.CeilToInt((float)renderTarget.width / 8.0f);
        int dispatchY = Mathf.CeilToInt((float)renderTarget.height / 8.0f);
        rayTracerCS.Dispatch(kernalHandle, dispatchX, dispatchY, 1);
    }
}
