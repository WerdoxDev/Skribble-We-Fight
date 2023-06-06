using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;

using Cursor = UnityEngine.Cursor;
using Random = UnityEngine.Random;

public class GameController : NetworkBehaviour {
    public static GameController Instance;

    public NetworkVariable<bool> CountdownStarted = new(false);
    public NetworkVariable<bool> PostGameCountdownStarted = new(false);
    public NetworkVariable<bool> GameStarted = new(false);

    [Header("General")]
    [SerializeField] private UIDocument uiDocument;
    [SerializeField] private VolumeProfile volumeProfile;
    [SerializeField] private GameObject spectatorCamera;
    [SerializeField] private GameObject mainCamera;
    [SerializeField] private Transform hitParticle;
    [SerializeField] private Transform arrowCollideParticle;
    [SerializeField] private Transform playerDieParticle;
    [SerializeField] private GameObject[] playerPresets;

    [Header("Audio")]
    [SerializeField] private AudioSource playerDieSound;

    [Header("Game")]
    [SerializeField] private float delayedStartTime = 5f;
    [SerializeField] private float delayedPostGameTime = 5f;

    private VisualElement _rootElement;
    private VisualElement _presetsContainer;
    private ProgressBar _healthBar;
    private Button _bowPresetButton;
    private Button _swordPresetButton;
    private Button _spearPresetButton;
    private Button _axePresetButton;
    private Label _statusText;
    private Label _titleText;
    private Label _pingText;

    private float _timeRemaining;
    private int _lastSelectedPreset = -1;
    private DateTime _timeBeforePing;
    private readonly Dictionary<ulong, byte> _clientsPreset = new();
    private readonly List<ulong> _playersInGame = new();

    private void Awake() {
        if (Instance != null && Instance != this) Destroy(Instance.gameObject);
        Instance = this;
    }

    public override void OnNetworkSpawn() {
        _rootElement = uiDocument.rootVisualElement;

        _presetsContainer = _rootElement.Q<VisualElement>("PresetsContainer");
        _healthBar = _rootElement.Q<ProgressBar>("HealthBar");
        _bowPresetButton = _rootElement.Q<Button>("CharPresetBow");
        _swordPresetButton = _rootElement.Q<Button>("CharPresetSword");
        _spearPresetButton = _rootElement.Q<Button>("CharPresetSpear");
        _axePresetButton = _rootElement.Q<Button>("CharPresetAxe");
        _statusText = _rootElement.Q<Label>("StatusText");
        _titleText = _rootElement.Q<Label>("Title");
        _pingText = _rootElement.Q<Label>("Ping");

        _bowPresetButton.clicked += () => SendPreset(0);
        _swordPresetButton.clicked += () => SendPreset(1);
        _spearPresetButton.clicked += () => SendPreset(2);
        _axePresetButton.clicked += () => SendPreset(3);

        GameStarted.OnValueChanged += (oldValue, newValue) => {
            if (IsServer) return;
            if (newValue) ClientStartGame();
            else ClientStartPresetChoosing();
        };

        if (!IsServer) {
            _pingText.style.display = DisplayStyle.Flex;
            SendPing();
        }
        else _pingText.style.display = DisplayStyle.None;

        SceneTransitionHandler.Instance.SetSceneState(SceneTransitionHandler.SceneState.Game);
        AudioController.Instance.PlayGameTheme();

        if (GameStarted.Value) ClientStartSpectator(true);
        else ClientStartPresetChoosing();
    }

    private void Update() {
        UpdateGameTimer();
    }

    #region Game State Management

    public void SetHealthBarProgress(int progress, int max) {
        _healthBar.highValue = max;
        _healthBar.value = progress;
    }

    public void PlayerDied(ulong clientId) {
        StartSpectatorClientRpc(new() { Send = new() { TargetClientIds = new[] { clientId } } });
        DespawnPlayer(clientId);
    }

    private void UpdateGameTimer() {
        if (!CountdownStarted.Value && !PostGameCountdownStarted.Value) return;
        if (GameStarted.Value && !PostGameCountdownStarted.Value) return;

        _timeRemaining -= Time.deltaTime;

        if (IsServer && _timeRemaining <= 0) {
            _timeRemaining = 0;

            if (CountdownStarted.Value) {
                CountdownStarted.Value = false;
                GameStarted.Value = true;
                ClientStartGame();
                ServerStartGame();
            }
            else if (PostGameCountdownStarted.Value) {
                MapGenerator.Instance.RandomizeSeed();
                PostGameCountdownStarted.Value = false;
                GameStarted.Value = false;
                ClientStartPresetChoosing();
            }
        }

        if (_timeRemaining <= 0.1f) return;

        if (CountdownStarted.Value) _statusText.text = $"Starting in {Mathf.CeilToInt(_timeRemaining)}...";
        else if (PostGameCountdownStarted.Value) _statusText.text = $"New Match starting in {Mathf.CeilToInt(_timeRemaining)}...";
    }

    private void ServerStartGame() {
        foreach (KeyValuePair<ulong, byte> clientPreset in _clientsPreset) {
            Vector2Int mapSize = MapGenerator.Instance.MapSize;
            Vector2 randomPosition = new(Random.Range(0, mapSize.x), Random.Range(0, mapSize.y));
            GameObject playerObj = Instantiate(playerPresets[clientPreset.Value], randomPosition, Quaternion.identity);
            playerObj.GetComponent<NetworkObject>().SpawnAsPlayerObject(clientPreset.Key);
            _playersInGame.Add(clientPreset.Key);
        }
    }

    private void ClientStartPresetChoosing() {
        MapGenerator.Instance.GenerateMap();

        _presetsContainer.style.display = DisplayStyle.Flex;
        _statusText.style.display = DisplayStyle.None;
        _titleText.style.display = DisplayStyle.Flex;
        _healthBar.style.display = DisplayStyle.None;

        _bowPresetButton.EnableInClassList("game__char__container--disabled", _lastSelectedPreset == 0);
        _swordPresetButton.EnableInClassList("game__char__container--disabled", _lastSelectedPreset == 1);
        _spearPresetButton.EnableInClassList("game__char__container--disabled", _lastSelectedPreset == 2);
        _axePresetButton.EnableInClassList("game__char__container--disabled", _lastSelectedPreset == 3);

        _titleText.text = "Choose A Preset";
        spectatorCamera.SetActive(false);

        volumeProfile.TryGet(out DepthOfField depthOfField);
        depthOfField.active = true;
    }

    private void ClientStartGame() {
        _presetsContainer.style.display = DisplayStyle.None;
        _statusText.style.display = DisplayStyle.None;
        _titleText.style.display = DisplayStyle.None;
        _healthBar.style.display = DisplayStyle.Flex;

        mainCamera.SetActive(false);

        volumeProfile.TryGet(out DepthOfField depthOfField);
        depthOfField.active = false;

        Cursor.lockState = CursorLockMode.Confined;
    }

    private void ClientStartSpectator(bool generateMap = false) {
        if (generateMap) MapGenerator.Instance.GenerateMap();

        _presetsContainer.style.display = DisplayStyle.None;
        _statusText.style.display = DisplayStyle.None;
        _titleText.style.display = DisplayStyle.Flex;
        _healthBar.style.display = DisplayStyle.None;

        _titleText.text = "Spectating";
        spectatorCamera.SetActive(true);

        volumeProfile.TryGet(out DepthOfField depthOfField);
        depthOfField.active = false;

        Cursor.lockState = CursorLockMode.None;
    }

    private void StartCountdown() {
        SetTimeRemainingClientRpc(delayedStartTime);
        CountdownStarted.Value = true;
    }

    private void StartPostGameCountdown() {
        SetTimeRemainingClientRpc(delayedPostGameTime);
        PostGameCountdownStarted.Value = true;
    }

    private void SendPreset(byte presetIndex) {
        if (_lastSelectedPreset == presetIndex) return;

        ChoosePresetServerRpc(NetworkManager.LocalClientId, presetIndex);

        _statusText.style.display = DisplayStyle.Flex;
        _statusText.text = "Waiting for other players...";

        _presetsContainer.style.display = DisplayStyle.None;

        _lastSelectedPreset = presetIndex;
    }

    private void CheckForEndGame() {
        if (_playersInGame.Count > 1) return;

        NetworkObject lastPlayer = NetworkManager.ConnectedClients[_playersInGame[0]].PlayerObject;
        lastPlayer.Despawn();

        ShowWinnerClientRpc(lastPlayer.OwnerClientId);
        StartPostGameCountdown();

        _clientsPreset.Clear();
        _playersInGame.Clear();
    }

    private void DespawnPlayer(ulong clientId) {
        NetworkObject playerObj = NetworkManager.ConnectedClients[clientId].PlayerObject;
        PlayerDieClientRpc(playerObj.transform.position);
        _playersInGame.Remove(clientId);
        Destroy(playerObj.gameObject);
        CheckForEndGame();
    }

    private bool AllClientsReady() => _clientsPreset.Count == NetworkManager.ConnectedClients.Count;

    [ServerRpc(RequireOwnership = false)]
    private void ChoosePresetServerRpc(ulong clientId, byte presetIndex) {
        if (!_clientsPreset.ContainsKey(clientId)) _clientsPreset.Add(clientId, presetIndex);
        if (AllClientsReady()) StartCountdown();
    }

    [ClientRpc]
    private void SetTimeRemainingClientRpc(float delayedStartTime, ClientRpcParams rpcParams = default) {
        _timeRemaining = delayedStartTime;
    }

    [ClientRpc]
    private void StartSpectatorClientRpc(ClientRpcParams rpcParams) {
        ClientStartSpectator();
    }

    [ClientRpc]
    private void ShowWinnerClientRpc(ulong winnerClientId) {
        _healthBar.style.display = DisplayStyle.None;
        _titleText.style.display = DisplayStyle.Flex;
        _statusText.style.display = DisplayStyle.Flex;

        mainCamera.SetActive(true);
        spectatorCamera.SetActive(false);

        _statusText.text = "";
        _titleText.text = $"{(winnerClientId == NetworkManager.LocalClientId ? "You" : "Player " + (winnerClientId + 1))} Won!";

        volumeProfile.TryGet(out DepthOfField depthOfField);
        depthOfField.active = true;

        if (winnerClientId != NetworkManager.LocalClientId) _lastSelectedPreset = -1;
    }

    #endregion

    private void SendPing() {
        PingServerRpc();
        _timeBeforePing = DateTime.Now;
    }

    [ServerRpc(RequireOwnership = false)]
    private void PingServerRpc(ServerRpcParams rpcParams = default) {
        PongClientRpc(new() { Send = new() { TargetClientIds = new[] { rpcParams.Receive.SenderClientId } } });
    }

    [ClientRpc]
    private void PongClientRpc(ClientRpcParams rpcParams = default) {
        TimeSpan timeSpan = DateTime.Now - _timeBeforePing;
        int latency = Mathf.FloorToInt((float)timeSpan.TotalMilliseconds);

        _pingText.text = $"Ping: {latency}ms";

        SendPing();
    }

    [ClientRpc]
    public void SpawnPlayerHitParticleClientRpc(Vector2 position) {
        Instantiate(hitParticle, position, Quaternion.identity);
    }

    [ClientRpc]
    public void SpawnArrowHitParticleClientRpc(Vector2 position) {
        Instantiate(arrowCollideParticle, position, Quaternion.identity);
    }

    [ClientRpc]
    public void PlayerDieClientRpc(Vector2 position) {
        Instantiate(playerDieParticle, position, Quaternion.identity);
        playerDieSound.Play();
    }
}
