using UnityEngine;
using UnityEngine.UI;

public class SelectionMenuUIManager : MonoBehaviour


{

    [Header("Riferimenti Pannelli")]
    public GameObject initialMenuPanel;    // Il pannello con i tasti "Host" e "Client"
    public GameObject currentPanel;        // Il pannello dove si trova questo script (Selection o Lobby)
   
   
    [Header("Interfaccia Grafica (Bottoni 'No')")]
    
    public Toggle noTurnTimerToggle; 
    public Toggle noGameTimerToggle;
    

    // --- MODALITÀ ---
    public void SetMode_Normal(bool isOn) { if (isOn) { SessionData.gameMode = "Normal"; Debug.Log("⚙️ Modalità: Normal"); } }
    public void SetMode_Chaos(bool isOn)  { if (isOn) { SessionData.gameMode = "Chaos"; Debug.Log("⚙️ Modalità: Chaos"); } }

    // --- TEMPO PER MOSSA ---
    public void SetTurn_10s(bool isOn) { if (isOn) UpdateTurnTimer(10); }
    public void SetTurn_20s(bool isOn) { if (isOn) UpdateTurnTimer(20); }
    public void SetTurn_30s(bool isOn) { if (isOn) UpdateTurnTimer(30); }
    public void SetTurn_No(bool isOn)  { if (isOn) UpdateTurnTimer(0); }

    private void UpdateTurnTimer(int seconds)
    {
        SessionData.turnTimerValue = seconds;
        Debug.Log("⏱️ Mossa: " + seconds + "s");
        
        if (seconds > 0 && noGameTimerToggle != null)
        {
            noGameTimerToggle.isOn = true; 
        }
    }

    // --- TEMPO PER PARTITA ---
    public void SetGame_2m(bool isOn) { if (isOn) UpdateGameTimer(120); }
    public void SetGame_3m(bool isOn) { if (isOn) UpdateGameTimer(180); }
    public void SetGame_5m(bool isOn) { if (isOn) UpdateGameTimer(300); }
    public void SetGame_No(bool isOn) { if (isOn) UpdateGameTimer(0); }

    private void UpdateGameTimer(int seconds)
    {
        SessionData.gameTimerValue = seconds;
        Debug.Log("⏳ Partita: " + seconds + "s");
        
        if (seconds > 0 && noTurnTimerToggle != null)
        {
            noTurnTimerToggle.isOn = true; 
        }
    }

    // --- PENALITÀ ---
    public void SetLives_1(bool isOn)  { if (isOn) SessionData.maxErrors = 1; }
    public void SetLives_2(bool isOn)  { if (isOn) SessionData.maxErrors = 2; }
    public void SetLives_3(bool isOn)  { if (isOn) SessionData.maxErrors = 3; }
    public void SetLives_No(bool isOn) { if (isOn) SessionData.maxErrors = 0; }

    public void GoBack()
    {
        // 1. CONTROLLO RETE "INTELLIGENTE"
        // Se la rete è attiva (perché siamo il Client in attesa o l'Host ha già avviato)
        // allora spegniamo tutto. Se è già spenta, non succede nulla.

        // 2. CAMBIO PANNELLO
        if (currentPanel != null) currentPanel.SetActive(false);
        if (initialMenuPanel != null) initialMenuPanel.SetActive(true);
        
        Debug.Log("🔙 Ritorno al menu principale.");
    }
}