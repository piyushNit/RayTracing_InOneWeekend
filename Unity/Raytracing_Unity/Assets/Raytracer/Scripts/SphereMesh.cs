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
    public RaytraceMaterial rayTraceMaterial;
    public float GetRadius(){ return transform.localScale.x * 0.5f; }
    public float3 GetPosition(){ return transform.position; }
}
