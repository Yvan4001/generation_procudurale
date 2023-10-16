using System;
using System.Threading;
using System.Collections.Generic;
using UnityEngine;

public class MapGenerator : MonoBehaviour
{

    public enum DrawnMode { NoiseMap, ColourMap, Mesh, FallOffMap }
    public DrawnMode drawnMode;

    public Noise.NormalizeMode normalizedMode;



    [HideInInspector] public const int mapChunkSize = 241;

    [Header("Map Generator Variable")]
    [Range(0, 6)]
    public int editorPreviewLod;

    public float noiseScale;

    public int octaves;

    [Range(0, 1)]
    public float persistance;
    public float lacunarity;

    public Vector2 offset;

    [Header("Use FalloffMap ?")]
    public bool useFallOff;

    [Header("Seed")]
    public int seed;

    [Header("Is AutoGenerate ?")]
    public bool autoUpdate;

    [Header("Generation Mesh Variable")]
    public float meshHeightMultiplayer;
    public AnimationCurve meshHeightCurve;

    [Header("Terrain Type")]
    public TerrainType[] regions;

    float[,] fallOffMap;

    Queue<MapThreadInfo<MapData>> mapDataThreadInfoQueue = new Queue<MapThreadInfo<MapData>>();
    Queue<MapThreadInfo<MeshData>> meshDataThreadInfoQueue = new Queue<MapThreadInfo<MeshData>>();

    private void Awake()
    {
        fallOffMap = FallOffGenerator.GenerateFallOfMap(mapChunkSize);
    }


    public void DrawMapInEditor()
    {
        MapDisplay mapDisplay = FindObjectOfType<MapDisplay>();
        MapData mapData = GenerateMapData(Vector2.zero);

        if (drawnMode == DrawnMode.NoiseMap)
        {
            mapDisplay.DrawnTexture(TextureGenerator.TextureFromHeightMap(mapData.heightMap));
        }
        else if (drawnMode == DrawnMode.ColourMap)
        {
            mapDisplay.DrawnTexture(TextureGenerator.TextureFromColorMap(mapData.colorMap, mapChunkSize, mapChunkSize));
        }
        else if (drawnMode == DrawnMode.Mesh)
        {
            mapDisplay.DrawnMesh(MeshGenerator.GenerateTerrainMesh(mapData.heightMap, meshHeightMultiplayer, meshHeightCurve, editorPreviewLod), TextureGenerator.TextureFromColorMap(mapData.colorMap, mapChunkSize, mapChunkSize));
        }
        else if (drawnMode == DrawnMode.FallOffMap)
        {
            mapDisplay.DrawnTexture(TextureGenerator.TextureFromHeightMap(FallOffGenerator.GenerateFallOfMap(mapChunkSize)));
        }
    }

    public void RequestMapData(Vector2 center, Action<MapData> callback)
    {
        ThreadStart threadStart = delegate { MapDataThread(center, callback); };

        new Thread(threadStart).Start();
    }

    public void MapDataThread(Vector2 center, Action<MapData> callback)
    {
        MapData mapData = GenerateMapData(center);
        lock (mapDataThreadInfoQueue)
        {
            mapDataThreadInfoQueue.Enqueue(new MapThreadInfo<MapData>(callback, mapData));
        }
    }

    public void RequestMeshData(MapData mapData, int lod, Action<MeshData> callback)
    {
        ThreadStart threadStart = delegate { MeshDataThread(mapData, lod, callback); };

        new Thread(threadStart).Start();
    }

    void MeshDataThread(MapData mapData, int lod, Action<MeshData> callback)
    {
        MeshData meshData = MeshGenerator.GenerateTerrainMesh(mapData.heightMap, meshHeightMultiplayer, meshHeightCurve, lod);
        lock (meshDataThreadInfoQueue)
        {
            meshDataThreadInfoQueue.Enqueue(new MapThreadInfo<MeshData>(callback, meshData));
        }
    }

    private void Update()
    {
        if (mapDataThreadInfoQueue.Count > 0)
        {
            for (int i = 0; i < mapDataThreadInfoQueue.Count; i++)
            {
                MapThreadInfo<MapData> threadInfo = mapDataThreadInfoQueue.Dequeue();
                threadInfo.callback(threadInfo.parameter);
            }
        }

        if (meshDataThreadInfoQueue.Count > 0)
        {
            for (int i = 0; i < meshDataThreadInfoQueue.Count; i++)
            {
                MapThreadInfo<MeshData> threadInfo = meshDataThreadInfoQueue.Dequeue();
                threadInfo.callback(threadInfo.parameter);
            }
        }
    }

    MapData GenerateMapData(Vector2 center)
    {
        float[,] noiseMap = Noise.GenerateNoiseMap(mapChunkSize, mapChunkSize, seed, noiseScale, octaves, persistance, lacunarity, center + offset, normalizedMode);

        Color[] colorMap = new Color[mapChunkSize * mapChunkSize];
        for (int y = 0; y < mapChunkSize; y++)
        {
            for (int x = 0; x < mapChunkSize; x++)
            {
                if (useFallOff)
                {
                    noiseMap[x, y] = Mathf.Clamp01(noiseMap[x, y] - fallOffMap[x, y]);
                }

                float currentHeight = noiseMap[x, y];

                for (int i = 0; i < regions.Length; i++)
                {
                    if (currentHeight >= regions[i].height)
                    {
                        colorMap[y * mapChunkSize + x] = regions[i].color;
                    }
                    else
                    {
                        break;
                    }
                }
            }
        }

        return new MapData(noiseMap, colorMap);
    }

    private void OnValidate()
    {
        if (lacunarity < 1)
        {
            lacunarity = 1;
        }
        if (octaves < 0)
        {
            octaves = 0;
        }

        fallOffMap = FallOffGenerator.GenerateFallOfMap(mapChunkSize);
    }

    struct MapThreadInfo<T>
    {
        public readonly Action<T> callback;
        public readonly T parameter;

        public MapThreadInfo(Action<T> callback, T parameter)
        {
            this.callback = callback;
            this.parameter = parameter;
        }
    }
}

[System.Serializable]
public struct TerrainType
{
    public string name;
    public float height;

    public Color color;
}

public struct MapData
{
    public readonly float[,] heightMap;
    public readonly Color[] colorMap;

    public MapData(float[,] heightMap, Color[] colorMap)
    {
        this.heightMap = heightMap;
        this.colorMap = colorMap;
    }
}