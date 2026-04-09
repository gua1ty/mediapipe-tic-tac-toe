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

    public event Action OnClientConnecting;
    
    public event Action OnHostClicked;

    public event Action OnClientConnectionFailed;

    private void Awake()
    {
        // Niente più DontDestroyOnLoad! 
        // Lasciamo che questo script muoia e rinasca con la scena Menu,
        // così si collegherà sempre ai bottoni freschi appena creati.
        Instance = this; 
    }

    private async void Start()
    {
        // 1. Inizializzazione Servizi (PROTETTA!)
        if (UnityServices.State == ServicesInitializationState.Uninitialized)
        {
            await UnityServices.InitializeAsync();
        }

        if (!AuthenticationService.Instance.IsSignedIn)
        {
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        // Se torniamo dal gioco, NetworkManager potrebbe metterci un decimo di secondo a ricrearsi
        if (NetworkManager.Singleton == null)
        {
            Debug.LogWarning("NetworkManager non trovato, assicurati che sia nella scena Menu!");
            return;
        }

        _transport = NetworkManager.Singleton.GetComponent<UnityTransport>();

        // 2. Setup Bottoni (Ora si collegano ai bottoni veri, non ai fantasmi!)
        hostButton.onClick.RemoveAllListeners(); // Pulizia di sicurezza
        hostButton.onClick.AddListener(StartHostRelay);
        
        connectButton.onClick.RemoveAllListeners();
        connectButton.onClick.AddListener(StartClientRelay);

        // 3. Setup Eventi Rete
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
    }

    private async void StartHostRelay()

    {
        
        OnHostClicked?.Invoke();

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

        OnClientConnecting?.Invoke();

        try
        {
            JoinAllocation allocation = await RelayService.Instance.JoinAllocationAsync(code);
            
            _transport.SetRelayServerData(AllocationUtils.ToRelayServerData(allocation, "dtls"));
            NetworkManager.Singleton.StartClient();
        }
        catch (Exception e) { 
        
        Debug.LogError("Errore Client: " + e.Message); 
        OnClientConnectionFailed?.Invoke();

    }
    }

    // Aggiungi "async" perché useremo un piccolo timer
    private async void OnClientConnected(ulong clientId)
    {
        // Controlliamo se siamo l'Host e se chi si è connesso NON siamo noi stessi
        if (IsServer && clientId != NetworkManager.ServerClientId)
        {
            Debug.Log("Client connesso! Mostro caricamento e preparo la scena...");
            
            // 1. Accendiamo la schermata di caricamento per l'Host!
            // Riutilizziamo l'evento che attiva il ConnectionMenu
            OnClientConnecting?.Invoke(); 

            // 2. Aspettiamo 1.5 secondi (ritardo cinematografico)
            // Questo dà il tempo di leggere "Connecting..." o "Player Found!"
            await System.Threading.Tasks.Task.Delay(1500);

            // 3. Ora cambiamo scena per tutti
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