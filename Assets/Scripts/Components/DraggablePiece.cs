using UnityEngine;

public class DraggablePiece : Grabbable
{
    [Header("Impostazioni Pezzo")]
    public CellState pieceType;

    private Vector3 startPosition;
    private Quaternion startRotation;
    private bool isDragged = false;
    private Vector3 currentTargetPosition;
    private Rigidbody rb; // AGGIUNTO

    private void Awake()
    {
        rb = GetComponent<Rigidbody>(); // AGGIUNTO
        rb.useGravity = false; // AGGIUNTO
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
        transform.Rotate(Vector3.up, 90f * Time.deltaTime); // AGGIUNTO
    }
}

    public override void StartDrag()
    {
        isDragged = true;
        rb.useGravity = false; // MODIFICATO
        rb.linearVelocity = Vector3.zero; // AGGIUNTO
    }

    public override void UpdateTarget(Vector3 targetPosition)
    {
        currentTargetPosition = targetPosition;
    }

    public override void StopDrag()
    {
        isDragged = false;
        CheckDropLocation();
    }

    public override bool IsPinchable()
    {
        return true;
    }

    private void OnMouseDown()
    {
        Debug.Log("CLICK RICEVUTO sulla pedina: " + gameObject.name);
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
        if (Physics.Raycast(transform.position, Vector3.down, out hit, 5f))
        {
            DropZone cell = hit.collider.GetComponent<DropZone>();
            if (cell != null)
            {
                rb.useGravity = true; // MODIFICATO
                GameManager.Instance.PlayMove(cell.cellIndex);
                return;
            }
        }
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