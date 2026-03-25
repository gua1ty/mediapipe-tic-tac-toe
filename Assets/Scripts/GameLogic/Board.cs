using System;

public enum CellState
{
    Empty,
    X,
    O
}

public class Board
{
    
    public CellState[] Grid { get; private set; }

    public Board()
    {
        Grid = new CellState[9];
        ResetBoard();
    }

    public void ResetBoard()
    {
        for (int i = 0; i < Grid.Length; i++)
        {
            Grid[i] = CellState.Empty;
        }
    }

    public bool TryMakeMove(int index, CellState playerMark)
    {
        if (index < 0 || index >= 9 || Grid[index] != CellState.Empty)
        {
            return false; //Mossa non valida
        }

        Grid[index] = playerMark;
        return true; // Mossa accettata
    }

    public CellState CheckWinner()
    {
        // Le 8 combinazioni vincenti 
        int[,] winLines = new int[,]
        {
            {0, 1, 2}, {3, 4, 5}, {6, 7, 8}, // Righe orizzontali
            {0, 3, 6}, {1, 4, 7}, {2, 5, 8}, // Colonne verticali
            {0, 4, 8}, {2, 4, 6}             // Diagonali
        };

        for (int i = 0; i < 8; i++)
        {
            int a = winLines[i, 0];
            int b = winLines[i, 1];
            int c = winLines[i, 2];

            // Se la prima cella non è vuota ed è uguale alle altre due, abbiamo Tris
            if (Grid[a] != CellState.Empty && Grid[a] == Grid[b] && Grid[a] == Grid[c])
            {
                return Grid[a]; // Ritorna X oppure O
            }
        }

        return CellState.Empty; // Nessun vincitore per ora
    }

    public bool IsBoardFull()
    {
        foreach (var cell in Grid)
        {
            if (cell == CellState.Empty) return false;
        }
        return true;
    }
}