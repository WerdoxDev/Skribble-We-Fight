using JetBrains.Annotations;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UIElements;

public class LobbyController : NetworkBehaviour {
    [SerializeField] private string gameSceneName;
    [SerializeField] private UIDocument uiDocument;
    [SerializeField] private VisualTreeAsset lobbyPlayerAsset;

    private VisualElement _rootElement;
    private VisualElement _playersContainer;
    private Button _readyButton;

    private readonly Dictionary<ulong, bool> _clientsInLobby = new();

    public override void OnNetworkSpawn() {
        _rootElement = uiDocument.rootVisualElement;
        _playersContainer = _rootElement.Q<VisualElement>("LobbyPlayerContainer");
        _readyButton = _rootElement.Q<Button>("ReadyButton");

        _readyButton.clicked += () => {
            bool isLocalClientReady = IsLocalClientReady();
            _readyButton.text = !isLocalClientReady ? "Unready" : "Ready";
            SetPlayerReadyServerRpc(NetworkManager.LocalClientId, !isLocalClientReady, false);
        };

        _clientsInLobby.Add(NetworkManager.LocalClientId, false);

        if (IsServer) {
            NetworkManager.OnClientConnectedCallback += OnClientConnected;
        }

        UpdatePlayersUI();

        SceneTransitionHandler.Instance.SetSceneState(SceneTransitionHandler.SceneState.Lobby);
    }

    private void UpdatePlayersUI() {
        _playersContainer.Clear();

        int index = 0;
        foreach (KeyValuePair<ulong, bool> player in _clientsInLobby) {
            VisualElement playerElement = lobbyPlayerAsset.Instantiate().Q<VisualElement>("LobbyPlayer");

            playerElement.Q<Label>("PlayerName").text =
                $"Player {player.Key + 1} " +
                $"{(player.Key == NetworkManager.LocalClientId ? "(you)" : player.Key == NetworkManager.ServerClientId ? "(host)" : "")}";
            playerElement.Q<Label>("ReadyStatus").text = player.Value ? "(READY)" : "(NOT READY)";

            _playersContainer.Add(playerElement);
            index++;
        }
    }

    private void UpdatePlayers() {
        foreach (KeyValuePair<ulong, bool> player in _clientsInLobby)
            SetPlayerReadyClientRpc(player.Key, player.Value);

        CheckAllPlayersReady();
    }

    private void CheckAllPlayersReady() {
        bool allPlayersReady = _clientsInLobby.All(x => x.Value);

        if (!allPlayersReady) return;

        NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;

        SceneTransitionHandler.Instance.SwitchScene(gameSceneName);
    }

    [ServerRpc(RequireOwnership = false)]
    private void SetPlayerReadyServerRpc(ulong clientId, bool isReady, bool exludeServer = true) =>
        SetPlayerReadyClientRpc(clientId, isReady, exludeServer);

    [ClientRpc]
    private void SetPlayerReadyClientRpc(ulong clientId, bool isReady, bool exludeServer = true) {
        if (IsServer && exludeServer) return;

        if (!_clientsInLobby.ContainsKey(clientId)) _clientsInLobby.Add(clientId, isReady);
        _clientsInLobby[clientId] = isReady;

        if (IsServer) CheckAllPlayersReady();
        UpdatePlayersUI();
    }

    private bool IsLocalClientReady() => _clientsInLobby[NetworkManager.LocalClientId];

    private void OnClientConnected(ulong clientId) {
        if (!_clientsInLobby.ContainsKey(clientId)) _clientsInLobby.Add(clientId, false);
        UpdatePlayersUI();
        UpdatePlayers();
    }
}
