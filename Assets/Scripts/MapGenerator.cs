using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using Random = UnityEngine.Random;

public class MapGenerator : NetworkBehaviour {
    public static MapGenerator Instance;

    public NetworkVariable<int> Seed;

    [SerializeField] private Vector2Int mapSize;
    [SerializeField] private Vector2Int allowedDistanceToBorders;
    [SerializeField] private float allowedDistanceToBuildings;
    [SerializeField] private float bushAllowedDistanceToBuildings;
    [SerializeField] private int minBuildings;
    [SerializeField] private int maxBuildings;
    [SerializeField] private int iterationFailSafe;
    [SerializeField] private GroundTile BushTile;
    [SerializeField] private GroundTile[] groundTiles;
    [SerializeField] private Transform[] buildingPrefabs;

    public Vector2Int MapSize => mapSize;

    private readonly List<Transform> _spawnedBuildings = new();
    private Transform _groundTilesHolder;

    public override void OnNetworkSpawn() {
        if (IsServer) Seed.Value = Random.Range(0, 1000000);
        else Seed.OnValueChanged += (oldValue, newValue) => Random.InitState(newValue);

        Random.InitState(Seed.Value);
    }

    private void Awake() {
        if (Instance != null && Instance != this) Destroy(Instance.gameObject);
        Instance = this;
    }

    private void Update() {
        if (Keyboard.current.kKey.wasPressedThisFrame) GenerateBuildings();
    }

    public void GenerateMap() {
        RemoveAllMap();

        GenerateGround();
        GenerateBuildings();
        GenerateBush();
    }

    public void RandomizeSeed() {
        Seed.Value = Random.Range(0, 1000000);
        Random.InitState(Seed.Value);
    }

    private void GenerateGround() {
        _groundTilesHolder = new GameObject("GroundTiles").transform;
        _groundTilesHolder.parent = transform;
        _groundTilesHolder.position = new Vector3(0, 0, 2);

        for (int x = 0; x < mapSize.x; x++) {
            for (int y = 0; y < mapSize.y; y++) {
                GroundTile tile = GetRandomGroundTile();
                GameObject tileObj =
                    Instantiate(tile.SpriteGO, new Vector3(x, y, _groundTilesHolder.position.z), Quaternion.identity, _groundTilesHolder);
                tileObj.name = $"GroundTile {x}_{y}";
            }
        }
    }

    private void GenerateBuildings() {
        int numOfBuildings = Random.Range(minBuildings, maxBuildings);

        for (int i = 0; i < numOfBuildings; i++) {
            Vector2Int position = GetNewBuildingPosition();

            if (position == Vector2Int.zero) continue;

            int buildingIndex = Random.Range(0, buildingPrefabs.Length);
            Transform newBuilding =
                Instantiate(buildingPrefabs[buildingIndex], new Vector3(position.x, position.y, 1.5f), Quaternion.identity, transform);
            _spawnedBuildings.Add(newBuilding);
        }
    }

    private void GenerateBush() {
        for (int x = 0; x < mapSize.x; x++) {
            for (int y = 0; y < mapSize.y; y++) {
                if (!IsFarFromBuildings(new Vector2Int(x, y), bushAllowedDistanceToBuildings)) continue;

                int value = Random.Range(0, 100);
                if (value < BushTile.Chance) {
                    GameObject tileObj =
                        Instantiate(BushTile.SpriteGO, new Vector3(x, y, 1.5f), Quaternion.identity, _groundTilesHolder);

                    tileObj.name = $"BushTile {x}_{y}";
                }
            }
        }
    }

    private bool IsWithinBorders(Vector2Int position) {
        bool xWithin = position.x - allowedDistanceToBorders.x >= 0 && position.x + allowedDistanceToBorders.x <= mapSize.x;
        bool yWithin = position.y - allowedDistanceToBorders.y >= 0 && position.y + allowedDistanceToBorders.y <= mapSize.y;
        return xWithin && yWithin;
    }

    private bool IsFarFromBuildings(Vector2Int position, float distance) {
        if (_spawnedBuildings.Count == 0) return true;

        bool isFar = true;
        for (int i = 0; i < _spawnedBuildings.Count; i++) {
            if (Vector2Int.Distance(position, Vector3To2Int(_spawnedBuildings[i].position)) < distance)
                isFar = false;
        }

        return isFar;
    }

    private void RemoveAllMap() {
        for (int i = 0; i < _spawnedBuildings.Count; i++)
            Destroy(_spawnedBuildings[i].gameObject);

        Destroy(_groundTilesHolder?.gameObject);

        _spawnedBuildings.Clear();
    }

    private Vector2Int GetNewBuildingPosition() {
        int iterations = 0;
        while (iterations < iterationFailSafe) {
            Vector2Int position = new(Random.Range(0, mapSize.x), Random.Range(0, mapSize.y));

            if (IsWithinBorders(position) && IsFarFromBuildings(position, allowedDistanceToBuildings)) return position;

            iterations++;
        }

        return Vector2Int.zero;
    }

    private GroundTile GetRandomGroundTile() {
        float total = groundTiles.Select(x => x.Chance).Sum();
        float value = Random.Range(0, total);
        float count = 0;

        for (int i = 0; i < groundTiles.Length - 1; i++) {
            count += groundTiles[i].Chance;
            if (value < count) {
                return groundTiles[i];
            }
        }

        return groundTiles[^1];
    }

    private Vector2Int Vector3To2Int(Vector3 vector3) => new((int)vector3.x, (int)vector3.y);
}

[Serializable]
public struct GroundTile {
    public GameObject SpriteGO;
    public float Chance;
}
