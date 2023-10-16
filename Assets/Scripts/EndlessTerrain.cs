using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EndlessTerrain : MonoBehaviour
{
    const float scale = 5f;
    const float viewerMoveThreadHoldForChunckUpdate = 25f;
    const float sqrViewerMoveThreadHoldForChunckUpdate = viewerMoveThreadHoldForChunckUpdate * viewerMoveThreadHoldForChunckUpdate;

    public static float maxViewDist;
    [Header("Details Level")]
    public LODInfo[] detailsLevels;

    [Header("Variable Endless Terrain")]
    public Transform viewer;
    public Material mapMaterial;

    public static Vector2 viewerPosition;
    Vector2 viewerPositionOld;
    static MapGenerator mapGenerator;
    int chunkSize;
    int chunksVisibleInDist;

    Dictionary<Vector2, TerrainChunk> terrainChunkDictionnary = new Dictionary<Vector2, TerrainChunk>();
    static List<TerrainChunk> terrainChunksVisibleLastUpdate = new List<TerrainChunk>();

    private void Start()
    {
        mapGenerator = FindObjectOfType<MapGenerator>();
        maxViewDist = detailsLevels[detailsLevels.Length - 1].visibleDistThreadOld;
        chunkSize = MapGenerator.mapChunkSize - 1;
        chunksVisibleInDist = Mathf.RoundToInt(maxViewDist / chunkSize);
        UpdateVisibleChunks();
    }

    private void Update()
    {
        viewerPosition = new Vector2(viewer.position.x, viewer.position.z) / 2f;

        if ((viewerPositionOld - viewerPosition).sqrMagnitude > sqrViewerMoveThreadHoldForChunckUpdate)
        {
            viewerPositionOld = viewerPosition;
            UpdateVisibleChunks();
        }
    }

    void UpdateVisibleChunks()
    {
        for (int i = 0; i < terrainChunksVisibleLastUpdate.Count; i++)
        {
            terrainChunksVisibleLastUpdate[i].SetVisible(false);
        }
        terrainChunksVisibleLastUpdate.Clear();

        int currentChunkCoordX = Mathf.RoundToInt(viewerPosition.x / chunkSize);
        int currentChunkCoordY = Mathf.RoundToInt(viewerPosition.y / chunkSize);

        for (int yOffset = -chunksVisibleInDist; yOffset <= chunksVisibleInDist; yOffset++)
        {
            for (int xOffset = -chunksVisibleInDist; xOffset <= chunksVisibleInDist; xOffset++)
            {
                Vector2 viewedChunkCoord = new Vector2(currentChunkCoordX + xOffset, currentChunkCoordY + yOffset);

                if (terrainChunkDictionnary.ContainsKey(viewedChunkCoord))
                {
                    terrainChunkDictionnary[viewedChunkCoord].UpdateTerrainChunk();
                }
                else
                {
                    terrainChunkDictionnary.Add(viewedChunkCoord, new TerrainChunk(viewedChunkCoord, chunkSize, detailsLevels, transform, mapMaterial));
                }
            }
        }
    }

    public class TerrainChunk
    {
        GameObject meshObject;
        Vector2 position;
        Bounds bounds;

        MapData mapData;
        MeshRenderer meshRenderer;
        MeshFilter meshFilter;

        LODInfo[] detailsLevels;
        LODMesh[] lODMeshes;

        bool mapDataReceived;
        int previousLODIndex = -1;

        public TerrainChunk(Vector2 coord, int size, LODInfo[] detailsLevel, Transform parent, Material material)
        {
            this.detailsLevels = detailsLevel;

            position = coord * size;
            bounds = new Bounds(position, Vector2.one * size);

            Vector3 positionV3 = new Vector3(position.x, 0, position.y);

            meshObject = new GameObject("Terrain Chunk");
            meshRenderer = meshObject.AddComponent<MeshRenderer>();
            meshFilter = meshObject.AddComponent<MeshFilter>();
            meshRenderer.material = material;

            meshObject.transform.position = positionV3 * scale;
            meshObject.transform.parent = parent;
            meshObject.transform.localScale = Vector3.one * scale;
            SetVisible(false);

            lODMeshes = new LODMesh[detailsLevel.Length];

            for (int i = 0; i < detailsLevel.Length; i++)
            {
                lODMeshes[i] = new LODMesh(detailsLevel[i].lod, UpdateTerrainChunk);
            }

            mapGenerator.RequestMapData(position, OnMapDataReceived);
        }

        void OnMapDataReceived(MapData mapData)
        {
            this.mapData = mapData;
            mapDataReceived = true;

            Texture2D texture = TextureGenerator.TextureFromColorMap(mapData.colorMap, MapGenerator.mapChunkSize, MapGenerator.mapChunkSize);
            meshRenderer.material.mainTexture = texture;

            UpdateTerrainChunk();
        }


        public void UpdateTerrainChunk()
        {
            if (mapDataReceived)
            {
                float viewerDistFromNearestEdge = Mathf.Sqrt(bounds.SqrDistance(viewerPosition));
                bool isVisible = viewerDistFromNearestEdge <= maxViewDist;

                if (isVisible)
                {
                    int lodIndex = 0;

                    for (int i = 0; i < detailsLevels.Length - 1; i++)
                    {
                        if (viewerDistFromNearestEdge > detailsLevels[i].visibleDistThreadOld)
                        {
                            lodIndex = i + 1;
                        }
                        else
                        {
                            break;
                        }
                    }

                    if (lodIndex != previousLODIndex)
                    {
                        LODMesh lODMesh = lODMeshes[lodIndex];
                        if (lODMesh.hasMesh)
                        {
                            previousLODIndex = lodIndex;
                            meshFilter.mesh = lODMesh.mesh;
                        }
                        else if (!lODMesh.hasRequestedMesh)
                        {
                            lODMesh.RequestMesh(mapData);
                        }
                    }
                        terrainChunksVisibleLastUpdate.Add(this);
                }

                SetVisible(isVisible);
            }
        }

        public void SetVisible(bool isVisible)
        {
            meshObject.SetActive(isVisible);
        }

        public bool IsVisible()
        {
            return meshObject.activeSelf;
        }
    }
    class LODMesh
    {
        public Mesh mesh;
        public bool hasRequestedMesh;
        public bool hasMesh;
        int lod;
        System.Action updateCallBack;

        public LODMesh(int lod, System.Action updateCallBack)
        {
            this.lod = lod;
            this.updateCallBack = updateCallBack;
        }

        void OnMeshDataReceived(MeshData meshData)
        {
            mesh = meshData.CreateMesh();
            hasMesh = true;
            updateCallBack();
        }

        public void RequestMesh(MapData mapData)
        {
            hasRequestedMesh = true;
            mapGenerator.RequestMeshData(mapData, lod, OnMeshDataReceived);
        }
    }

    [System.Serializable]
    public struct LODInfo
    {
        public int lod;
        public float visibleDistThreadOld;
    }
}