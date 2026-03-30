using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : NetworkBehaviour
{
    public static GameManager Instance { get; private set; }

    public NetworkVariable<int> CurrentTurnIndex = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    
    public NetworkVariable<bool> IsGameOver = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public Board Board { get; private set; }

    public event Action<int, CellState> OnMoveMade;
    public event Action<CellState> OnGameEnded;
    public event Action OnGameRestarted;

    [Header("Riferimenti Pedine O")]
    public GameObject[] oPieces; 

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        Board = new Board();
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            // FONDAMENTALE: Usiamo solo OnLoadEventCompleted. 
            // OnClientConnectedCallback era troppo veloce per il Relay!
            NetworkManager.Singleton.SceneManager.OnLoadEventCompleted += OnSceneLoaded;
        }
        
        CurrentTurnIndex.OnValueChanged += OnTurnChanged;
        IsGameOver.OnValueChanged += OnGameOverChanged;
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer && NetworkManager.Singleton != null && NetworkManager.Singleton.SceneManager != null)
        {
            NetworkManager.Singleton.SceneManager.OnLoadEventCompleted -= OnSceneLoaded;
        }
        CurrentTurnIndex.OnValueChanged -= OnTurnChanged;
        IsGameOver.OnValueChanged -= OnGameOverChanged;
    }

    // Questo metodo ora riceve la lista dei client che hanno FINITO di caricare
    private void OnSceneLoaded(string sceneName, LoadSceneMode loadSceneMode, List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
    {
        // Assicurati che il nome della scena sia identico a quello nelle Build Settings (es. "Game")
        if (sceneName == "Game" && IsServer)
        {
            Debug.Log("Server: Scena caricata. Avvio gioco e assegno pedine.");
            StartGame();

            foreach (ulong clientId in clientsCompleted)
            {
                // Se non è l'Host, gli diamo le pedine O
                if (clientId != NetworkManager.ServerClientId)
                {
                    AssignPiecesToClient(clientId);
                }
            }
        }
    }

    private void AssignPiecesToClient(ulong clientId)
    {
        Debug.Log($"Assegnazione Ownership pedine O al Client: {clientId}");
        foreach (var piece in oPieces)
        {
            if (piece != null)
            {
                var netObj = piece.GetComponent<NetworkObject>();
                if (netObj != null)
                {
                    // Ora funziona perché il client è sicuramente "dentro" la scena
                    netObj.ChangeOwnership(clientId);
                }
            }
        }
    }

    public void StartGame()
    {
        // Usa la stessa logica del rematch, così siamo sicuri che tutto si sincronizzi
        RequestRematch(); 
        Debug.Log("Gioco iniziato! Turno di: X (Host)");
    }

    [Rpc(SendTo.Server)]
    public void PlayMoveRpc(int cellIndex)
    {
        if (IsGameOver.Value) return;

        CellState currentMark = CurrentTurnIndex.Value == 0 ? CellState.X : CellState.O;

        if (Board.TryMakeMove(cellIndex, currentMark))
        {
            UpdateVisualsRpc(cellIndex, (int)currentMark);
            CheckGameState();
        }
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void UpdateVisualsRpc(int cellIndex, int markIndex)
    {
        OnMoveMade?.Invoke(cellIndex, (CellState)markIndex);
    }

    private void CheckGameState()
    {
        CellState winner = Board.CheckWinner();
        if (winner != CellState.Empty)
        {
            IsGameOver.Value = true;
            GameEndedRpc((int)winner);
        }
        else if (Board.IsBoardFull())
        {
            IsGameOver.Value = true;
            GameEndedRpc((int)CellState.Empty);
        }
        else
        {
            CurrentTurnIndex.Value = CurrentTurnIndex.Value == 0 ? 1 : 0;
        }
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void GameEndedRpc(int winnerIndex)
    {
        CellState winner = (CellState)winnerIndex;
        OnGameEnded?.Invoke(winner);
        Debug.Log(winner == CellState.Empty ? "Pareggio!" : "Ha vinto: " + winner);
    }

    public void RequestRematch()
    {
        if (!IsServer) return; // Sicurezza: solo l'Host può farlo

        // 1. Puliamo la logica (che esiste solo sul Server)
        Board.ResetBoard();
        CurrentTurnIndex.Value = 0;
        IsGameOver.Value = false;

        // 2. Urliamo a TUTTI i computer connessi di resettare la loro grafica
        RestartGameVisualsRpc();
    }

    // Questo [Rpc] viaggia su internet e viene eseguito sui computer di tutti
    [Rpc(SendTo.ClientsAndHost)]
    private void RestartGameVisualsRpc()
    {
        Debug.Log("Reset grafico ricevuto! Pulisco il tavolo...");
        
        // Lanciamo l'evento! La UI lo sentirà e rimetterà "Turno X"
        OnGameRestarted?.Invoke(); 
    }

    private void OnTurnChanged(int previous, int current) => Debug.Log("Turno: " + (current == 0 ? "X" : "O"));
    private void OnGameOverChanged(bool previous, bool current) { if (current) Debug.Log("Partita finita!"); }
}