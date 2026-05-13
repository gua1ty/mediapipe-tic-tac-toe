using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening; 

public class GameUIManager : NetworkBehaviour
{
    [Header("UI References")]
    [SerializeField] private Image turnDisplayImage;
    [SerializeField] private Image localTurn;
    [SerializeField] private Sprite yourTurnRed;
    [SerializeField] private Sprite yourTurnBlue;
    [SerializeField] private Sprite waitTurn;
    
    [Header("Buttons")]
    [SerializeField] private Button rematchButton;
    [SerializeField] private Button quitButton;
    [SerializeField] private Button disconnectedReturnToMenu;

    [SerializeField] private GameObject disconnectedPanel;

    [Header("Turn Sprites")]
    [SerializeField] private Sprite spriteTurnoXRed;
    [SerializeField] private Sprite spriteTurnXGrey;
    [SerializeField] private Sprite spriteTurnOBlue;
    [SerializeField] private Sprite spriteTurnOGrey;
    [SerializeField] private Sprite spriteWinX;
    [SerializeField] private Sprite spriteWinO;
    [SerializeField] private Sprite spriteDraw;

    [Header("Stats")]
    [SerializeField] private TMP_Text XWinsText;
    [SerializeField] private TMP_Text OWinsText;
    [SerializeField] private TMP_Text DrawsText;

    [Header("Blur")]
    public Image blurImage;          
    public Sprite redBlurSprite;     
    public Sprite blueBlurSprite;    

    [Header("Timer UI")]
    [SerializeField] private TMP_Text timerTextX;
    [SerializeField] private TMP_Text timerTextO;

    [Header("Vite (Cuori) Giocatore X")]
    [SerializeField] private GameObject heartsContainerX; 
    [SerializeField] private Image[] heartImagesX; 

    [Header("Vite (Cuori) Giocatore O")]
    [SerializeField] private GameObject heartsContainerO; 
    [SerializeField] private Image[] heartImagesO; 

    [Header("Grafica Cuori X (Host)")]
    [SerializeField] private Sprite fullHeartSpriteX;  
    [SerializeField] private Sprite emptyHeartSpriteX; 

    [Header("Grafica Cuori O (Client)")]
    [SerializeField] private Sprite fullHeartSpriteO;  
    [SerializeField] private Sprite emptyHeartSpriteO;

    [Header("Pre-Game Attesa")]
    [SerializeField] private GameObject waitingPanel; 
    [SerializeField] private Image waitingStatusImage; // La tua grafica Figma
    [SerializeField] private Sprite spriteInquadraMano; 
    [SerializeField] private Sprite spriteAttendi; 
    [SerializeField] private Button forceReadyButton;

    [SerializeField] private Image instructionImage; // La tua grafica Figma

    [SerializeField] private Sprite instructionSpriteHost; // La tua grafica Figma
    [SerializeField] private Sprite instructionSpriteClient; // La tua grafica Figma



    [SerializeField] private TMP_Text countdownText; // Il testo puro per fare 3, 2, 1

    public void Awake() 
    {
        if (localTurn != null) localTurn.gameObject.SetActive(false);
        if (disconnectedPanel != null) disconnectedPanel.SetActive(false);
    }

    public override void OnNetworkSpawn()
    {
        // Stato iniziale
        UpdateTurnDisplay(GameManager.Instance.CurrentTurnIndex.Value);

        // Eventi GameManager
        GameManager.Instance.OnGameRestarted += ResetToTurnGraphics;
        GameManager.Instance.OnOpponentDisconnected += ShowDisconnectedWarning;
        GameManager.Instance.OnGameEnded += ShowResult;

        GameManager.Instance.CurrentTurnIndex.OnValueChanged += RefreshUI;
        GameManager.Instance.IsGameStarted.OnValueChanged += RefreshUI;

        GameManager.Instance.LivesX.OnValueChanged += OnLivesXChanged;
        GameManager.Instance.LivesO.OnValueChanged += OnLivesOChanged;

        // Imposta i cuori iniziali appena si entra (spegne quelli in più)
        SetupInitialHearts(GameManager.Instance.MaxLives.Value);
        
        // Iscrizione all'evento del Countdown
        GameManager.Instance.OnCountdownStarted += PlayCountdownAnimation;

        // Listener Bottoni
        if (quitButton != null) quitButton.onClick.AddListener(OnQuitButtonClicked);
        if (disconnectedReturnToMenu != null) disconnectedReturnToMenu.onClick.AddListener(OnQuitButtonClicked);
        if (rematchButton != null)
        {
            rematchButton.gameObject.SetActive(false);
            rematchButton.onClick.AddListener(OnRematchButtonClicked);
        }

        // Variabili di Rete
        GameManager.Instance.CurrentTurnIndex.OnValueChanged += OnTurnVariableChanged;
        GameManager.Instance.XWins.OnValueChanged += OnScoreChanged;
        GameManager.Instance.OWins.OnValueChanged += OnScoreChanged;
        GameManager.Instance.Draws.OnValueChanged += OnScoreChanged;
        
        GameManager.Instance.IsPaused.OnValueChanged += ShowPauseScreen;
        
        // Iscrizione ai Timer (Stile Scacchi)
        GameManager.Instance.TimerX.OnValueChanged += OnTimerXChanged;
        GameManager.Instance.TimerO.OnValueChanged += OnTimerOChanged;

        UpdateScoreDisplay();
        UpdateTimerDisplays(); 
        
        // --- LOGICA WAITING ROOM INIZIALE ---
        if (waitingPanel != null) waitingPanel.SetActive(true);
        if (countdownText != null) countdownText.text = ""; 
        if (instructionImage != null) instructionImage.gameObject.SetActive(false);

        if (waitingStatusImage != null)
        {
            waitingStatusImage.gameObject.SetActive(true);
            waitingStatusImage.sprite = spriteInquadraMano; 
        }

        if (forceReadyButton != null)
        {
            forceReadyButton.onClick.AddListener(OnForceReadyClicked);
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
            GameManager.Instance.TimerX.OnValueChanged -= OnTimerXChanged;
            GameManager.Instance.TimerO.OnValueChanged -= OnTimerOChanged;
            GameManager.Instance.IsPaused.OnValueChanged -= ShowPauseScreen;
            GameManager.Instance.CurrentTurnIndex.OnValueChanged -= RefreshUI;
            GameManager.Instance.IsGameStarted.OnValueChanged -= RefreshUI;

            
            GameManager.Instance.OnCountdownStarted -= PlayCountdownAnimation;
        }
    }

    private void RefreshUI(bool previous, bool current) => RefreshUI();
private void RefreshUI(int previous, int current) => RefreshUI();

private void RefreshUI()
{
    // Chiama la tua funzione passandogli il valore attuale del turno
    UpdateTurnDisplay(GameManager.Instance.CurrentTurnIndex.Value);
}


    private void Update()
    {
        // Se la partita è già iniziata o la rete non c'è, non fare nulla
        if (GameManager.Instance == null || NetworkManager.Singleton == null || GameManager.Instance.IsGameStarted.Value) return;

        // Scopriamo se noi abbiamo già dato l'OK
        ulong myClientId = NetworkManager.Singleton.LocalClientId;
        bool amIReady = (myClientId == NetworkManager.ServerClientId) 
                        ? GameManager.Instance.HostReady.Value 
                        : GameManager.Instance.ClientReady.Value;

        // Se non abbiamo ancora dato l'OK, "spiamo" MediaPipe
        if (!amIReady && MediapipeBridge.Instance != null)
        {
            var hands = MediapipeBridge.Instance.GetProcessedHands();
            if (hands != null)
            {
                // Se MediaPipe vede almeno una mano...
                if (hands[TypeOfHand.Left].handVisible || hands[TypeOfHand.Right].handVisible)
                {
                    OnForceReadyClicked(); 
                }
            }
        }
    }


    private void OnForceReadyClicked()
    {
        if (forceReadyButton != null) forceReadyButton.gameObject.SetActive(false);
        
        // SWAP DELLO SPRITE FIGMA!
        if (waitingStatusImage != null) waitingStatusImage.sprite = spriteAttendi;

        ulong myClientId = NetworkManager.Singleton.LocalClientId;
        GameManager.Instance.SetPlayerReadyRpc(myClientId);
    }

    private void PlayCountdownAnimation()
    {
        if (countdownText == null) return;



        if (NetworkManager.Singleton.IsServer)

        
        {
            instructionImage.sprite = instructionSpriteHost;
        }

        else
        {
            instructionImage.sprite = instructionSpriteClient;

        }

        
        instructionImage.gameObject.SetActive(true);


        // Nascondiamo l'immagine Figma e il bottone, ora servono solo i numeri!
        if (waitingStatusImage != null) waitingStatusImage.gameObject.SetActive(false);
        if (forceReadyButton != null) forceReadyButton.gameObject.SetActive(false);

        // Prepariamo la sequenza DoTween
        Sequence countdownSeq = DOTween.Sequence();

        countdownSeq.AppendCallback(() => { countdownText.text = "3"; countdownText.transform.localScale = Vector3.one * 1.5f; });
        countdownSeq.Append(countdownText.transform.DOScale(1f, 0.5f).SetEase(Ease.OutBounce));
        countdownSeq.AppendInterval(0.5f);

        countdownSeq.AppendCallback(() => { countdownText.text = "2"; countdownText.transform.localScale = Vector3.one * 1.5f; });
        countdownSeq.Append(countdownText.transform.DOScale(1f, 0.5f).SetEase(Ease.OutBounce));
        countdownSeq.AppendInterval(0.5f);

        countdownSeq.AppendCallback(() => { countdownText.text = "1"; countdownText.transform.localScale = Vector3.one * 1.5f; });
        countdownSeq.Append(countdownText.transform.DOScale(1f, 0.5f).SetEase(Ease.OutBounce));
        countdownSeq.AppendInterval(0.5f);

        countdownSeq.AppendCallback(() => { 
            countdownText.text = "VIA!"; 
            countdownText.transform.localScale = Vector3.one * 2f; 
        });
        countdownSeq.Append(countdownText.transform.DOScale(1f, 0.5f));
        
        countdownSeq.OnComplete(() => {
            if (waitingPanel != null) waitingPanel.SetActive(false);
            countdownText.color = Color.white; 
        });
    }

    // --- LOGICA TIMER ---
    private void OnTimerXChanged(float prev, float current) => UpdateTimerDisplays();
    private void OnTimerOChanged(float prev, float current) => UpdateTimerDisplays();

    private void UpdateTimerDisplays()
    {
        if (!GameManager.Instance.IsTimerActive.Value)
        {
            if (timerTextX != null) timerTextX.gameObject.SetActive(false);
            if (timerTextO != null) timerTextO.gameObject.SetActive(false);
            return;
        }

        int currentTurn = GameManager.Instance.CurrentTurnIndex.Value;

        float timeX = GameManager.Instance.TimerX.Value;
        if (timerTextX != null)
        {
            timerTextX.gameObject.SetActive(true);
            timerTextX.text = FormatTime(timeX);
            timerTextX.color = (currentTurn == 0) ? (timeX <= 10f ? Color.red : Color.white) : Color.gray;
        }

        float timeO = GameManager.Instance.TimerO.Value;
        if (timerTextO != null)
        {
            timerTextO.gameObject.SetActive(true);
            timerTextO.text = FormatTime(timeO);
            timerTextO.color = (currentTurn == 1) ? (timeO <= 10f ? Color.red : Color.white) : Color.gray;
        }
    }

    private string FormatTime(float time)
    {
        int minutes = Mathf.FloorToInt(Mathf.Max(0, time) / 60);
        int seconds = Mathf.FloorToInt(Mathf.Max(0, time) % 60);
        return string.Format("{0}:{1:00}", minutes, seconds);
    }

    // --- LOGICA TURNI E GRAFICA ---
    private void OnTurnVariableChanged(int previousValue, int newValue)
    {
        UpdateTurnDisplay(newValue);
        UpdateTimerDisplays(); 
    }

    private void OnScoreChanged(int previousValue, int newValue) => UpdateScoreDisplay();

    private void UpdateScoreDisplay()
    {
        if (XWinsText != null) XWinsText.text = $"{GameManager.Instance.XWins.Value}";
        if (OWinsText != null) OWinsText.text = $"{GameManager.Instance.OWins.Value}";
        if (DrawsText != null) DrawsText.text = $"{GameManager.Instance.Draws.Value}";
    }

    private void UpdateTurnDisplay(int newTurnIndex)
    {
        blurImage.DOKill();
        blurImage.rectTransform.DOKill();
        if (localTurn != null) localTurn.rectTransform.DOKill(); 

        if (newTurnIndex == 0)
        {
            turnDisplayImage.sprite = IsServer ? spriteTurnoXRed : spriteTurnXGrey;
        }
        else
        {
            turnDisplayImage.sprite = IsServer ? spriteTurnOGrey : spriteTurnOBlue;
        }

        // --- NUOVO CONTROLLO: Se siamo in attesa o nel 3-2-1, non far pulsare niente! ---
        if (GameManager.Instance == null || !GameManager.Instance.IsGameStarted.Value)
        {
            if (localTurn != null) localTurn.gameObject.SetActive(false);
            blurImage.color = new Color(1, 1, 1, 0); // Nasconde il bagliore istantaneamente
            return; // Esce dalla funzione senza far partire le animazioni
        }
        // ---------------------------------------------------------------------------------

        bool isMyTurn = (IsServer && newTurnIndex == 0) || (!IsServer && newTurnIndex == 1);

        if(localTurn != null) localTurn.gameObject.SetActive(true);

        if(isMyTurn)
    {
        // 1. Fermiamo ogni animazione precedente
        localTurn.DOKill();
        blurImage.DOKill();
        
        // 2. Reset Opacità (Tornano visibili all'istante)
        localTurn.color = Color.white;
        blurImage.color = Color.white;

        // Reset scala solo del testo (perché sappiamo che è 0.128)
        // NON tocchiamo la scala di blurImage così rimane quella dell'Inspector
        float baseScale = 0.128f;
    localTurn.rectTransform.localScale = new Vector3(baseScale, baseScale, 1f);

    blurImage.sprite = IsServer ? redBlurSprite : blueBlurSprite;
    localTurn.sprite = IsServer ? yourTurnRed : yourTurnBlue;

    // 3. NUOVA ANIMAZIONE: Pulsazione infinita solo per il TESTO
    // La scritta ingrandisce leggermente e torna indietro senza mai dissolversi
    localTurn.rectTransform.DOScale(new Vector3(baseScale * 1.1f, baseScale * 1.1f, 1f), 1.2f)
        .SetEase(Ease.InOutSine)
        .SetLoops(-1, LoopType.Yoyo);

    // 4. LOGICA BLUR ORIGINALE: Resta 1 secondo e poi sfuma
    Sequence blurSequence = DOTween.Sequence();
    blurSequence.AppendInterval(1.0f);
    blurSequence.Append(blurImage.DOFade(0f, 2.0f).SetEase(Ease.InQuad));
    }
        else
        {
            localTurn.sprite = waitTurn;
            localTurn.rectTransform.localScale = new Vector3(0.128f, 0.128f, 1f);
            blurImage.DOFade(0f, 0.5f);
        }
    }

    private void ShowResult(CellState winner)
    {
        if (blurImage != null)
        {
            blurImage.DOKill();
            blurImage.rectTransform.DOKill();
            blurImage.DOFade(0f, 0.5f);
        }

        if (winner == CellState.X) turnDisplayImage.sprite = spriteWinX;
        else if (winner == CellState.O) turnDisplayImage.sprite = spriteWinO;
        else turnDisplayImage.sprite = spriteDraw;

        if (IsServer && rematchButton != null) rematchButton.gameObject.SetActive(true);
        if (localTurn != null) localTurn.gameObject.SetActive(false);

        if (timerTextX != null) timerTextX.gameObject.SetActive(false);
        if (timerTextO != null) timerTextO.gameObject.SetActive(false);
    }

    private void ShowDisconnectedWarning()
    {
       if (disconnectedPanel != null) disconnectedPanel.SetActive(true);
    }

    private void OnQuitButtonClicked()
    {
        if (GameManager.Instance != null) GameManager.Instance.LeaveGame();
    }

    private void OnRematchButtonClicked() => GameManager.Instance.RequestRematch();

    private void ResetToTurnGraphics()
    {
        UpdateTurnDisplay(GameManager.Instance.CurrentTurnIndex.Value);
        if (rematchButton != null) rematchButton.gameObject.SetActive(false);
        UpdateTimerDisplays();
    }

    private void OnLivesXChanged(int prev, int current) => UpdateHeartsDisplay(heartImagesX, current, GameManager.Instance.MaxLives.Value, fullHeartSpriteX, emptyHeartSpriteX);
    private void OnLivesOChanged(int prev, int current) => UpdateHeartsDisplay(heartImagesO, current, GameManager.Instance.MaxLives.Value, fullHeartSpriteO, emptyHeartSpriteO);

    private void SetupInitialHearts(int maxLives)
    {
        bool showHearts = maxLives > 0;
        
        if (heartsContainerX != null) heartsContainerX.SetActive(showHearts);
        if (heartsContainerO != null) heartsContainerO.SetActive(showHearts);

        if (!showHearts) return;

        // Spegne i cuori "di troppo" e usa gli sprite PIENI specifici per X e O
        for (int i = 0; i < 3; i++) // 3 è il massimo di cuori
        {
            if (i < maxLives)
            {
                if (i < heartImagesX.Length) { heartImagesX[i].gameObject.SetActive(true); heartImagesX[i].sprite = fullHeartSpriteX; }
                if (i < heartImagesO.Length) { heartImagesO[i].gameObject.SetActive(true); heartImagesO[i].sprite = fullHeartSpriteO; }
            }
            else
            {
                if (i < heartImagesX.Length) heartImagesX[i].gameObject.SetActive(false);
                if (i < heartImagesO.Length) heartImagesO[i].gameObject.SetActive(false);
            }
        }
    }

    // Ora questo metodo accetta anche il fullSprite e l'emptySprite da usare!
    private void UpdateHeartsDisplay(Image[] playerHearts, int currentLives, int maxLives, Sprite fullSprite, Sprite emptySprite)
    {
        if (maxLives <= 0) return;

        for (int i = 0; i < maxLives; i++)
        {
            if (i < playerHearts.Length)
            {
                playerHearts[i].sprite = (i < currentLives) ? fullSprite : emptySprite;
            }
        }
    }

    private void ShowPauseScreen(bool previousValue, bool newValue)
    {

        countdownText.gameObject.SetActive(false);
        instructionImage.gameObject.SetActive(false);


        if (newValue){

        if (waitingPanel != null) waitingPanel.SetActive(true);


        if (GameManager.Instance.IsMyTurnLocal)
        {
            waitingStatusImage.gameObject.SetActive(true);
            waitingStatusImage.sprite = spriteInquadraMano; 
        }

        else 
        {
            waitingStatusImage.gameObject.SetActive(true);
            waitingStatusImage.sprite = spriteAttendi;
        }
        }
        else
        {
            
            if (waitingPanel != null) waitingPanel.SetActive(false);

        }        
    }

}