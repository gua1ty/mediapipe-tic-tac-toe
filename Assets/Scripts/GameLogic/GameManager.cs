using System;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.SceneManagement;

using Random = UnityEngine.Random;

public class GameManager : NetworkBehaviour
{
    private Coroutine pauseCoroutine;
    public bool IsMyTurnLocal { get; private set; }
    public static GameManager Instance { get; private set; }
    public Board Board { get; private set; }

    public NetworkVariable<int> CurrentTurnIndex = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkVariable<bool> IsGameOver = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // Eventi
    public event Action<int, CellState> OnMoveMade;
    public event Action<CellState> OnGameEnded;
    public event Action OnGameRestarted;
    public event Action OnOpponentDisconnected;
    public event Action OnCountdownStarted;
    
    [Header("Riferimenti Pedine")]
    public GameObject[] oPieces; // Pedine Client
    public GameObject[] xPieces; // Pedine Host

    [Header("Risultati")]
    public NetworkVariable<int> XWins = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> OWins = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> Draws = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    [Header("Timer Sessione")]
    public NetworkVariable<bool> IsTimerActive = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<float> TimerX = new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<float> TimerO = new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private bool isTurnTimer = false;
    private bool timerRunning = false;

    [Header("Vite Giocatori")]
    public NetworkVariable<int> LivesX = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> LivesO = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> MaxLives = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    [Header("Stato Gioco")]
    public NetworkVariable<bool> IsGameStarted = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<bool> HostReady = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<bool> ClientReady = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<bool> IsPaused = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    [Header("Puntatori")]
    [SerializeField] private GameObject pointerPrefab;

    [Header("Modalità Chaos")]

    public NetworkVariable<bool> IsChaosMode = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public List<Transform> leftSlots;  // Lista di punti a sinistra
    public List<Transform> rightSlots; // Lista di punti a destra
    public float tableY = 0.8f;



    private CellState lastGameWinner = CellState.Empty;
    private int startingTurnIndex = 0;
    public bool isExiting = false;

    // ─────────────────────────────────────────────
    // LIFECYCLE
    // ─────────────────────────────────────────────

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
        MaxLives.Value = SessionData.maxErrors;
        LivesX.Value = MaxLives.Value;
        LivesO.Value = MaxLives.Value;
        NetworkManager.Singleton.SceneManager.OnLoadEventCompleted += OnSceneLoaded;
    }

    CurrentTurnIndex.OnValueChanged += OnTurnChanged;
    IsGameOver.OnValueChanged += OnGameOverChanged;
    NetworkManager.Singleton.OnClientDisconnectCallback += ClientDisconnected;
}

    public override void OnNetworkDespawn()
    {
        if (IsServer && NetworkManager.Singleton != null && NetworkManager.Singleton.SceneManager != null)
            NetworkManager.Singleton.SceneManager.OnLoadEventCompleted -= OnSceneLoaded;

        CurrentTurnIndex.OnValueChanged -= OnTurnChanged;
        IsGameOver.OnValueChanged -= OnGameOverChanged;
    }

    private void OnSceneLoaded(string sceneName, LoadSceneMode loadSceneMode, List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
{
    if (sceneName == "Game" && IsServer)
    {
        // Assegnazione proprietà...
        foreach (ulong clientId in clientsCompleted)
        {
            if (clientId != NetworkManager.ServerClientId) AssignPiecesToClient(clientId);
        }

        if (SessionData.gameMode == "Chaos")
        {
            IsChaosMode.Value = true;
            StartCoroutine(ApplyChaosLayoutDelayed());
        }
        else 
        {
            // Se è Normal, le accendiamo subito nella loro posizione ordinata
            SetPiecesVisibilityRpc(true);
        }
    }
}

// Aggiungi questo RPC per gestire la visibilità
[Rpc(SendTo.ClientsAndHost)]
private void SetPiecesVisibilityRpc(bool isVisible)
{
    foreach (var p in xPieces) if (p.GetComponent<MeshRenderer>() != null) {
            p.GetComponent<MeshRenderer>().enabled = true;
        };
    foreach (var p in oPieces) if (p.GetComponent<MeshRenderer>() != null) {
            p.GetComponent<MeshRenderer>().enabled = true;
        };;
}

    private void AssignPiecesToClient(ulong clientId)
    {
        foreach (var piece in oPieces)
        {
            if (piece == null) continue;
            var netObj = piece.GetComponent<NetworkObject>();
            if (netObj != null) netObj.ChangeOwnership(clientId);
        }
    }

    // ─────────────────────────────────────────────
    // CHAOS MODE — SPAWN
    // ─────────────────────────────────────────────

    // Aspetta 2 frame per essere sicuri che tutti i NetworkTransform siano inizializzati
    private System.Collections.IEnumerator ApplyChaosLayoutDelayed()
{
    // 1. Aspettiamo un attimo che la rete si stabilizzi
    yield return new WaitForSeconds(0.1f);

    // 2. Sparpagliamo le pedine (mentre sono ancora invisibili)
    ApplyChaosLayout();

    // 3. Aspettiamo un istante che il teletrasporto avvenga su tutti i client
    yield return new WaitForSeconds(0.2f);

    // 4. ACCENDIAMO LE PEDINE
    SetPiecesVisibilityRpc(true);
    EnableAllPiecesPhysicsRpc();
}
[Rpc(SendTo.ClientsAndHost)]
private void EnableAllPiecesPhysicsRpc()
{
    foreach (var p in xPieces) p?.GetComponent<DraggablePiece>()?.EnablePhysics();
    foreach (var p in oPieces) p?.GetComponent<DraggablePiece>()?.EnablePhysics();
}


private void ApplyChaosLayout()
{
    // 1. Uniamo tutte le pedine
    List<GameObject> allPieces = new List<GameObject>();
    if (xPieces != null) allPieces.AddRange(xPieces);
    if (oPieces != null) allPieces.AddRange(oPieces);

    // 2. Uniamo tutti gli slot disponibili
    List<Transform> allAvailableSlots = new List<Transform>();
    if (leftSlots != null) allAvailableSlots.AddRange(leftSlots);
    if (rightSlots != null) allAvailableSlots.AddRange(rightSlots);

    // 3. Mescoliamo gli slot (Shuffle) per un vero effetto Chaos
    // Usiamo l'algoritmo Fisher-Yates
    for (int i = 0; i < allAvailableSlots.Count; i++)
    {
        Transform temp = allAvailableSlots[i];
        int randomIndex = Random.Range(i, allAvailableSlots.Count);
        allAvailableSlots[i] = allAvailableSlots[randomIndex];
        allAvailableSlots[randomIndex] = temp;
    }

    // 4. Prepariamo i dati per l'RPC
    ulong[] netIds = new ulong[allPieces.Count];
    Vector3[] positions = new Vector3[allPieces.Count];
    float[] rotations = new float[allPieces.Count];

    for (int i = 0; i < allPieces.Count; i++)
    {
        if (allPieces[i] == null || i >= allAvailableSlots.Count) continue;

        // Prendiamo lo slot corrispondente
        Transform targetSlot = allAvailableSlots[i];

        netIds[i] = allPieces[i].GetComponent<NetworkObject>().NetworkObjectId;
        // Usiamo la posizione esatta dello slot
        positions[i] = new Vector3(targetSlot.position.x, tableY, targetSlot.position.z);
        rotations[i] = Random.Range(0f, 360f); // Rotazione estetica casuale
    }

    ApplyChaosPositionsRpc(netIds, positions, rotations);
}

    // Ogni client (e host) applica localmente le posizioni con autorità sul proprio NetworkTransform
  [Rpc(SendTo.ClientsAndHost)]
private void ApplyChaosPositionsRpc(ulong[] netIds, Vector3[] positions, float[] rotations)
{
    for (int i = 0; i < netIds.Length; i++)
    {
        // Cerchiamo l'oggetto nella rete
        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(netIds[i], out var netObj))
            continue;

        GameObject piece = netObj.gameObject;
        var draggable = piece.GetComponent<DraggablePiece>();
        var rb = piece.GetComponent<Rigidbody>();

        // --- AZIONE 1: AGGIORNA LA MEMORIA (startPosition) ---
        // Lo facciamo per TUTTI, indipendentemente da chi è il proprietario.
        // Questo risolve il problema della pedina che torna al posto sbagliato.
        if (draggable != null)
        {
            draggable.SetStartPosition(positions[i], Quaternion.Euler(0, rotations[i], 0));
        }

        // --- AZIONE 2: SPOSTAMENTO FISICO ---
        // Se io sono il proprietario di questa pedina, DEVO spostarla ora.
        if (netObj.IsOwner)
        {
            if (rb != null) 
            {
                rb.isKinematic = true; // Fermiamo la fisica per il teletrasporto
                rb.linearVelocity = Vector3.zero;
            }

            piece.transform.SetPositionAndRotation(positions[i], Quaternion.Euler(0, rotations[i], 0));

            // Se hai un NetworkTransform, forziamo la sincronizzazione immediata
            var nt = piece.GetComponent<NetworkTransform>();
            if (nt != null)
            {
                nt.Teleport(positions[i], Quaternion.Euler(0, rotations[i], 0), piece.transform.localScale);
            }
        }

        if (piece.GetComponent<MeshRenderer>() != null) {
            piece.GetComponent<MeshRenderer>().enabled = true;
        }
    }
}

[Rpc(SendTo.Server)]
public void RequestOwnershipServerRpc(NetworkObjectReference targetNetObj, ulong requesterId)
{
    if (targetNetObj.TryGet(out NetworkObject netObj))
    {
        // Il server sposta la proprietà della pedina a chi l'ha appena toccata
        netObj.ChangeOwnership(requesterId);
    }
}
    // ─────────────────────────────────────────────
    // GAME FLOW
    // ─────────────────────────────────────────────

    public void StartGame()
    {
        if (IsServer)
        {
            bool hasTimer = false;
            if (SessionData.turnTimerValue > 0)
            {
                isTurnTimer = true;
                TimerX.Value = SessionData.turnTimerValue;
                TimerO.Value = SessionData.turnTimerValue;
                hasTimer = true;
            }
            else if (SessionData.gameTimerValue > 0)
            {
                isTurnTimer = false;
                TimerX.Value = SessionData.gameTimerValue;
                TimerO.Value = SessionData.gameTimerValue;
                hasTimer = true;
            }

            IsTimerActive.Value = hasTimer;
            timerRunning = hasTimer;
        }

        startingTurnIndex = 0;
        CurrentTurnIndex.Value = startingTurnIndex;
        IsGameOver.Value = false;
        Board.ResetBoard();
        RestartGameVisualsRpc();
    }

    public void RequestRematch()
    {
        if (!IsServer) return;

        timerRunning = false;
        Board.ResetBoard();

        if (IsChaosMode.Value) StartCoroutine(ApplyChaosLayoutDelayed());

        if (lastGameWinner == CellState.X) startingTurnIndex = 1;
        else if (lastGameWinner == CellState.O) startingTurnIndex = 0;
        else startingTurnIndex = (startingTurnIndex == 0) ? 1 : 0;

        LivesX.Value = MaxLives.Value;
        LivesO.Value = MaxLives.Value;

        ResetTimers();
        timerRunning = IsTimerActive.Value;
        CurrentTurnIndex.Value = startingTurnIndex;
        IsGameOver.Value = false;
        RestartGameVisualsRpc();
    }

    private void ResetTimers()
    {
        if (SessionData.turnTimerValue > 0) { TimerX.Value = SessionData.turnTimerValue; TimerO.Value = SessionData.turnTimerValue; }
        else if (SessionData.gameTimerValue > 0) { TimerX.Value = SessionData.gameTimerValue; TimerO.Value = SessionData.gameTimerValue; }
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void RestartGameVisualsRpc() => OnGameRestarted?.Invoke();

    [Rpc(SendTo.Server)]
    public void SetPlayerReadyRpc(ulong clientId)
    {
        if (clientId == NetworkManager.ServerClientId) HostReady.Value = true;
        else ClientReady.Value = true;

        if (HostReady.Value && ClientReady.Value && !IsGameStarted.Value)
        {
            TriggerCountdownRpc();
            StartCoroutine(WaitCountdownAndStartGame());
        }
    }

    private System.Collections.IEnumerator WaitCountdownAndStartGame()
    {
        yield return new WaitForSeconds(3.5f);
        IsGameStarted.Value = true;
        if (IsServer) SpawnPlayerPointers();
        StartGame();
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void TriggerCountdownRpc() => OnCountdownStarted?.Invoke();

    [Rpc(SendTo.Server)]
    public void SetPauseRpc(bool pauseState) => IsPaused.Value = pauseState;

    // ─────────────────────────────────────────────
    // UPDATE / TIMER
    // ─────────────────────────────────────────────

    private void Update()
    {
        IsMyTurnLocal = (CurrentTurnIndex.Value == 0 && IsHost) || (CurrentTurnIndex.Value == 1 && !IsHost);

        if (IsGameStarted.Value && !IsGameOver.Value && IsMyTurnLocal)
            HandlePauseLogic();

        if (!IsServer || !timerRunning || IsGameOver.Value || IsPaused.Value) return;

        if (CurrentTurnIndex.Value == 0)
        {
            TimerX.Value -= Time.deltaTime;
            if (TimerX.Value <= 0) HandleTimerExpired(0);
        }
        else
        {
            TimerO.Value -= Time.deltaTime;
            if (TimerO.Value <= 0) HandleTimerExpired(1);
        }
    }

    private void HandlePauseLogic()
{
    var hands = MediapipeBridge.Instance.GetProcessedHands();
    bool handVisible = (hands != null) && (hands[TypeOfHand.Left].handVisible || hands[TypeOfHand.Right].handVisible);

    // CONTROLLO MOUSE (Debug): Verifichiamo se il mouse si è mosso in questo frame
    // Usiamo GetAxis per rilevare il movimento reale invece della posizione statica
    bool mouseMoving = Mathf.Abs(Input.GetAxis("Mouse X")) > 0.01f || Mathf.Abs(Input.GetAxis("Mouse Y")) > 0.01f;

    // Il giocatore è considerato "presente" se c'è una mano visibile O se muove il mouse
    bool isUserActive = handVisible || mouseMoving;

    if (!isUserActive && pauseCoroutine == null && !IsPaused.Value)
    {
        pauseCoroutine = StartCoroutine(WaitAndPauseRoutine());
    }
    else if (isUserActive)
    {
        // Se l'utente torna attivo, cancelliamo il countdown della pausa o togliamo il gioco dalla pausa
        if (pauseCoroutine != null) 
        { 
            StopCoroutine(pauseCoroutine); 
            pauseCoroutine = null; 
        }
        
        if (IsPaused.Value) SetPauseRpc(false);
    }
}

    private void HandleTimerExpired(int playerIndex)
    {
        if (isTurnTimer)
        {
            CurrentTurnIndex.Value = (CurrentTurnIndex.Value == 0) ? 1 : 0;
            TimerX.Value = SessionData.turnTimerValue;
            TimerO.Value = SessionData.turnTimerValue;
        }
        else
        {
            IsGameOver.Value = true;
            timerRunning = false;
            CellState winner = (playerIndex == 0) ? CellState.O : CellState.X;
            lastGameWinner = winner;
            if (winner == CellState.X) XWins.Value++; else OWins.Value++;
            GameEndedRpc((int)winner);
        }
    }

    private System.Collections.IEnumerator WaitAndPauseRoutine()
    {
        yield return new WaitForSeconds(2.0f);
        SetPauseRpc(true);
        pauseCoroutine = null;
    }

    private void SpawnPlayerPointers()
    {
        if (!IsServer) return;
        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            GameObject ptr = Instantiate(pointerPrefab);
            ptr.GetComponent<NetworkObject>().SpawnWithOwnership(client.ClientId);
        }
    }

    // ─────────────────────────────────────────────
    // MOSSE
    // ─────────────────────────────────────────────

    [Rpc(SendTo.Server)]
public void PlayMoveRpc(int cellIndex, CellState markOfPiece) // Aggiungiamo il tipo di pedina
{
    if (!IsGameStarted.Value || IsGameOver.Value) return;

    // Usiamo markOfPiece (il colore reale del pezzo) invece di CurrentTurnIndex
    if (Board.TryMakeMove(cellIndex, markOfPiece))
    {
        UpdateVisualsRpc(cellIndex, (int)markOfPiece);
        CheckGameState();
    }
}

    [Rpc(SendTo.ClientsAndHost)]
    private void UpdateVisualsRpc(int cellIndex, int markIndex) => OnMoveMade?.Invoke(cellIndex, (CellState)markIndex);

    private void CheckGameState()
    {
        CellState winner = Board.CheckWinner();
        if (winner != CellState.Empty)
        {
            lastGameWinner = winner;
            IsGameOver.Value = true;
            if (winner == CellState.X) XWins.Value++; else OWins.Value++;
            GameEndedRpc((int)winner);
        }
        else if (Board.IsBoardFull())
        {
            lastGameWinner = CellState.Empty;
            IsGameOver.Value = true;
            Draws.Value++;
            GameEndedRpc((int)CellState.Empty);
        }
        else
        {
            CurrentTurnIndex.Value = CurrentTurnIndex.Value == 0 ? 1 : 0;
            if (IsServer && isTurnTimer) ResetTimers();
        }
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void GameEndedRpc(int winnerIndex) => OnGameEnded?.Invoke((CellState)winnerIndex);

    // ─────────────────────────────────────────────
    // VITE / ERRORI
    // ─────────────────────────────────────────────

    [Rpc(SendTo.Server)]
    public void WrongMoveRpc(ulong clientId)
    {
        if (!IsGameStarted.Value || IsGameOver.Value) return;

        if (clientId == NetworkManager.ServerClientId) // Host (X)
        {
            if (LivesX.Value > 0) { LivesX.Value--; Debug.Log($"X ha sbagliato! Vite rimaste: {LivesX.Value}"); }
            if (LivesX.Value <= 0)
            {
                Debug.Log("X ha finito le vite! O vince.");
                lastGameWinner = CellState.O;
                IsGameOver.Value = true;
                OWins.Value++;
                GameEndedRpc((int)CellState.O);
            }
        }
        else // Client (O)
        {
            if (LivesO.Value > 0) { LivesO.Value--; Debug.Log($"O ha sbagliato! Vite rimaste: {LivesO.Value}"); }
            if (LivesO.Value <= 0)
            {
                Debug.Log("O ha finito le vite! X vince.");
                lastGameWinner = CellState.X;
                IsGameOver.Value = true;
                XWins.Value++;
                GameEndedRpc((int)CellState.X);
            }
        }
    }

    // ─────────────────────────────────────────────
    // UTILITY / DEBUG
    // ─────────────────────────────────────────────

    private void ClientDisconnected(ulong clientId)
    {
        if (!isExiting) OnOpponentDisconnected?.Invoke();
    }

    private void OnTurnChanged(int prev, int curr) => Debug.Log("Turno: " + (curr == 0 ? "X" : "O"));
    private void OnGameOverChanged(bool prev, bool curr) { if (curr) Debug.Log("Partita finita!"); }

    

    public void LeaveGame()
{
    isExiting = true;

    // 1. Spegnimento forzato della rete
    if (NetworkManager.Singleton != null)
    {
        // Shutdown pulisce i client collegati e ferma i trasporti
        NetworkManager.Singleton.Shutdown();
    }

    // 2. IMPORTANTE: Reset del time scale (se per caso era in pausa)
    Time.timeScale = 1f;

    SessionData.ResetToDefaults();

    // 3. Caricamento scena standard
    // Usa il nome esatto che hai nei Build Settings
    SceneManager.LoadScene("Menu"); 
}
} 