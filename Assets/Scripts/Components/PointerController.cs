using Unity.Netcode;
using UnityEngine;

public class PointerController : NetworkBehaviour
{
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Header("Sprites")]
    public Sprite hostIdle; public Sprite hostPinch;
    public Sprite clientIdle; public Sprite clientPinch;

    [Header("Dimensioni e Fluidità")]
    public float scaleIdle = 0.05f;
    public float scalePinch = 0.08f;
    public float lerpSpeed = 15f; // Più alto è, più è reattivo

    [Header("Posizionamento")]
    public float handZOffset = 2.0f;

    private NetworkVariable<bool> isPinchingNet = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    private HandGrabber localGrabber;
    private Transform indexJoint;
    private Transform thumbJoint;
    private Camera refCamera;

    public override void OnNetworkSpawn()
    {
        refCamera = Camera.main;
        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        UpdateSprite(false);
    }

    void Update()
    {
        if (refCamera == null) refCamera = Camera.main;
        transform.rotation = refCamera.transform.rotation;

        if (IsOwner)
        {
            HandleOwnerLogic();
            spriteRenderer.enabled = false; // Io non vedo il mio
        }
        else
        {
            UpdateOpponentVisuals();
        }
    }

    private void HandleOwnerLogic()
    {
        // 1. Cerchiamo i riferimenti se mancano
        if (localGrabber == null) localGrabber = Object.FindFirstObjectByType<HandGrabber>();
        
        // Cerchiamo i Joint per la precisione millimetrica della posizione
        if (indexJoint == null) indexJoint = GameObject.Find("Joint_8")?.transform;
        if (thumbJoint == null) thumbJoint = GameObject.Find("Joint_4")?.transform;

        // 2. Sincronizziamo lo stato del pinch dal Grabber
        if (localGrabber != null)
        {
            isPinchingNet.Value = localGrabber.isPinching;
        }

        // 3. Muoviamo il puntatore con la logica del punto medio (PRECISIONE)
        MovePointerPrecise();
    }

    private void MovePointerPrecise()
    {
        Vector3 worldPosition;

        // Se abbiamo i Joint, calcoliamo il punto esatto tra le dita
        if (indexJoint != null && thumbJoint != null)
        {
            // La posizione è esattamente la media tra le due punte
            worldPosition = (indexJoint.position + thumbJoint.position) / 2f;
        }
        else
        {
            // Fallback al mouse se la mano non è vista
            worldPosition = refCamera.ScreenToWorldPoint(new Vector3(Input.mousePosition.x, Input.mousePosition.y, handZOffset));
        }

        // Proiettiamo nel Viewport e poi di nuovo nel World per mantenere la distanza Z fissa (effetto HandRenderer)
        Vector3 vPos = refCamera.WorldToScreenPoint(worldPosition);
        Vector3 viewportPoint = refCamera.ScreenToViewportPoint(vPos);
        transform.position = refCamera.ViewportToWorldPoint(new Vector3(viewportPoint.x, viewportPoint.y, handZOffset));
    }

    private void UpdateOpponentVisuals()
    {
        if (spriteRenderer == null || GameManager.Instance == null) return;

        // Visibilità per turno
        int currentTurn = GameManager.Instance.CurrentTurnIndex.Value;
        spriteRenderer.enabled = (int)OwnerClientId == currentTurn && GameManager.Instance.IsGameStarted.Value;

        // --- TRANSIZIONE FLUIDA ---
        // Cambiamo lo sprite
        UpdateSprite(isPinchingNet.Value);

        // Animiamo la scala con un Lerp per evitare l'effetto brusco
        float targetSize = isPinchingNet.Value ? scalePinch : scaleIdle;
        // Vector3.Lerp crea quel movimento "morbido" che manca allo sprite swap
        transform.localScale = Vector3.Lerp(transform.localScale, Vector3.one * targetSize, Time.deltaTime * lerpSpeed);
    }

    private void UpdateSprite(bool p)
    {
        bool isHost = OwnerClientId == 0;
        Sprite targetSprite;

        if (isHost) targetSprite = p ? hostPinch : hostIdle;
        else targetSprite = p ? clientPinch : clientIdle;

        if (spriteRenderer.sprite != targetSprite)
        {
            spriteRenderer.sprite = targetSprite;
        }
    }
}