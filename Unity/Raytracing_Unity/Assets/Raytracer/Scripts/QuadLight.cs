using Unity.Mathematics;
using UnityEngine;

public class QuadLight : MonoBehaviour
{
    public MaterialType MaterialType;
    public Color Color = Color.white;
    public Color Emission = Color.white;
    public float EmissionStrength = 1;
    public float GetRadius(){ return transform.localScale.x * 0.5f; }
    public float3 GetPosition(){ return transform.position; }
}
