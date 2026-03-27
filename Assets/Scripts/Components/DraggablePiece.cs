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
    }

    private void Start()
    {
        startPosition = transform.position;
        startRotation = transform.rotation;
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
    isDragged = true;
    rb.useGravity = false;
    rb.isKinematic = true; // Impedisce al pezzo di bloccarsi contro il tavolo
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
        return true;
    }

    private void OnMouseDown()
    {
        bool isMyTurn;
        if (NetworkManager.Singleton.IsHost)
            isMyTurn = GameManager.Instance.CurrentTurnIndex.Value == 0;
        else
            isMyTurn = GameManager.Instance.CurrentTurnIndex.Value == 1;

        if (GameManager.Instance.IsGameOver.Value) return;
        if (!isMyTurn) return;
        if (pieceType != (NetworkManager.Singleton.IsHost ? CellState.X : CellState.O)) return;

        StartDrag();
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
            GameManager.Instance.PlayMoveRpc(cell.cellIndex);
            return; // Esci dal metodo: mossa completata!
        }
    }

    // --- USCITA DI EMERGENZA ---
    // Se il raggio non ha colpito nulla, o ha colpito un'altra pedina,
    // o la cella era occupata... il codice arriverà qui sotto.
    Debug.Log("Mossa non valida (cella occupata o fuori scacchiera). Torno all'inizio.");
    ReturnToStart();
}

    public void ReturnToStart()
    {
        rb.useGravity = false;
        rb.linearVelocity = Vector3.zero;
        transform.position = startPosition;
        transform.rotation = startRotation;
    }
}