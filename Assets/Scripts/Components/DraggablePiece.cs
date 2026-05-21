using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class DraggablePiece : Grabbable
{
    [Header("Impostazioni Pezzo")]
    public CellState pieceType;

    public bool isPlaced = false; // <-- NUOVO: Impedisce di raccogliere pedine già sul tavolo

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
        if (GameManager.Instance.IsChaosMode.Value && !GetComponent<NetworkObject>().IsOwner)
        {
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

    // Ferma TUTTO prima ancora di controllare dove siamo
    rb.linearVelocity  = Vector3.zero;
    rb.angularVelocity = Vector3.zero;
    rb.useGravity      = false;
    rb.isKinematic     = true;
    rb.constraints     = RigidbodyConstraints.FreezeAll;

    transform.rotation = Quaternion.identity;
    CheckDropLocation();
}


    public override bool IsPinchable()
    {
        // 1. NUOVO FIX: Se la pedina è già stata piazzata, non si può più toccare!
        if (isPlaced) return false;

        if (GameManager.Instance.IsGameOver.Value || !GameManager.Instance.IsGameStarted.Value) return false;
        if (!GameManager.Instance.IsMyTurnLocal) return false;

        if (!GameManager.Instance.IsChaosMode.Value)
        {
            CellState myColor = NetworkManager.Singleton.IsHost ? CellState.X : CellState.O;
            if (pieceType != myColor) return false;
        }

        return true; 
    }

    private void OnMouseDown()
    {
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

    if (Physics.Raycast(transform.position, Vector3.down, out hit, 5f))
    {
        DropZone cell = hit.collider.GetComponent<DropZone>();

        if (cell != null && GameManager.Instance.Board.Grid[cell.cellIndex] == CellState.Empty)
        {
            // Uccidi completamente la fisica PRIMA che Unity rilevi compenetrazioni
            rb.linearVelocity  = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.useGravity      = false;
            rb.isKinematic     = true;
            rb.constraints     = RigidbodyConstraints.FreezeAll;

            isPlaced = true;

            // La Y finale è la Y della cella + un offset fisso (regola in base al tuo setup)
            Vector3 finalPos = new Vector3(
                cell.transform.position.x,
                cell.transform.position.y,   // <-- adatta all'altezza della tua pedina
                cell.transform.position.z
            );

            StartCoroutine(SnapToCell(finalPos, cell.cellIndex));
            return;
        }
    }

    // Mossa non valida → torna all'inizio
    ReturnToStart();

    if (GameManager.Instance != null && GameManager.Instance.MaxLives.Value > 0)
    {
        ulong myClientId = NetworkManager.Singleton.LocalClientId;
        GameManager.Instance.WrongMoveRpc(myClientId);
    }
}

private IEnumerator SnapToCell(Vector3 targetPosition, int cellIndex)
{
    float duration = 0.12f;   // veloce ma visibile — regola a piacere
    float elapsed  = 0f;

    targetPosition.y = 1f;

    Vector3 startPos = transform.position;

    while (elapsed < duration)
    {
        elapsed += Time.deltaTime;
        float t = Mathf.SmoothStep(0f, 1f, elapsed / duration); // easing morbido
        transform.position = Vector3.Lerp(startPos, targetPosition, t);
        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.identity, t);
        yield return null;
    }

    // Posizione finale esatta garantita
    transform.SetPositionAndRotation(targetPosition, Quaternion.identity);

    // Registra la mossa SOLO dopo che la pedina è a posto visivamente
    GameManager.Instance.PlayMoveRpc(cellIndex, pieceType);
}

    public void SetStartPosition(Vector3 pos, Quaternion rot)
    {
        this.startPosition = pos;
        this.startRotation = rot;

        if (IsOwner)
        {
            if (rb == null) rb = GetComponent<Rigidbody>();
            rb.isKinematic = true;
            transform.SetPositionAndRotation(pos, rot);
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

    public void ReturnToStart()
    {
        isPlaced = false; 

        if (GetComponent<MeshRenderer>() != null) GetComponent<MeshRenderer>().enabled = true;

        if (rb != null)
        {
            rb.constraints = RigidbodyConstraints.None; // Sblocchiamo la rotazione
            rb.linearDamping = 0f; // Reset attrito
            rb.useGravity = false;
            rb.isKinematic = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        
        transform.SetPositionAndRotation(startPosition, startRotation);
    }
    override public void OnDestroy()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameRestarted -= ReturnToStart;
        }
    }
}