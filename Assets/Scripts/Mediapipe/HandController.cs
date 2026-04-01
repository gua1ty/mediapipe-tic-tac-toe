using System;
using UnityEngine;

public class HandController : MonoBehaviour
{
    private MediapipeBridge mediapipeBridge;
    [SerializeField, Range(0, 1)] private float interpolationFactor;
    [SerializeField] private GameObject handRenderer;
    [NonSerialized] public ProcessedHand referenceHand = null;
    public bool renderizeHand = true;
    public TypeOfHand typeOfHand;

    void Start()
    {
        mediapipeBridge = MediapipeBridge.Instance;
        referenceHand = null;
    }

    //Update function
    public void Update()
    {
        // Hide renderer if the hand is not present
        if (handRenderer != null)
        {
            if (referenceHand != null && referenceHand.handVisible && renderizeHand)
            {
                handRenderer.SetActive(true);
            }
            else
            {
                handRenderer.SetActive(false);
            }
        }

        UpdateHand();
    }

    private void UpdateHand()
    {
        var hands = mediapipeBridge.GetProcessedHands();
        ProcessedHand hand = hands[typeOfHand];

        if (hand == null || hand.landmarksList.Count == 0 || !hand.handVisible)
        {
            if (referenceHand != null)
            {
                referenceHand = null;
            }
            return;
        }

        if (referenceHand == null)
        {
           // Debug.Log("update hand1");
            referenceHand = new ProcessedHand(hand)
            {
                handVisible = true
            };
         //   Debug.Log("update hand2");
            return;
        }

        referenceHand.handVisible = true;

        float inverseInterpolationFactor = 1 - interpolationFactor;

        // Aggiornamento posizioni con interpolazione
        for (int index = 0; index < referenceHand.landmarksList.Count; index++)
        {
            var newLandmark = hand.landmarksList[index];
            var refLandmark = referenceHand.landmarksList[index];
            var newRotatedLandmark = hand.landmarksListRotated[index];
            var refRotatedLandmark = referenceHand.landmarksListRotated[index];

            refLandmark.position = newLandmark.position * inverseInterpolationFactor + refLandmark.position * interpolationFactor;
            refRotatedLandmark.position = newRotatedLandmark.position * inverseInterpolationFactor + refRotatedLandmark.position * interpolationFactor;
        }

        // Aggiornamento dei parametri della mano
        referenceHand.center = hand.center * inverseInterpolationFactor + referenceHand.center * interpolationFactor;
        referenceHand.normDistance = hand.normDistance;
        referenceHand.refX = hand.refX;
        referenceHand.refY = hand.refY;
        referenceHand.refZ = hand.refZ;
    }
}
