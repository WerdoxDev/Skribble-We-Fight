using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class ArrowSpawner : NetworkBehaviour {
    [SerializeField] private Transform spawnPosition;
    [SerializeField] private GameObject arrowPrefab;
    [SerializeField] private float arrowSpeed;
    [SerializeField] private float arrowSpread;
    [SerializeField] private float arrowKeepAliveTime;

    private PlayerController _player;

    private void Awake() {
        _player = GetComponent<PlayerController>();
    }

    public void SpawnArrow() { if (IsOwner) SpawnArrowServerRpc(); }
    public void SpawnMultipleArrows() { if (IsOwner) SpawnMultipleArrowsServerRpc(); }

    [ServerRpc]
    public void SpawnMultipleArrowsServerRpc() {
        for (int i = 0; i < 3; i++) {
            GameObject arrow = Instantiate(arrowPrefab, spawnPosition.position, spawnPosition.rotation);

            if (i == 0) arrow.transform.rotation *= Quaternion.Euler(0, 0, arrowSpread);
            else if (i == 1) arrow.transform.rotation *= Quaternion.Euler(0, 0, -arrowSpread);

            arrow.GetComponent<Rigidbody2D>().AddForce(arrow.transform.up * arrowSpeed, ForceMode2D.Impulse);
            arrow.GetComponent<Arrow>().Damage = i == 2 ? _player.NormalDamage : _player.SecondaryDamage;

            arrow.GetComponent<NetworkObject>().SpawnWithOwnership(OwnerClientId);

            Destroy(arrow, arrowKeepAliveTime);
        }
    }

    [ServerRpc]
    public void SpawnArrowServerRpc() {
        GameObject arrow = Instantiate(arrowPrefab, spawnPosition.position, spawnPosition.rotation);

        arrow.GetComponent<Rigidbody2D>().AddForce(arrow.transform.up * arrowSpeed, ForceMode2D.Impulse);
        arrow.GetComponent<Arrow>().Damage = _player.NormalDamage;

        arrow.GetComponent<NetworkObject>().SpawnWithOwnership(OwnerClientId);

        Destroy(arrow, arrowKeepAliveTime);
    }
}
