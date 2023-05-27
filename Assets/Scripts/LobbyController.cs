using JetBrains.Annotations;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UIElements;

public class LobbyController : NetworkBehaviour {
    [SerializeField] private string inGameSceneName;
    [SerializeField] private UIDocument uiDocument;
    [SerializeField] private VisualTreeAsset lobbyPlayerAsset;

    private VisualElement _rootElement;
    private VisualElement _playersContainer;
    private Button _readyButton;

    private Dictionary<ulong, bool> _clientsInLobby = new();

    public override void OnNetworkSpawn() {
        _rootElement = uiDocument.rootVisualElement;
        _playersContainer = _rootElement.Q<VisualElement>("LobbyPlayerContainer");
        _readyButton = _rootElement.Q<Button>("ReadyButton");

        if (IsServer) {
            NetworkManager.OnClientConnectedCallback += OnClientConnected;
        }

        UpdatePlayers();
        UpdatePlayers();

        SceneTransitionHandler.Instance.SetSceneState(SceneTransitionHandler.SceneState.Lobby);
    }

    private void UpdatePlayers() {
        _playersContainer.Clear();

        int index = 0;
        foreach (KeyValuePair<ulong, bool> player in _clientsInLobby) {
            VisualElement playerElement = lobbyPlayerAsset.Instantiate().Q<VisualElement>("LobbyPlayer");

            playerElement.Q<Label>("PlayerName").text = $"Player #{index + 1}";
            playerElement.Q<Label>("ReadyStatus").text = player.Value ? "(READY)" : "(NOT READY)";

            _playersContainer.Add(playerElement);
            index++;
        }
    }

    [ServerRpc]
    private void SetPlayerReadyServerRpc(ulong clientId, bool isReady) => SetPlayerReadyClientRpc(clientId, isReady);

    [ClientRpc]
    private void SetPlayerReadyClientRpc(ulong clientId, bool isReady) {
        if (!_clientsInLobby.ContainsKey(clientId)) return;
        _clientsInLobby[clientId] = isReady;
    }

    private void OnClientConnected(ulong clientId) {
        if (!_clientsInLobby.ContainsKey(clientId)) _clientsInLobby.Add(clientId, false);
        UpdatePlayers();
    }
}
