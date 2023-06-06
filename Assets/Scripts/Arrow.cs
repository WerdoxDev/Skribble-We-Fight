using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class Arrow : NetworkBehaviour {
    [Header("General")]
    [SerializeField] private string playerLayer;
    [SerializeField] private string localPlayerLayer;
    [SerializeField] private string ignoreLayer;
    [SerializeField] private string arrowLayer;
    public int Damage;

    [Header("Audio")]
    [SerializeField] private AudioSource arrowHitSound;
    [SerializeField] private float nonLocalSoundVolume;
    [SerializeField] private float nonLocalSpatialBalance;

    private void OnTriggerEnter2D(Collider2D other) {
        if (!IsServer) return;
        if (other.gameObject.layer == LayerMask.NameToLayer(ignoreLayer)) return;

        if (other.gameObject.layer == LayerMask.NameToLayer(arrowLayer))
            if (other.TryGetComponent(out NetworkObject arrowObj))
                if (arrowObj.OwnerClientId == OwnerClientId) return;

        if (other.TryGetComponent(out PlayerController player)) {
            if (player.OwnerClientId == OwnerClientId) return;
            player.TakeDamage(Damage);
            if (player == null) return;

            player.PlayDamageSoundClientRpc(new() { Send = new() { TargetClientIds = new[] { OwnerClientId } } });
            GameController.Instance.SpawnPlayerHitParticleClientRpc(other.ClosestPoint(transform.position));
        }
        else PlayArrowHitSoundClientRpc(OwnerClientId);

        GameController.Instance.SpawnArrowHitParticleClientRpc(other.ClosestPoint(transform.position));

        DisableObjectClientRpc();
        Destroy(gameObject, arrowHitSound.clip.length);
    }

    [ClientRpc]
    private void DisableObjectClientRpc() {
        gameObject.GetComponent<SpriteRenderer>().enabled = false;
        gameObject.GetComponent<BoxCollider2D>().enabled = false;
    }

    [ClientRpc]
    private void PlayArrowHitSoundClientRpc(ulong clientId) {
        arrowHitSound.spatialBlend = nonLocalSpatialBalance;
        arrowHitSound.PlayOneShot(arrowHitSound.clip, clientId == OwnerClientId ? arrowHitSound.volume : arrowHitSound.volume * nonLocalSoundVolume);
    }
}
