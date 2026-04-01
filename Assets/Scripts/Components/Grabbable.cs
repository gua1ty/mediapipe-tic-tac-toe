using Unity.Netcode;
using UnityEngine;

/// <summary>
/// An interface that allows objects to be picked up by a Mediapipe HandController.
/// </summary>
public abstract class Grabbable : NetworkBehaviour
{
    public abstract void StartDrag();

    public abstract void UpdateTarget(Vector3 targetPosition);

    public abstract void StopDrag();

    public abstract bool IsPinchable();
}