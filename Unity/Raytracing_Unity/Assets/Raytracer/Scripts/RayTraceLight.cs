using UnityEngine;

[RequireComponent(typeof(Light))]
public class RayTraceLight : MonoBehaviour
{
    Color color;
    public float EmissiveMultiplier = 20;
    public Vector3 Position { get=>transform.position; }
    public float Radius = 5;
    public Color Color { get=>color; }

    Light light;

    void Awake()
    {
        light = GetComponent<Light>();
        color = light.color;
    }

    void Update()
    {
        color = light.color;
    }
}
