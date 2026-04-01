using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class HandRenderer : MonoBehaviour
{
    public HandController handController;
    public GameObject sphere_to_print;
    public bool horizontallyInverted;
    private Camera referenceCamera;
    public float screenScaling = 1.0f;
    private readonly int[,] linesToDraw = new int[,] { { 0, 1 }, { 1, 2 }, { 2, 3 }, { 3, 4 }, { 1, 5 }, { 5, 6 }, { 6, 7 }, { 7, 8 }, { 5, 9 }, { 9, 10 }, { 10, 11 }, { 11, 12 }, { 9, 13 }, { 13, 14 }, { 14, 15 }, { 15, 16 }, { 13, 17 }, { 17, 18 }, { 18, 19 }, { 19, 20 }, { 17, 0 } };
    public int handZOffset = 15;
    private List<GameObject> lines;
    public float scale = 1.0f;

    private Vector3 cameraPosition;
    public float width = 0.05f;

    private List<GameObject> jointObjectsList = null;

    // Start is called before the first frame update
    void Start()
    {
        referenceCamera = Camera.main;
        cameraPosition = referenceCamera.transform.position;

        lines = new List<GameObject>();
        for (int i = 0; i < linesToDraw.Length / 2; i++)
        {
            GameObject line = new("Line");
            line.transform.parent = this.transform;
            line.AddComponent<LineRenderer>();
            LineRenderer lineRenderer = line.GetComponent<LineRenderer>();
            lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
            lines.Add(line);
        }

    }

    // Update is called once per frame
    void Update()
    {
        if (handController.referenceHand == null || !handController.referenceHand.handVisible)
        {
            return;
        }

        if (jointObjectsList == null)
        {
            CreateJointObjectList();
        }

        UpdateHandPosition();
        UpdateLandmarks();
        UpdateLines();
    }

    private void CreateJointObjectList()
    {
        jointObjectsList = new List<GameObject>();

        for (int i = 0; i < handController.referenceHand.landmarksList.Count; i++)
        {
            GameObject newJointObject = new($"Joint_{handController.referenceHand.landmarksList[i].index}");
            newJointObject.transform.parent = transform;
            jointObjectsList.Add(newJointObject);
        }
    }

    private void UpdateHandPosition()
    {
        if (referenceCamera == null) referenceCamera = Camera.main;

        // 1. Posizione normalizzata da MediaPipe (da 0 a 1)
        Vector3 handPos = handController.referenceHand.center;

        // 2. Adattiamo le coordinate per Unity (Viewport)
        // In Unity, Y=0 è in basso e Y=1 è in alto. MediaPipe spesso è al contrario, quindi facciamo 1 - Y.
        float viewportX = handPos.x;
        float viewportY = 1.0f - handPos.y; 

        // 3. Gestione dell'effetto specchio
        if (horizontallyInverted)
        {
            viewportX = 1.0f - viewportX;
        }

        // 4. LA MAGIA: Chiediamo a Unity di trovare il punto 3D esatto nello schermo!
        // Passiamo X e Y dello schermo, e come Z la distanza dalla telecamera
        Vector3 worldPosition = referenceCamera.ViewportToWorldPoint(new Vector3(viewportX, viewportY, handZOffset));

        // 5. Spostiamo la mano
        gameObject.transform.parent.position = worldPosition;
    }

    private void UpdateLandmarks()
    {
        foreach (ProcessedLandmark landmark in handController.referenceHand.landmarksList)
        {
            if (landmark.worldObj == null)
            {
                landmark.worldObj = jointObjectsList[landmark.index];
            }

            Vector3 landmarkPosition = horizontallyInverted ?
                new Vector3(-landmark.position.x, landmark.position.y, landmark.position.z) :
                landmark.position;

            landmark.worldObj.transform.localPosition = landmarkPosition * scale;
        }
    }

    private void UpdateLines()
    {
        const int LINE_LAYER = 9;
        const int SORTING_ORDER = 20;

        for (int i = 0; i < lines.Count; i++)
        {
            LineRenderer lineRenderer = lines[i].GetComponent<LineRenderer>();
            if (lineRenderer == null) continue;

            ConfigureLineRenderer(lineRenderer, LINE_LAYER, SORTING_ORDER);

            try
            {
                SetLinePositions(lineRenderer, i);
            }
            catch (System.Exception e)
            {
            }
        }
    }

    private void ConfigureLineRenderer(LineRenderer lineRenderer, int layer, int sortingOrder)
    {
        lineRenderer.startColor = lineRenderer.endColor = Color.black;
        lineRenderer.startWidth = lineRenderer.endWidth = width;
        lineRenderer.positionCount = 2;
        lineRenderer.useWorldSpace = true;
        lineRenderer.sortingOrder = sortingOrder;
        lineRenderer.gameObject.layer = layer;
    }

    private void SetLinePositions(LineRenderer lineRenderer, int lineIndex)
    {
        var startLandmark = handController.referenceHand.landmarksList[linesToDraw[lineIndex, 0]].worldObj;
        var endLandmark = handController.referenceHand.landmarksList[linesToDraw[lineIndex, 1]].worldObj;

        if (startLandmark != null && endLandmark != null)
        {
            lineRenderer.SetPosition(0, startLandmark.transform.position);  // position of the starting points of the line
            lineRenderer.SetPosition(1, endLandmark.transform.position);    // position of the end points of the line
        }
    }
}
