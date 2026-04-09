using System;
using System.Collections; // Necessario per la Coroutine (IEnumerator)
using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    [SerializeField] private GameObject MainMenu;
    [SerializeField] private GameObject HostMenu;
    [SerializeField] private GameObject ClientMenu;

    [SerializeField] private GameObject ConnectionMenu;

    [SerializeField] private GameObject GeneratingCodeMenu;


    [SerializeField] private Button copyCodeButton;
    [SerializeField] private TMPro.TMP_Text joinCodeText;
    [SerializeField] private Button joinButton;

    [SerializeField] private Sprite copiedSprite; // Trascina qui l'immagine "COPIED!"

    private string _lastGeneratedCode; // Variabile di supporto per sapere cosa copiare

    private void Start()
    {
        MainMenu.SetActive(true);
        HostMenu.SetActive(false);
        ClientMenu.SetActive(false);
        ConnectionMenu.SetActive(false);

        GeneratingCodeMenu.SetActive(false);

        joinButton.onClick.AddListener(ShowClientMenu);
        
        // Colleghiamo il tasto copia alla funzione
        if (copyCodeButton != null)
        {
            copyCodeButton.onClick.AddListener(CopyCodeToClipboard);
        }

        
        ConnectionManager.Instance.OnHostClicked += ShowGeneratingCode;
        ConnectionManager.Instance.OnJoinCodeGenerated += ShowHostMenu;
        ConnectionManager.Instance.OnClientConnecting += ShowLoadingScreen;
        ConnectionManager.Instance.OnClientConnectionFailed += HideLoadingScreen;


    }

    private void ShowHostMenu(string joinCode)

    {
        _lastGeneratedCode = joinCode; // Memorizziamo il codice
        GeneratingCodeMenu.SetActive(false);
        MainMenu.SetActive(false);
        HostMenu.SetActive(true);
        joinCodeText.text = joinCode;
    }

    private void ShowClientMenu()
    {
        MainMenu.SetActive(false);
        ClientMenu.SetActive(true);
    }

    private void ShowLoadingScreen()
    {
        MainMenu.SetActive(false);
        HostMenu.SetActive(false);
        ClientMenu.SetActive(false);
        ConnectionMenu.SetActive(true);
    }

    private void ShowGeneratingCode()
    {
        MainMenu.SetActive(false);
        HostMenu.SetActive(false);
        ClientMenu.SetActive(false);
        GeneratingCodeMenu.SetActive(true);
    }

    private void HideLoadingScreen()

    {
        
        ConnectionMenu.SetActive(false);
        // Opzionale: qui potresti anche mostrare un testo di errore "Connessione Fallita!"
    }

    // --- FUNZIONE DI COPIA ---
    private void CopyCodeToClipboard()
    {
        if (!string.IsNullOrEmpty(_lastGeneratedCode))
        {
            // Copia nel "cassetto" del sistema operativo
            GUIUtility.systemCopyBuffer = _lastGeneratedCode;
            
            // Avviamo un piccolo feedback visivo sul bottone
            StartCoroutine(CopyFeedbackRoutine());
        }
    }

    private IEnumerator CopyFeedbackRoutine()
{
    Image btnImage = copyCodeButton.GetComponent<Image>();
    if (btnImage == null) yield break;

    // Salviamo lo sprite originale ("COPY")
    Sprite originalSprite = btnImage.sprite;
    
    // Mettiamo lo sprite di feedback ("COPIED!")
    btnImage.sprite = copiedSprite;
    
    yield return new WaitForSeconds(2f); // Aspetta 2 secondi
    
    // Ripristiniamo lo sprite originale
    btnImage.sprite = originalSprite;
}
}