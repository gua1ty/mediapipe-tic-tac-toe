using System;
using System.Collections.Generic;
using Unity.Collections;
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
    public event Action OnOpponentDisconnected;

    [Header("Riferimenti Pedine O")]
    public GameObject[] oPieces; 

    private int startingTurnIndex = 0;

    public bool isExiting = false;

    public NetworkVariable<int> XWins = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> OWins = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> Draws = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);



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
            // OnClientConnectedCallback era troppo veloce per il Relay!
            NetworkManager.Singleton.SceneManager.OnLoadEventCompleted += OnSceneLoaded;
        }
        
        CurrentTurnIndex.OnValueChanged += OnTurnChanged;
        IsGameOver.OnValueChanged += OnGameOverChanged;
        NetworkManager.Singleton.OnClientDisconnectCallback += ClientDisconnected;
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

       startingTurnIndex = 0; // La prima partita in assoluto la inizia sempre X
        CurrentTurnIndex.Value = startingTurnIndex;
        
        IsGameOver.Value = false;
        Board.ResetBoard();
        
        // Urliamo a tutti di resettare la grafica per iniziare
        RestartGameVisualsRpc(); 
        
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

       if (winner == CellState.X) XWins.Value++;
        else if (winner == CellState.O) OWins.Value++;

        if (winner != CellState.Empty)
        {
            IsGameOver.Value = true;
            GameEndedRpc((int)winner);
        }
        else if (Board.IsBoardFull())
        {
            IsGameOver.Value = true;
            Draws.Value++;
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
        
        CellState lastWinner = Board.CheckWinner();

        // 2. Ora possiamo pulire la plancia
        Board.ResetBoard();

        // 3. Applichiamo la tua logica per chi inizia
        if (lastWinner == CellState.X)
        {
            startingTurnIndex = 1; // Ha vinto X, tocca a O
        }
        else if (lastWinner == CellState.O)
        {
            startingTurnIndex = 0; // Ha vinto O, tocca a X
        }
        else 
        {
            // Pareggio: Alterniamo. Se prima era 0 diventa 1, se era 1 diventa 0.
            startingTurnIndex = (startingTurnIndex == 0) ? 1 : 0;
        }

        // 4. Assegniamo il nuovo turno iniziale alla variabile di rete
        CurrentTurnIndex.Value = startingTurnIndex;

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

    private void ClientDisconnected(ulong clientId)
    {
        if (isExiting) return; // Se abbiamo premuto Esci noi, ignoriamo l'allarme!

        // ... qui sotto tieni il resto del tuo codice con gli if(IsServer) ...
        if (IsServer && clientId != NetworkManager.ServerClientId)
        {
            Debug.Log("Il Client si è disconnesso!");
            OnOpponentDisconnected?.Invoke(); 
        }
        else if (!IsServer && clientId == NetworkManager.ServerClientId)
        {
            Debug.Log("L'Host ha chiuso la stanza!");
            OnOpponentDisconnected?.Invoke(); 
        }
    }
    // Questo metodo può essere chiamato da chiunque (UI, input da tastiera, ecc.)
    public void LeaveGame()
    {
        isExiting = true; // Segnaliamo che stiamo uscendo volontariamente!
        StartCoroutine(ShutdownAndReturnToMenu());
    }

    private System.Collections.IEnumerator ShutdownAndReturnToMenu()
    {
        if (NetworkManager.Singleton != null)
        {
            // 1. Diamo l'ordine di spegnimento
            NetworkManager.Singleton.Shutdown();
            
            // 2. MAGIA: Aspettiamo finché Netcode non ha finito di spegnersi completamente!
            yield return new WaitWhile(() => NetworkManager.Singleton.ShutdownInProgress);
        }

        // 3. Torniamo al Menu in totale sicurezza
        UnityEngine.SceneManagement.SceneManager.LoadScene("Menu"); 
    }

    private void OnTurnChanged(int previous, int current) => Debug.Log("Turno: " + (current == 0 ? "X" : "O"));
    private void OnGameOverChanged(bool previous, bool current) { if (current) Debug.Log("Partita finita!"); }
}