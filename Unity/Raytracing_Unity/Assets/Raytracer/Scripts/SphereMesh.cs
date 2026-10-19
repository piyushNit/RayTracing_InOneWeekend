using Unity.Mathematics;
using UnityEngine;

public class SphereMesh : MonoBehaviour
{
    public float GetRadius(){ return transform.localScale.x; }
    public float3 GetPosition(){ return transform.position; }
}
