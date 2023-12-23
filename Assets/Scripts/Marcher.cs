using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
[RequireComponent(typeof(MeshFilter))]
public class Marcher : MonoBehaviour
{
    [Header("Noise")]
    [Range(2, 256)] public int baseResolution = 64;
    public NoiseFilter[] noiseFilters = new NoiseFilter[1];
    public float radious = 500f;
    public float distFallof = 1;

    [Header("Marching")]
    public float isoLevel = 0.5f;
    [Range(0f, 0.5f)] public float threashold = 0.05f;
    public bool interpolate = true;
    public bool run = false;

    private ComputeShader noiseCompute;
    private ComputeShader marchingCompute;
    private RenderTexture volumetricData;
    private void Start()
    {
        noiseCompute = (ComputeShader)Resources.Load("Noise", typeof(ComputeShader));
        marchingCompute = (ComputeShader)Resources.Load("MarchingCubes", typeof(ComputeShader));
        GetComponent<MeshFilter>().sharedMesh = GenerateMesh(baseResolution);

        Benchmarker.Test("PersistantVolumetricData.txt", new Mark[]
        {
            new (() => GenerateMesh(baseResolution), 5),
            new (() => GenerateMesh(baseResolution / 2), 10),
            new (() => GenerateMesh(baseResolution / 4), 15),
            new (() => GenerateMesh(baseResolution / 8), 20),
        });
    }
    private void Update()
    {
        if (run == false) return;

        noiseCompute = (ComputeShader)Resources.Load("Noise", typeof(ComputeShader));
        marchingCompute = (ComputeShader)Resources.Load("MarchingCubes", typeof(ComputeShader));
        GetComponent<MeshFilter>().sharedMesh = GenerateMesh(baseResolution);
    }
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.gray;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
    }
    private Mesh GenerateMesh(int resolution)
    {
        // Generate Noise.
        RenderTexture volumetricData = GenerateNoise(Vector3.zero, Vector3.one, resolution);

        int vertexCount = CountVertices(volumetricData);
        if (vertexCount == 0)
        {
            volumetricData.Release();
            return new Mesh();
        }

        Vector3[] vertices = new Vector3[vertexCount];
        Vector3[] normals = new Vector3[vertexCount];
        int[] indices = new int[vertexCount];
        GenerateTriangles(ref vertices, ref normals, ref indices, volumetricData);
        volumetricData.Release();

        Mesh mesh = new()
        {
            indexFormat = IndexFormat.UInt32,
            vertices = vertices,
            normals = normals,
            triangles = indices
        };

        return mesh;
    }
    private RenderTexture GenerateNoise(Vector3 position, Vector3 scale, int resolution)
    {
        if (volumetricData == null || volumetricData.width != resolution)
        {
            RenderTextureDescriptor desc = new(resolution, resolution, RenderTextureFormat.RFloat)
            {
                enableRandomWrite = true,
                dimension = TextureDimension.Tex3D,
                autoGenerateMips = false
            };
            volumetricData = new(desc) { volumeDepth = resolution };
            volumetricData.Create();
        }

        // Generate noise values on GPU.
        int dispachSize = (int)Mathf.Ceil(resolution / 8f);
        noiseCompute.SetInt("resolution", resolution);
        noiseCompute.SetFloat("value", 0f);
        noiseCompute.SetTexture(0, "noiseValues", volumetricData);
        noiseCompute.Dispatch(0, dispachSize, dispachSize, dispachSize);
        noiseCompute.SetVector("chunkPosition", position - Vector3.one * (scale.x * 0.5f));
        noiseCompute.SetVector("chunkScale", scale);
        for (int i = 0; i < noiseFilters.Length; i++)
        {
            if (noiseFilters[i].exclude) continue;
            int kernelID = ((int)noiseFilters[i].noiseType) * 3 + ((int)noiseFilters[i].filterType) + 2;

            noiseCompute.SetVector("offset", noiseFilters[i].offset);
            noiseCompute.SetFloat("scale", noiseFilters[i].scale * 0.01f);
            noiseCompute.SetFloat("amplitude", noiseFilters[i].amplitude);
            noiseCompute.SetFloat("lacunarity", noiseFilters[i].lacunarity);
            noiseCompute.SetFloat("persistance", noiseFilters[i].persistance);
            noiseCompute.SetInt("octaves", noiseFilters[i].octaves);
            noiseCompute.SetTexture(kernelID, "noiseValues", volumetricData);

            noiseCompute.Dispatch(kernelID, dispachSize, dispachSize, dispachSize);
        }
        noiseCompute.SetFloat("radious", radious);
        noiseCompute.SetFloat("distFallof", distFallof);
        noiseCompute.SetTexture(1, "noiseValues", volumetricData);
        noiseCompute.Dispatch(1, dispachSize, dispachSize, dispachSize);

        return volumetricData;
    }
    private int CountVertices(RenderTexture volumetricData)
    {
        int resolution = volumetricData.width;
        int dispachSize = (int)Mathf.Ceil((resolution - 1) / 8f);

        ComputeBuffer counterBuffer = new(1, sizeof(int), ComputeBufferType.Structured);
        counterBuffer.SetData(new int[1] { 0 });

        marchingCompute.SetInt("resolution", resolution);
        marchingCompute.SetFloat("isoLevel", isoLevel);
        marchingCompute.SetFloat("threashold", threashold);
        marchingCompute.SetBool("shouldInterpolate", interpolate);
        marchingCompute.SetTexture(1, "volumetricData", volumetricData);
        marchingCompute.SetBuffer(1, "counterBuffer", counterBuffer);
        marchingCompute.Dispatch(1, dispachSize, dispachSize, dispachSize);

        int[] c = new int[1];
        counterBuffer.GetData(c);
        counterBuffer.Release();
        return c[0];
    }
    private void GenerateTriangles(ref Vector3[] vertices, ref Vector3[] normals, ref int[] indices, RenderTexture volumetricData)
    {
        int resolution = volumetricData.width;
        int dispachSize = (int)Mathf.Ceil((resolution - 1) / 4f);
        ComputeBuffer vertexBuffer = new(vertices.Length, sizeof(float) * 3, ComputeBufferType.Structured);
        ComputeBuffer normalBuffer = new(normals.Length, sizeof(float) * 3, ComputeBufferType.Structured);
        ComputeBuffer counterBuffer = new(1, sizeof(int), ComputeBufferType.Structured);
        counterBuffer.SetData(new int[1] { 0 });

        marchingCompute.SetInt("resolution", resolution);
        marchingCompute.SetFloat("isoLevel", isoLevel);
        marchingCompute.SetFloat("threashold", threashold);
        marchingCompute.SetBool("shouldInterpolate", interpolate);
        marchingCompute.SetTexture(0, "volumetricData", volumetricData);
        marchingCompute.SetBuffer(0, "vertices", vertexBuffer);
        marchingCompute.SetBuffer(0, "normals", normalBuffer);
        marchingCompute.SetBuffer(0, "counterBuffer", counterBuffer);

        marchingCompute.Dispatch(0, dispachSize, dispachSize, dispachSize);

        for (int i = 0; i < indices.Length; i++) indices[i] = i;
        vertexBuffer.GetData(vertices);
        normalBuffer.GetData(normals);
        vertexBuffer.Release();
        normalBuffer.Release();
        counterBuffer.Release();

        int deletedCount = 0;
        Dictionary<Vector3, int> uniqueVertices = new(vertices.Length);
        List<Vector3> uniqueNormals = new(normals.Length);
        for (int i = 0; i < vertices.Length; i++)
        {
            if (uniqueVertices.ContainsKey(vertices[i]))
            {
                indices[i] = uniqueVertices[vertices[i]];
                deletedCount++;
            }
            else
            {
                indices[i] = indices[i] - deletedCount;
                uniqueVertices.Add(vertices[i], indices[i]);
                uniqueNormals.Add(normals[i]);
            }
        }
        vertices = uniqueVertices.Keys.ToArray();
        normals = uniqueNormals.ToArray();
    }
}