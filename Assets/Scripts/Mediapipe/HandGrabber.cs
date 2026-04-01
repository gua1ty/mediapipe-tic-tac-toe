using UnityEngine;

public class HandGrabber : MonoBehaviour
{
    [Header("Impostazioni Pinch")]
    public float pinchThreshold = 0.05f; 
    public float grabRadius = 0.1f;      

    [Header("Correzione Rotazione Mano")]
    [Tooltip("Usa questo per riallineare visivamente la pedina alle dita (es. Y = -2 o Z = 1)")]
    public Vector3 manualVisualOffset = Vector3.zero;

    private Transform thumbTip;  
    private Transform indexTip;  

    private Grabbable currentGrabbedObject = null;
    private bool isPinching = false;

    private Vector3 grabOffset = Vector3.zero;
    private bool initialized = false;

    void Update()
    {
        // 1. Aspettiamo che l'HandRenderer crei i Joint
        if (!initialized)
        {
            FindFingers();
            return; 
        }

        // 2. Calcoliamo la distanza tra pollice e indice
        float distance = Vector3.Distance(thumbTip.position, indexTip.position);
        
        // 3. Troviamo il punto a metà tra le dita
        Vector3 pinchCenter = (thumbTip.position + indexTip.position) / 2f;

        // 4. Logica del Pinch
        if (distance < pinchThreshold)
        {
            if (!isPinching)
            {
                // INIZIO PINCH
                isPinching = true;
                TryGrabObject(pinchCenter);
            }
            else if (currentGrabbedObject != null)
            {
                // DURANTE IL PINCH (Trascinamento)
                // Qui applichiamo sia l'offset matematico iniziale, sia il tuo offset visivo manuale
                currentGrabbedObject.UpdateTarget(pinchCenter + grabOffset + manualVisualOffset);
            }
        }
        else
        {
            if (isPinching)
            {
                // FINE PINCH (Rilascio)
                isPinching = false;
                if (currentGrabbedObject != null)
                {
                    // CORREZIONE: Qui dobbiamo chiamare StopDrag per lasciarla cadere nella cella!
                    currentGrabbedObject.StopDrag();
                    currentGrabbedObject = null;
                }
            }
        }
    }

    private void FindFingers()
    {
        Transform j4 = transform.Find("Joint_4");
        Transform j8 = transform.Find("Joint_8");

        if (j4 != null && j8 != null)
        {
            thumbTip = j4;
            indexTip = j8;
            initialized = true;
            Debug.Log("Dita trovate! HandGrabber attivato.");
        }
    }

    private void TryGrabObject(Vector3 pinchCenter)
    {
        // Trasformiamo le tue dita 3D in coordinate "Schermo" 2D
        Vector3 screenPos = Camera.main.WorldToScreenPoint(pinchCenter);

        // Creiamo il raggio
        Ray mouseRay = Camera.main.ScreenPointToRay(screenPos);

        // Spariamo il raggio
        if (Physics.Raycast(mouseRay, out RaycastHit hit, 100f))
        {
            Grabbable grabbable = hit.collider.GetComponentInParent<Grabbable>();
            
            if (grabbable != null && grabbable.IsPinchable())
            {
                currentGrabbedObject = grabbable;
                currentGrabbedObject.StartDrag();

                // Calcoliamo la distanza tra la mano e l'oggetto nel momento esatto della presa
                grabOffset = currentGrabbedObject.transform.position - pinchCenter;
                
                Debug.Log("<color=yellow>PRESA EFFETTUATA:</color> " + hit.collider.gameObject.name);
            }
        }
    }
}