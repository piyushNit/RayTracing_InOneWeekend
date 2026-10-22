using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using UnityEngine;

public class MeshTriangle : MonoBehaviour
{
    public MaterialType MaterialType;
    public Color Color = Color.white;
    public Color Emission = Color.white;
    public float EmissionStrength = 1;
    public float GetRadius(){ return transform.localScale.x * 0.5f; }
    public float3 GetPosition(){ return transform.position; }

    MeshFilter meshFilter;

    void Awake()
    {
        FindMeshFilter();
    }

    void FindMeshFilter()
    {
        if(meshFilter != null)
            return;
        meshFilter = GetComponent<MeshFilter>();
    }

    public int TriangleCount {
        get
        {
            FindMeshFilter();
            if(meshFilter == null) return 0;
            return meshFilter.mesh.triangles.Length / 3;
        }
    }

    public List<Triangle> GetTriangleList()
    {
        List<Triangle> triList = new List<Triangle>();
        Mesh mesh = meshFilter.mesh;
        int[] triangles = mesh.triangles;
        Vector3[] vertices = mesh.vertices;
        Vector3[] normals = mesh.normals;

        Matrix4x4 localToWorld = meshFilter.transform.localToWorldMatrix;
        Matrix4x4 normalMatrix = localToWorld.inverse.transpose;

        for(int i = 0; i < mesh.triangles.Length; i += 3)
        {
            int i0 = triangles[i];
            int i1 = triangles[i + 1];
            int i2 = triangles[i + 2];

            Triangle tri = new Triangle();
            tri.v0 = localToWorld.MultiplyPoint3x4(vertices[i0]);
            tri.v1 = localToWorld.MultiplyPoint3x4(vertices[i1]);
            tri.v2 = localToWorld.MultiplyPoint3x4(vertices[i2]);
            tri.n0 = normalMatrix.MultiplyVector(normals[i0]);
            tri.n1 = normalMatrix.MultiplyVector(normals[i1]);
            tri.n2 = normalMatrix.MultiplyVector(normals[i2]);
            triList.Add(tri);
        }
        return triList;
    }

    public void GetMinMaxBounds(out float3 minBound, out float3 maxBound)
    {
        Renderer renderer = gameObject.GetComponent<Renderer>();
        minBound = renderer.bounds.min;
        maxBound = renderer.bounds.max;
    }
}
