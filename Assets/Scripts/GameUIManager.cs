using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class GameUIManager : NetworkBehaviour
{

    [Header("UI References")]
    [SerializeField] private Image turnDisplayImage;

    [SerializeField] private Image localTurn;

    [SerializeField] private Sprite yourTurn;

    [SerializeField] private Sprite waitTurn;

    
    
     [Header("Buttons")]

    [SerializeField] private Button rematchButton;
    [SerializeField] private Button quitButton;
    
    
    [SerializeField] private Button disconnectedReturnToMenu;

    [SerializeField] private GameObject disconnectedPanel;


    [Header("Turn Sprites")]
    [SerializeField] private Sprite spriteTurnoX;
    [SerializeField] private Sprite spriteTurnoO;

    [SerializeField] private Sprite spriteWinX;
    [SerializeField] private Sprite spriteWinO;

    [SerializeField] private Sprite spriteDraw;

    [Header("Stats")]
    [SerializeField] private TMP_Text XWinsText;
    [SerializeField] private TMP_Text OWinsText;
    [SerializeField] private TMP_Text DrawsText;


    

    public void Awake() // <- Aggiungi questo!
    {
        // Spegniamo l'immagine di default appena la scena si carica
        // Così nessuno vede informazioni sbagliate mentre la rete si sta connettendo
        if (localTurn != null)
        {
            localTurn.gameObject.SetActive(false);
        }
    }



    public override void OnNetworkSpawn()
    {
        UpdateTurnDisplay(GameManager.Instance.CurrentTurnIndex.Value);

        GameManager.Instance.OnGameRestarted += ResetToTurnGraphics;

        GameManager.Instance.OnOpponentDisconnected += ShowDisconnectedWarning;

        if (quitButton != null)
            quitButton.onClick.AddListener(OnQuitButtonClicked);
            
        if (disconnectedReturnToMenu != null)
            disconnectedReturnToMenu.onClick.AddListener(OnQuitButtonClicked);

        GameManager.Instance.CurrentTurnIndex.OnValueChanged += OnTurnVariableChanged;

        GameManager.Instance.XWins.OnValueChanged += OnScoreChanged;
        GameManager.Instance.OWins.OnValueChanged += OnScoreChanged;
        GameManager.Instance.Draws.OnValueChanged += OnScoreChanged;
        

        UpdateScoreDisplay();


        GameManager.Instance.OnGameEnded += ShowResult;

        rematchButton.gameObject.SetActive(false);
            rematchButton.onClick.AddListener(OnRematchButtonClicked);


    }

    private void OnTurnVariableChanged(int previousValue, int newValue)
    {
        UpdateTurnDisplay(newValue);
    }

    private void OnScoreChanged(int previousValue, int newValue)
    {
        UpdateScoreDisplay(); // Aggiorna il testo a schermo!
    }

    private void UpdateScoreDisplay()
    {
        if (XWinsText != null) 
            XWinsText.text = $"{GameManager.Instance.XWins.Value}";
            
        if (OWinsText != null) 
            OWinsText.text = $"{GameManager.Instance.OWins.Value}";
            
        if (DrawsText != null) 
            DrawsText.text = $"{GameManager.Instance.Draws.Value}";
    }// Aggiorna il testo a schermo!


    private void UpdateTurnDisplay(int newTurnIndex){
        if (newTurnIndex == 0)
        {
            turnDisplayImage.sprite = spriteTurnoX;

        }

        else
        {
            turnDisplayImage.sprite = spriteTurnoO;
          
        }

        bool isMyTurn = false;

        if(IsServer && newTurnIndex == 0)
        {
            isMyTurn = true;
        }

        else if (!IsServer && newTurnIndex ==1)
        {
            isMyTurn = true;
        }

        if(localTurn != null)
        {
            localTurn.gameObject.SetActive(true);
        }

        if(isMyTurn)
        {
            localTurn.sprite = yourTurn;
        }

        else
        {
            localTurn.sprite = waitTurn;
        }


    }

        public override void OnNetworkDespawn()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.CurrentTurnIndex.OnValueChanged -= OnTurnVariableChanged;
            GameManager.Instance.OnGameEnded -= ShowResult;
            GameManager.Instance.OnGameRestarted -= ResetToTurnGraphics;
            GameManager.Instance.OnOpponentDisconnected -= ShowDisconnectedWarning;
        }

        if (!GameManager.Instance.isExiting)
            {
                ShowDisconnectedWarning();
            }


    }

    private void ShowDisconnectedWarning()
    {
       if (disconnectedPanel != null)
        {
            disconnectedPanel.SetActive(true);
        }
    }

    private void OnQuitButtonClicked()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.LeaveGame();
        }  }

    

    private void ShowResult(CellState winner)
    {
        if (winner == CellState.X)
        {
            turnDisplayImage.sprite = spriteWinX;
        }
        else if (winner == CellState.O)
        {
            turnDisplayImage.sprite = spriteWinO;
        }
        else // Pareggio (CellState.Empty)
        {
            turnDisplayImage.sprite = spriteDraw;
        }

        if (IsServer && rematchButton != null)
        {
            rematchButton.gameObject.SetActive(true);
        }

        if (localTurn != null)
        {
            localTurn.gameObject.SetActive(false);
        }


        
    }

    private void OnRematchButtonClicked()
    {
        Debug.Log("Bottone Rematch premuto dall'Host! Chiedo il reset...");
        GameManager.Instance.RequestRematch(); // Chiamiamo il GameManager!
    }

    private void ResetToTurnGraphics()
    {
        UpdateTurnDisplay(GameManager.Instance.CurrentTurnIndex.Value);
        
        if (rematchButton != null)
        {
            rematchButton.gameObject.SetActive(false); // Nasconde di nuovo il bottone
        }
    }
    

}