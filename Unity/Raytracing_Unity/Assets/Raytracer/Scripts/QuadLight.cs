using Unity.Mathematics;
using UnityEngine;

public class QuadLight : MonoBehaviour
{
    public RaytraceMaterial rayTraceMaterial;
    public float GetRadius(){ return transform.localScale.x * 0.5f; }
    public float3 GetPosition(){ return transform.position; }
}
