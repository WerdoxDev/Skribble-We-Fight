using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

public class Building : MonoBehaviour {
    [SerializeField] private Transform[] propPositions;
    [SerializeField] private GameObject[] propPrefabs;

    private void Awake() {
        foreach (Vector3 pos in propPositions.Select(x => x.position)) {
            int randomIndex = Random.Range(0, propPrefabs.Length);
            Quaternion randomRotation = Quaternion.Euler(0, 0, Random.Range(0, 360));
            Instantiate(propPrefabs[randomIndex], new Vector3(pos.x, pos.y, 0.5f), randomRotation, transform);
        }
    }
}
