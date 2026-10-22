using UnityEngine;

public class RayTraceLight : MonoBehaviour
{
    public float EmissiveMultiplier = 20;
    public Vector3 Position { get=>transform.position; }
    public float Radius = 5;
    public Color Color = Color.white;
    public Color Emissive = Color.white;
    public float emissiveStrength = 1;
}
