using Unity.Netcode;
using UnityEngine;

public class DraggablePiece : Grabbable
{
    [Header("Impostazioni Pezzo")]
    public CellState pieceType;

    private Vector3 startPosition;
    private Quaternion startRotation;
    private bool isDragged = false;
    private Vector3 currentTargetPosition;
    private Rigidbody rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;

        if (GetComponent<MeshRenderer>() != null) {
        GetComponent<MeshRenderer>().enabled = false;
    }
    }

    private void Start()
    {
        startPosition = transform.position;
        startRotation = transform.rotation;

        // NUOVO: Mi metto in ascolto dell'Arbitro (GameManager)
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameRestarted += ReturnToStart;
        }
    }

    private void Update()
    {
        if (isDragged)
        {
            transform.position = Vector3.Lerp(transform.position, currentTargetPosition, 15f * Time.deltaTime);
            transform.Rotate(Vector3.up, 90f * Time.deltaTime);
        }
    }

    public override void StartDrag()
{
    // --- NOVITÀ: GESTIONE AUTORITÀ (OWNERSHIP) ---
    // Se siamo in Chaos e non sono il proprietario di questa pedina...
    if (GameManager.Instance.IsChaosMode.Value && !GetComponent<NetworkObject>().IsOwner)
    {
        // Chiedo al Server di darmi la proprietà della pedina per poterla muovere
        GameManager.Instance.RequestOwnershipServerRpc(GetComponent<NetworkObject>(), NetworkManager.Singleton.LocalClientId);
    }

    isDragged = true;
    rb.useGravity = false;
    rb.isKinematic = true; 
    rb.linearVelocity = Vector3.zero;
}


    public override void UpdateTarget(Vector3 targetPosition)
    {
        currentTargetPosition = targetPosition;
    }

    public override void StopDrag()
{
    isDragged = false;
    rb.isKinematic = false; // <--- Torna a essere un oggetto fisico per cadere nella cella
    rb.useGravity = true;
    transform.rotation = Quaternion.identity;
    CheckDropLocation();
}

    public override bool IsPinchable()
{
    // 1. Il gioco è finito o non iniziato?
    if (GameManager.Instance.IsGameOver.Value || !GameManager.Instance.IsGameStarted.Value) return false;

    // 2. È il mio turno? (Usa la proprietà che abbiamo creato nel GameManager)
    if (!GameManager.Instance.IsMyTurnLocal) return false;

    // 3. È la mia pedina? 
    // In Modalità Chaos ignoriamo questo controllo!
    if (!GameManager.Instance.IsChaosMode.Value)
    {
        // Se NON è chaos, applichiamo il controllo classico del colore
        CellState myColor = NetworkManager.Singleton.IsHost ? CellState.X : CellState.O;
        if (pieceType != myColor) return false;
    }

    return true; // Se siamo in Chaos o se è il mio colore in Normal, procedi
}
    private void OnMouseDown()
    {
        // Ora il mouse chiede il permesso alla stessa funzione che userà la mano!
        if (IsPinchable()) 
        {
            StartDrag();
        }
    }
    private void OnMouseDrag()
    {
        Plane dragPlane = new Plane(Vector3.up, transform.position);
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (dragPlane.Raycast(ray, out float distance))
        {
            UpdateTarget(ray.GetPoint(distance));
        }
    }

    private void OnMouseUp()
    {
        StopDrag();
    }

    private void CheckDropLocation()
{
    RaycastHit hit;
    
    // Spariamo il raggio verso il basso
    if (Physics.Raycast(transform.position, Vector3.down, out hit, 5f))
    {
        DropZone cell = hit.collider.GetComponent<DropZone>();

        // 1. Controllo: Abbiamo colpito una cella? 
        // 2. Controllo: La cella è vuota nella logica del Board?
        if (cell != null && GameManager.Instance.Board.Grid[cell.cellIndex] == CellState.Empty)
        {
            // Se è tutto OK, procediamo con lo snapping
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;

            // Centra il pezzo sulla cella (X e Z)
            Vector3 snapPosition = new Vector3(
                cell.transform.position.x, 
                transform.position.y, 
                cell.transform.position.z
            );
            transform.position = snapPosition;
            transform.rotation = Quaternion.identity;

            rb.useGravity = true;
            rb.isKinematic = false;

            // Comunica la mossa al server
            GameManager.Instance.PlayMoveRpc(cell.cellIndex, pieceType);
            return; // Esci dal metodo: mossa completata!
        }
    }

    // --- USCITA DI EMERGENZA ---
    // Se il raggio non ha colpito nulla, o ha colpito un'altra pedina,
    // o la cella era occupata... il codice arriverà qui sotto.
    Debug.Log("Mossa non valida (cella occupata o fuori scacchiera). Torno all'inizio.");

    ReturnToStart();

    if (GameManager.Instance != null && GameManager.Instance.MaxLives.Value > 0)
    {
        ulong myClientId = Unity.Netcode.NetworkManager.Singleton.LocalClientId;
        GameManager.Instance.WrongMoveRpc(myClientId);
    }

    
}

public void SetStartPosition(Vector3 pos, Quaternion rot)
{
    // AGGIORNAMENTO FONDAMENTALE: Cambiamo la casa base locale
    this.startPosition = pos;
    this.startRotation = rot;

    // Se siamo noi a gestire il pezzo (Owner), lo muoviamo anche fisicamente
    if (IsOwner)
    {
        if (rb == null) rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        transform.SetPositionAndRotation(pos, rot);
        
        // Riattiviamo la fisica dopo un istante
        Invoke(nameof(EnablePhysics), 0.2f);
    }
}

public void DisablePhysics() 
{ 
    rb.isKinematic = true; 
    rb.linearVelocity = Vector3.zero;
    rb.angularVelocity = Vector3.zero;
}

public void EnablePhysics() { rb.isKinematic = false; }

private System.Collections.IEnumerator TeleportAndWait(Vector3 pos, Quaternion rot)
{
    var col = GetComponent<Collider>();
    
    rb.isKinematic = true;
    if (col != null) col.enabled = false;
    
    transform.SetPositionAndRotation(pos, rot);
    rb.linearVelocity = Vector3.zero;
    rb.angularVelocity = Vector3.zero;
    
    // Aspetta che tutti i pezzi siano stati posizionati e la fisica si stabilizzi
    yield return new WaitForFixedUpdate();
    yield return new WaitForFixedUpdate();
    
    rb.isKinematic = false;
    if (col != null) col.enabled = true;
}


    public void ReturnToStart()
{
    // Assicuriamoci che sia visibile (nel caso fosse stata spenta)
    if (GetComponent<MeshRenderer>() != null) 
        GetComponent<MeshRenderer>().enabled = true;

    if (rb != null)
    {
        rb.useGravity = false;
        rb.isKinematic = true;
        rb.linearVelocity = Vector3.zero;
    }
    
    transform.SetPositionAndRotation(startPosition, startRotation);
}
    // NUOVO: Tolgo le cuffie quando la pedina viene distrutta
    override public void OnDestroy()
    {
        // 1. Facciamo fare la pulizia di base a Netcode

        // 2. Facciamo la nostra pulizia personalizzata
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameRestarted -= ReturnToStart;
        }
    }
}