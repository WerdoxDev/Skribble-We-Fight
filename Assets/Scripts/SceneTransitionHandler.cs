using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneTransitionHandler : MonoBehaviour {
    public static SceneTransitionHandler Instance;

    [SerializeField] private string MainMenuScene;

    public enum SceneState { Init, Menu, Lobby, InGame }

    private SceneState _sceneState;
    private int _numOfClientsLoaded;

    public event Action<SceneState> OnSceneStateChanged;
    public event Action<ulong> OnClientSceneLoaded;

    private void Awake() {
        if (Instance != null && Instance != this) Destroy(Instance.gameObject);
        Instance = this;
        DontDestroyOnLoad(this);
    }

    private void Start() {
        if (_sceneState == SceneState.Init) {
            SetSceneState(SceneState.Menu);
            SceneManager.LoadScene(MainMenuScene);
        }
    }

    public void SetSceneState(SceneState sceneState) {
        _sceneState = sceneState;
        OnSceneStateChanged?.Invoke(_sceneState);
    }

    public void SwitchScene(string sceneName) {
        if (NetworkManager.Singleton.IsListening) {
            _numOfClientsLoaded = 0;
            NetworkManager.Singleton.SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
        }
        else SceneManager.LoadSceneAsync(sceneName);
    }

    public void RegisterCallbacks() {
        NetworkManager.Singleton.SceneManager.OnLoadComplete += OnSceneLoadComplete;
    }

    public void ExitAndLoadMenuScene() {
        NetworkManager.Singleton.SceneManager.OnLoadComplete -= OnSceneLoadComplete;
        OnClientSceneLoaded = null;
        SetSceneState(SceneState.Menu);
        SceneManager.LoadScene(MainMenuScene);
    }

    public SceneState GetSceneState() => _sceneState;

    public bool AllClientsAreLoaded() => _numOfClientsLoaded == NetworkManager.Singleton.ConnectedClients.Count;

    private void OnSceneLoadComplete(ulong clientId, string sceneName, LoadSceneMode loadSceneMode) {
        _numOfClientsLoaded += 1;
        OnClientSceneLoaded?.Invoke(clientId);
    }
}
