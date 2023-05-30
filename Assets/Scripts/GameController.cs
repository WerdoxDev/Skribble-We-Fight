using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;

public class GameController : NetworkBehaviour {
    public NetworkVariable<bool> CountdownStarted = new(false);
    public NetworkVariable<bool> GameStarted = new(false);

    [SerializeField] private float delayedStartTime = 5f;
    [SerializeField] private UIDocument uiDocument;
    [SerializeField] private VolumeProfile volumeProfile;
    [SerializeField] private GameObject[] playerPresets;

    private VisualElement _rootElement;
    private VisualElement _presetsContainer;
    private Button _bowPresetButton;
    private Button _swordPresetButton;
    private Button _spearPresetButton;
    private Button _axePresetButton;
    private Label _statusText;
    private Label _titleText;

    private float _timeRemaining;
    private readonly Dictionary<ulong, byte> _clientsPreset = new();

    public override void OnNetworkSpawn() {
        _rootElement = uiDocument.rootVisualElement;

        _presetsContainer = _rootElement.Q<VisualElement>("PresetsContainer");
        _bowPresetButton = _rootElement.Q<Button>("CharPresetBow");
        _swordPresetButton = _rootElement.Q<Button>("CharPresetSword");
        _spearPresetButton = _rootElement.Q<Button>("CharPresetSpear");
        _axePresetButton = _rootElement.Q<Button>("CharPresetAxe");
        _statusText = _rootElement.Q<Label>("StatusText");
        _titleText = _rootElement.Q<Label>("Title");

        _bowPresetButton.clicked += () => SendPreset(0);
        _swordPresetButton.clicked += () => SendPreset(1);
        _spearPresetButton.clicked += () => SendPreset(2);
        _axePresetButton.clicked += () => SendPreset(3);

        GameStarted.OnValueChanged += (oldValue, newValue) => {
            ClientStartGame();
        };

        if (IsServer) {
            //SceneTransitionHandler.Instance.OnAllClientsLoaded += () => _shouldStartCountdown = true;
        }

        SceneTransitionHandler.Instance.SetSceneState(SceneTransitionHandler.SceneState.Game);
    }

    private void Update() {
        UpdateGameTimer();
    }

    private void UpdateGameTimer() {
        if (!CountdownStarted.Value) return;
        if (GameStarted.Value) return;

        _timeRemaining -= Time.deltaTime;

        if (IsServer && _timeRemaining <= 0) {
            _timeRemaining = 0;
            GameStarted.Value = true;
            ClientStartGame();
            ServerStartGame();
        }

        if (_timeRemaining > 0.1f) _statusText.text = $"Starting in {Mathf.CeilToInt(_timeRemaining)}...";
    }

    private void ServerStartGame() {

        foreach (KeyValuePair<ulong, byte> clientPreset in _clientsPreset) {
            GameObject playerObj = Instantiate(playerPresets[clientPreset.Value], Vector2.zero, Quaternion.identity);
            playerObj.GetComponent<NetworkObject>().SpawnWithOwnership(clientPreset.Key);
        }
        // Enable in game ui
    }

    private void ClientStartGame() {
        _presetsContainer.style.display = DisplayStyle.None;
        _statusText.style.display = DisplayStyle.None;
        _titleText.style.display = DisplayStyle.None;

        volumeProfile.TryGet(out DepthOfField depthOfField);
        depthOfField.active = false;
    }

    private void StartCountdown() {
        SetTimeRemainingClientRpc(delayedStartTime);
        CountdownStarted.Value = true;
    }

    private void SendPreset(byte presetIndex) {
        ChoosePresetServerRpc(NetworkManager.LocalClientId, presetIndex);

        _statusText.style.display = DisplayStyle.Flex;
        _statusText.text = "Waiting for other players...";

        _presetsContainer.style.display = DisplayStyle.None;
    }

    [ServerRpc(RequireOwnership = false)]
    private void ChoosePresetServerRpc(ulong clientId, byte presetIndex) {
        if (!_clientsPreset.ContainsKey(clientId)) _clientsPreset.Add(clientId, presetIndex);
        if (AllClientsReady()) StartCountdown();
    }

    [ClientRpc]
    private void SetTimeRemainingClientRpc(float delayedStartTime, ClientRpcParams rpcParams = default) {
        _timeRemaining = delayedStartTime;
    }

    private bool AllClientsReady() => _clientsPreset.Count == NetworkManager.ConnectedClients.Count;

}
