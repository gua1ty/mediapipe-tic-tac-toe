using UnityEngine;

public class DropZone : MonoBehaviour
{
    [Header("Impostazioni Cella")]
    [Tooltip("Inserisci il numero della cella: da 0 a 8")]
    public int cellIndex;

    // Questa funzione è una magia per noi sviluppatori: 
    // Disegna un cubo verde semitrasparente nell'Editor di Unity per farti vedere 
    // dove sono le celle invisibili, ma non si vedrà nel gioco finale!
    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0f, 1f, 0f, 0.3f); // Verde trasparente
        Gizmos.DrawCube(transform.position, transform.localScale);
    }
}