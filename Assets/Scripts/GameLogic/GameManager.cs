using System;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    // Pattern Singleton: permette di accedere al GameManager da qualsiasi altro script
    public static GameManager Instance { get; private set; }

    public Board GameBoard { get; private set; }
    public CellState CurrentTurn { get; private set; }
    public bool IsGameOver { get; private set; }

    // Pattern Observer (Eventi): avvisano la grafica e la UI senza "conoscerle" direttamente
    public event Action<int, CellState> OnMoveMade;
    public event Action<CellState> OnGameEnded; // Se passa CellState.Empty è un pareggio
    public event Action OnGameRestarted;

    private void Awake()
    {

        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        
        GameBoard = new Board();
    }

    private void Start()
    {
        StartGame();
    }

    public void StartGame()
    {
        GameBoard.ResetBoard();
        CurrentTurn = CellState.X; // Inizia sempre la X
        IsGameOver = false;
        
        // "Spara" l'evento per dire a tutti che il gioco è ricominciato
        OnGameRestarted?.Invoke();
        Debug.Log("Gioco Iniziato! Turno di: " + CurrentTurn);
    }

    // Questo è il metodo che chiamerà il Mouse (e in futuro MediaPipe!)
    public void PlayMove(int cellIndex)
    {
        if (IsGameOver) return;

        // Chiediamo alla scacchiera se la mossa è valida matematicamente
        if (GameBoard.TryMakeMove(cellIndex, CurrentTurn))
        {
            // Se la mossa è valida, avvisiamo la grafica di disegnare la X o la O
            OnMoveMade?.Invoke(cellIndex, CurrentTurn);

            CheckGameState();
        }
        else
        {
            Debug.LogWarning("Mossa non valida nella cella: " + cellIndex);
        }
    }

    private void CheckGameState()
    {
        CellState winner = GameBoard.CheckWinner();

        if (winner != CellState.Empty)
        {
            IsGameOver = true;
            Debug.Log("Partita finita! Ha vinto: " + winner);
            OnGameEnded?.Invoke(winner);
        }
        else if (GameBoard.IsBoardFull())
        {
            IsGameOver = true;
            Debug.Log("Partita finita! Pareggio!");
            OnGameEnded?.Invoke(CellState.Empty);
        }
        else
        {
            // Nessuno ha vinto, la griglia non è piena: si cambia turno!
            CurrentTurn = (CurrentTurn == CellState.X) ? CellState.O : CellState.X;
            Debug.Log("Turno passato. Ora tocca a: " + CurrentTurn);
        }
    }
}