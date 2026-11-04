using Unity.Mathematics;
using UnityEngine;

[System.Serializable]
public class RaytraceMaterial
{
    public MaterialType MaterialType;
    public Color Color = Color.white;
    public Color Emission = Color.white;
    public float EmissionStrength = 1;
    [Header("Metal")]
    [Range(0.0f, 1.0f)]
    public float ReflectionFuzz = 0.5f;
    [Header("Dielectric")]
    public float RefractionIndex = 0.8f;

    public RayTraceMaterial RayTraceMaterial {
        get {
            RayTraceMaterial material = new RayTraceMaterial();
            material.matType = (int)MaterialType;
            material.albedo = new float3(Color.r, Color.g, Color.b);
            material.emissive = new float3(Emission.r, Emission.g, Emission.b);
            material.emissiveStrength = EmissionStrength;
            material.reflectionFuzz = ReflectionFuzz;
            material.RefractionIndex = RefractionIndex;
            material.pad0 = float2.zero;
            return material;
        }
    }
}
