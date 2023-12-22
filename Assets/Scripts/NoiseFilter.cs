using UnityEngine;
public enum NoiseType
{
    [InspectorName("Perlin")]
    Perlin = 0,
    [InspectorName("Vorley")]
    Vorley = 1
}
public enum FilterType
{
    [InspectorName("Simple")]
    Simple = 0,
    [InspectorName("Ridge")]
    Ridge = 1,
    [InspectorName("Mask")]
    Mask = 2
}
[System.Serializable]
public struct NoiseFilter
{
    public NoiseType noiseType;
    public FilterType filterType;
    public Vector3 offset;
    public float scale;
    public float amplitude;
    public float lacunarity;
    public float persistance;
    [Range(1, 12)] public int octaves;
    public bool exclude;
};