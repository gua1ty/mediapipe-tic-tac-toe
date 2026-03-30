using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class GamUIManager : NetworkBehaviour
{

    [Header("UI References")]
    [SerializeField] private Image turnDisplayImage;
    [SerializeField] private Button rematchButton;

    [Header("Turn Sprites")]
    [SerializeField] private Sprite spriteTurnoX;
    [SerializeField] private Sprite spriteTurnoO;

    [SerializeField] private Sprite spriteWinX;
    [SerializeField] private Sprite spriteWinO;

    [SerializeField] private Sprite spriteDraw;



    public override void OnNetworkSpawn()
    {
        UpdateTurnDisplay(GameManager.Instance.CurrentTurnIndex.Value);

        GameManager.Instance.OnGameRestarted += ResetToTurnGraphics;

        GameManager.Instance.CurrentTurnIndex.OnValueChanged += OnTurnVariableChanged;

        GameManager.Instance.OnGameEnded += ShowResult;

        rematchButton.gameObject.SetActive(false);
            rematchButton.onClick.AddListener(OnRematchButtonClicked);


    }

    private void OnTurnVariableChanged(int previousValue, int newValue)
    {
        UpdateTurnDisplay(newValue);
    }

    private void UpdateTurnDisplay(int newTurnIndex){
        if (newTurnIndex == 0)
        {
            turnDisplayImage.sprite = spriteTurnoX;

        }

        else
        {
            turnDisplayImage.sprite = spriteTurnoO;
          
        }
    }

        public override void OnNetworkDespawn()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.CurrentTurnIndex.OnValueChanged -= OnTurnVariableChanged;
            GameManager.Instance.OnGameEnded -= ShowResult;
            GameManager.Instance.OnGameRestarted -= ResetToTurnGraphics;
        }


    }

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


        
    }

    private void OnRematchButtonClicked()
    {
        Debug.Log("Bottone Rematch premuto dall'Host! Chiedo il reset...");
        GameManager.Instance.RequestRematch(); // Chiamiamo il GameManager!
    }

    private void ResetToTurnGraphics()
    {
        UpdateTurnDisplay(0); // Rimette lo sprite del Turno X
        
        if (rematchButton != null)
        {
            rematchButton.gameObject.SetActive(false); // Nasconde di nuovo il bottone
        }
    }
    

}