using Unity.Mathematics;
using UnityEngine;

public enum MaterialType
{
    Diffuse = 0,
    Metal,
    Dielectric
}

public class SphereMesh : MonoBehaviour
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
    public float GetRadius(){ return transform.localScale.x * 0.5f; }
    public float3 GetPosition(){ return transform.position; }
}
