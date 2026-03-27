using System;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;
using UnityEngine.UI;

public class ConnectionManager : NetworkBehaviour
{
    public static ConnectionManager Instance { get; private set; }

    [Header("UI Elements")]
    [SerializeField] private Button hostButton;
    [SerializeField] private Button connectButton; 
    [SerializeField] private TMPro.TMP_InputField joinCodeInputField;

    public event Action<string> OnJoinCodeGenerated;
    private UnityTransport _transport;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private async void Start()
    {
        // 1. Inizializzazione Servizi (Una volta sola)
        await UnityServices.InitializeAsync();
        if (!AuthenticationService.Instance.IsSignedIn)
        {
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        _transport = NetworkManager.Singleton.GetComponent<UnityTransport>();

        // 2. Setup Bottoni
        hostButton.onClick.AddListener(StartHostRelay);
        connectButton.onClick.AddListener(StartClientRelay);

        // 3. Setup Eventi Rete
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
    }

    private async void StartHostRelay()
    {
        try
        {
            // Crea stanza per 2 persone (1 ospite + 1 host)
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(1);
            string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

            Debug.Log($"Codice Generato: {joinCode}");
            OnJoinCodeGenerated?.Invoke(joinCode);

            // Configura il trasporto e avvia
            _transport.SetRelayServerData(AllocationUtils.ToRelayServerData(allocation, "dtls"));
            NetworkManager.Singleton.StartHost();
        }
        catch (Exception e) { Debug.LogError("Errore Host: " + e.Message); }
    }

    private async void StartClientRelay()
    {
        string code = joinCodeInputField.text.Trim().ToUpper();
        if (code.Length != 6) return;

        try
        {
            JoinAllocation allocation = await RelayService.Instance.JoinAllocationAsync(code);
            
            _transport.SetRelayServerData(AllocationUtils.ToRelayServerData(allocation, "dtls"));
            NetworkManager.Singleton.StartClient();
        }
        catch (Exception e) { Debug.LogError("Errore Client: " + e.Message); }
    }

    private void OnClientConnected(ulong clientId)
    {
        // Solo l'Host decide quando cambiare scena
        if (IsServer && clientId != NetworkManager.ServerClientId)
        {
            Debug.Log("Client connesso! Cambio scena...");
            NetworkManager.Singleton.SceneManager.LoadScene("Game", UnityEngine.SceneManagement.LoadSceneMode.Single);
        }
    }

    public override void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        base.OnDestroy();
    }
}