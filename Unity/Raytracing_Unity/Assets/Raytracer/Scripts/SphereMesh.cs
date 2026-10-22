using Unity.Mathematics;
using UnityEngine;

public class SphereMesh : MonoBehaviour
{
    public Color Color = Color.white;
    public Color Emission = Color.white;
    public float EmissionStrength = 1;
    public float GetRadius(){ return transform.localScale.x; }
    public float3 GetPosition(){ return transform.position; }
}
