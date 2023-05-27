using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.UIElements;

public class MenuController : MonoBehaviour {
    [SerializeField] string lobbySceneName;
    [SerializeField] private UIDocument uiDocument;

    private VisualElement _rootElement;
    private TextField _ipField;
    private Button _joinButton;
    private Button _hostButton;

    private void Awake() {
        _rootElement = uiDocument.rootVisualElement;

        _ipField = _rootElement.Q<TextField>("IpField");
        _joinButton = _rootElement.Q<Button>("JoinButton");
        _hostButton = _rootElement.Q<Button>("HostButton");

        _joinButton.clicked += () => JoinGame();
        _hostButton.clicked += () => HostGame();
    }

    public void HostGame() {
        UnityTransport transport = (UnityTransport)NetworkManager.Singleton.NetworkConfig.NetworkTransport;
        string[] ipSplit = _ipField.value.Split(":");
        transport.SetConnectionData(ipSplit[0], ushort.Parse(ipSplit[1]));

        if (NetworkManager.Singleton.StartHost()) {
            SceneTransitionHandler.Instance.RegisterCallbacks();
            SceneTransitionHandler.Instance.SwitchScene(lobbySceneName);
        }
    }

    public void JoinGame() {
        UnityTransport transport = (UnityTransport)NetworkManager.Singleton.NetworkConfig.NetworkTransport;
        string[] ipSplit = _ipField.value.Split(":");
        transport.SetConnectionData(ipSplit[0], ushort.Parse(ipSplit[1]));

        NetworkManager.Singleton.StartClient();
    }
}
