using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using System;
using System.Threading.Tasks;
using System.Linq;
using UnityEngine.SceneManagement;

[Serializable]
public class MpCommand
{
    public int hands_to_search = 1;
}

public class MediapipeBridge : MonoBehaviour
{
    private static MediapipeBridge instance = null;
    public static MediapipeBridge Instance => instance;

    public class MpMessage
    {
        public bool close = false;
    }

    public bool rotation_correction = true;
    public bool geometric_correction = false;
    public float lambda_rate_dist = 0.1f;
    public float lambda_rate_ang = 0.1f;
    public int descent_steps = 10;
    public float l1 = 0.35f;
    public float l2 = 0.2f;

    [SerializeField] public float scale_factor = 1.0f;

    private RequestsThread requestsThread;
    private bool mediapipeProcessStarted = false;
    
    private System.Diagnostics.Process mpProcess; 

    public MpCommand mp_message = new MpCommand();
    public bool connectionOk = false;

    private Dictionary<TypeOfHand, ProcessedHand> processedHands;
    private readonly object handsLock = new();

    [NonSerialized] public MpData currentData;
    [NonSerialized] public int mpRectWidth;
    [NonSerialized] public int mpRectHeight;

    private float lastTimestamp = 0f;

    private void Awake()
    {
        if (!InitializeSingleton()) return;

        processedHands = new Dictionary<TypeOfHand, ProcessedHand>
        {
            { TypeOfHand.Left, new ProcessedHand() },
            { TypeOfHand.Right, new ProcessedHand() }
        };
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "Game") 
        {
            InitializeMediaPipe();
        }
        else if (scene.name == "Menu") // Sostituisci "Menu" con il nome esatto della tua scena!
        {
            ShutdownMediaPipe();
        }
    }

    public void InitializeMediaPipe()
    {
        if (!mediapipeProcessStarted)
        {
            StartCoroutine(StartupSequence());
        }
    }

private IEnumerator StartupSequence()
    {
        if (requestsThread == null)
            requestsThread = new RequestsThread();

        StartMediapipeProcess();

        // Aspettiamo 5 secondi per vedere se il Mac killa il processo per il popup
        yield return new WaitForSeconds(5f);

        bool processAlive = false;
        try { processAlive = mpProcess != null && !mpProcess.HasExited; } catch { }

        if (!processAlive)
        {
            // Primo avvio assoluto: è crashato per il popup, rilanciamo
            Debug.Log("[MP] Primo avvio rilevato, rilancio...");
            StartMediapipeProcess();
        }
        else
        {
            // Avvii successivi: era già autorizzato, non toccare nulla
            Debug.Log("[MP] Permesso già presente, nessun rilancio necessario.");
        }

        mediapipeProcessStarted = true;
    }




    // ---------------------------------------------------------------

    private void Update()
    {
        // TASTO DI EMERGENZA (Premi R se serve riavviarlo a mano)
        if (Input.GetKeyDown(KeyCode.R))
        {
            Debug.LogWarning("Riavvio manuale dell'eseguibile...");
            if (mpProcess != null && !mpProcess.HasExited) mpProcess.Kill();
            StartMediapipeProcess();
            return;
        }

        if (!mediapipeProcessStarted || requestsThread == null) return;

        string answer;
        lock (requestsThread.lockObject)
        {
            answer = requestsThread.answer;
        }

        if (string.IsNullOrEmpty(answer))
        {
            ResetHandsVisibility();
            return;
        }

        if (!requestsThread.connectionOk) return;

        currentData = JsonUtility.FromJson<MpData>(answer);

        if (currentData.hands_list.Count == 0)
        {
            ResetHandsVisibility();
            return;
        }

        bool newTrackingData = lastTimestamp != currentData.timestamp_ms;
        lastTimestamp = currentData.timestamp_ms;

        if (!newTrackingData) return;

        if (currentData.image_width != mpRectWidth) mpRectWidth = currentData.image_width;
        if (currentData.image_height != mpRectHeight) mpRectHeight = currentData.image_height;

        _ = ProcessHandsAsync(currentData.hands_list);
    }

    private void OnApplicationQuit()
    {
        if (requestsThread != null) requestsThread.Stop();
        if (mpProcess != null && !mpProcess.HasExited) mpProcess.Kill();
    }

    public void ShutdownMediaPipe()
    {
        Debug.Log("[MP] Spegnimento MediaPipe per ritorno al menu...");
        
        StopAllCoroutines(); 
        
        // 1. Fermiamo il thread di comunicazione
        if (requestsThread != null) 
        {
            requestsThread.Stop();
            requestsThread = null;
        }
        
        // 2. Killiamo il processo pesante in background (spegne la webcam)
        if (mpProcess != null && !mpProcess.HasExited) 
        {
            mpProcess.Kill();
            mpProcess.Dispose();
            mpProcess = null;
        }
        
        mediapipeProcessStarted = false;
        ResetHandsVisibility();
    }

    private void ResetHandsVisibility()
    {
        if (processedHands == null) return;
        lock (handsLock)
        {
            processedHands[TypeOfHand.Left].handVisible = false;
            processedHands[TypeOfHand.Right].handVisible = false;
        }
    }

    public Dictionary<TypeOfHand, ProcessedHand> GetProcessedHands()
    {
        lock (handsLock)
        {
            return new Dictionary<TypeOfHand, ProcessedHand>(processedHands);
        }
    }

    private bool InitializeSingleton()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
            return true;
        }

        if (instance != this)
        {
            Destroy(gameObject);
            return false;
        }
        return true;
    }

    private void StartMediapipeProcess()
    {
        string logPath = Application.persistentDataPath + "/bridge_log.txt";
        string configPath = Application.streamingAssetsPath + "/conf/config.json";
        string executablePath = Application.streamingAssetsPath + "/mediapipe-bridge-dist/mediapipe-bridge.bin";

        try
        {
            mpProcess = new System.Diagnostics.Process();
            mpProcess.StartInfo.FileName = executablePath;
            mpProcess.StartInfo.Arguments = $"\"{configPath}\"";
            mpProcess.StartInfo.WorkingDirectory = System.IO.Path.GetDirectoryName(executablePath);
            mpProcess.StartInfo.UseShellExecute = false;
            mpProcess.Start();

            System.IO.File.WriteAllText(logPath, $"Executable: {executablePath}\nConfig: {configPath}\n");
        }
        catch (Exception e)
        {
            System.IO.File.WriteAllText(logPath, $"ERRORE: {e.Message}");
            UnityEngine.Debug.LogError($"Errore avvio MediaPipe: {e.Message}");
        }
    }

    private async Task ProcessHandsAsync(List<MpHand> hands)
    {
        Dictionary<TypeOfHand, ProcessedHand> newProcessedHands = new()
        {
            { TypeOfHand.Left, new ProcessedHand() },
            { TypeOfHand.Right, new ProcessedHand() }
        };

        var tasks = hands.Select(hand => Task.Run(() => ProcessHand(new MpHand(hand)))).ToList();
        var results = await Task.WhenAll(tasks);

        foreach (var processedHand in results)
        {
            newProcessedHands[processedHand.typeOfHand] = processedHand;
            newProcessedHands[processedHand.typeOfHand].handVisible = true;
        }

        lock (handsLock)
        {
            processedHands = new Dictionary<TypeOfHand, ProcessedHand>(newProcessedHands);
        }
    }

    private ProcessedHand ProcessHand(MpHand actualHand)
    {
        var _outputHand = new ProcessedHand();
        _ = Enum.TryParse(actualHand.type_of_hand, out _outputHand.typeOfHand);

        // Centro della mano
        Vector3 origin = (actualHand.landmarks_list[5].position + actualHand.landmarks_list[9].position
            + actualHand.landmarks_list[13].position + actualHand.landmarks_list[17].position
            + actualHand.landmarks_list[0].position) / 5;
        _outputHand.center = origin;

        // Asse Y normalizzato
        Vector3 y_mp = ((actualHand.landmarks_list[5].position + actualHand.landmarks_list[9].position
            + actualHand.landmarks_list[13].position + actualHand.landmarks_list[17].position) / 4)
            - actualHand.landmarks_list[0].position;
        Vector3 y_mp_n = y_mp.normalized;
        _outputHand.refY = y_mp_n;

        // Asse X normalizzato
        Vector3 x_mp = ((actualHand.landmarks_list[5].position - actualHand.landmarks_list[17].position)
            + (actualHand.landmarks_list[9].position - actualHand.landmarks_list[17].position)
            + (actualHand.landmarks_list[13].position - actualHand.landmarks_list[17].position)) / 3;
        Vector3 x_mp_dir = x_mp - Vector3.Dot(x_mp, y_mp_n) * y_mp_n;
        Vector3 x_mp_n = x_mp_dir.normalized;
        _outputHand.refX = x_mp_n;

        // Asse Z normalizzato
        Vector3 z_mp_dir = Vector3.Cross(x_mp_dir, y_mp);
        Vector3 z_mp_n = z_mp_dir.normalized;
        _outputHand.refZ = z_mp_n;

        // Distanza di normalizzazione
        float norm_distance = Mathf.Sqrt(z_mp_dir.magnitude) * scale_factor;
        _outputHand.normDistance = norm_distance;

        // Trasformazione nel sistema di riferimento della mano
        int idx = 0;
        foreach (MpLandmark tmp_landmark in actualHand.landmarks_list)
        {
            Vector3 traslated = tmp_landmark.position - origin;
            Vector3 normalized = traslated / norm_distance;
            Vector3 rotated = new Vector3(
                Vector3.Dot(normalized, x_mp_n),
                Vector3.Dot(normalized, y_mp_n),
                Vector3.Dot(normalized, z_mp_n)
            );

            ProcessedLandmark norm_landmark = new ProcessedLandmark { index = idx, position = normalized };
            ProcessedLandmark rot_landmark = new ProcessedLandmark { index = idx, position = rotated };

            norm_landmark.position.y = -norm_landmark.position.y;

            _outputHand.landmarksList.Add(norm_landmark);
            _outputHand.landmarksListRotated.Add(rot_landmark);
            idx++;
        }

        if (geometric_correction)
            HandGeometricCorrection(_outputHand);

        return _outputHand;
    }

    public void HandGeometricCorrection(ProcessedHand auxiliar_hand)
    {
        int N = descent_steps;
        float finger_length = l1;
        float finger_length2 = l1;
        float finger_length3 = l2;

        ProcessedHand descent_hand = auxiliar_hand;
        for (int i = 1; i < N; i++)
        {
            descent_hand.landmarksList[6].position = -lambda_rate_ang * DescentAngleCos(auxiliar_hand.landmarksList[0].position, auxiliar_hand.landmarksList[5].position, auxiliar_hand.landmarksList[6].position, 0, 1) + auxiliar_hand.landmarksList[6].position;
            descent_hand.landmarksList[10].position = -lambda_rate_ang * DescentAngleCos(auxiliar_hand.landmarksList[0].position, auxiliar_hand.landmarksList[9].position, auxiliar_hand.landmarksList[10].position, 0, 1) + auxiliar_hand.landmarksList[10].position;
            descent_hand.landmarksList[14].position = -lambda_rate_ang * DescentAngleCos(auxiliar_hand.landmarksList[0].position, auxiliar_hand.landmarksList[13].position, auxiliar_hand.landmarksList[14].position, 0, 1) + auxiliar_hand.landmarksList[14].position;
            descent_hand.landmarksList[18].position = -lambda_rate_ang * DescentAngleCos(auxiliar_hand.landmarksList[0].position, auxiliar_hand.landmarksList[17].position, auxiliar_hand.landmarksList[18].position, 0, 1) + auxiliar_hand.landmarksList[18].position;
            descent_hand.landmarksList[6].position = -lambda_rate_ang * DescentAngleSin(auxiliar_hand.landmarksList[0].position, auxiliar_hand.landmarksList[5].position, auxiliar_hand.landmarksList[6].position, 0, 1) + auxiliar_hand.landmarksList[6].position;
            descent_hand.landmarksList[10].position = -lambda_rate_ang * DescentAngleSin(auxiliar_hand.landmarksList[0].position, auxiliar_hand.landmarksList[9].position, auxiliar_hand.landmarksList[10].position, 0, 1) + auxiliar_hand.landmarksList[10].position;
            descent_hand.landmarksList[14].position = -lambda_rate_ang * DescentAngleSin(auxiliar_hand.landmarksList[0].position, auxiliar_hand.landmarksList[13].position, auxiliar_hand.landmarksList[14].position, 0, 1) + auxiliar_hand.landmarksList[14].position;
            descent_hand.landmarksList[18].position = -lambda_rate_ang * DescentAngleSin(auxiliar_hand.landmarksList[0].position, auxiliar_hand.landmarksList[17].position, auxiliar_hand.landmarksList[18].position, 0, 1) + auxiliar_hand.landmarksList[18].position;

            descent_hand.landmarksList[7].position = -lambda_rate_ang * DescentAngleCos(auxiliar_hand.landmarksList[5].position, auxiliar_hand.landmarksList[6].position, auxiliar_hand.landmarksList[7].position, 0, 1) + auxiliar_hand.landmarksList[7].position;
            descent_hand.landmarksList[11].position = -lambda_rate_ang * DescentAngleCos(auxiliar_hand.landmarksList[9].position, auxiliar_hand.landmarksList[10].position, auxiliar_hand.landmarksList[11].position, 0, 1) + auxiliar_hand.landmarksList[11].position;
            descent_hand.landmarksList[15].position = -lambda_rate_ang * DescentAngleCos(auxiliar_hand.landmarksList[13].position, auxiliar_hand.landmarksList[14].position, auxiliar_hand.landmarksList[15].position, 0, 1) + auxiliar_hand.landmarksList[15].position;
            descent_hand.landmarksList[19].position = -lambda_rate_ang * DescentAngleCos(auxiliar_hand.landmarksList[17].position, auxiliar_hand.landmarksList[18].position, auxiliar_hand.landmarksList[19].position, 0, 1) + auxiliar_hand.landmarksList[19].position;
            descent_hand.landmarksList[7].position = -lambda_rate_ang * DescentAngleSin(auxiliar_hand.landmarksList[5].position, auxiliar_hand.landmarksList[6].position, auxiliar_hand.landmarksList[7].position, 0, 1) + auxiliar_hand.landmarksList[7].position;
            descent_hand.landmarksList[11].position = -lambda_rate_ang * DescentAngleSin(auxiliar_hand.landmarksList[9].position, auxiliar_hand.landmarksList[10].position, auxiliar_hand.landmarksList[11].position, 0, 1) + auxiliar_hand.landmarksList[11].position;
            descent_hand.landmarksList[15].position = -lambda_rate_ang * DescentAngleSin(auxiliar_hand.landmarksList[13].position, auxiliar_hand.landmarksList[14].position, auxiliar_hand.landmarksList[15].position, 0, 1) + auxiliar_hand.landmarksList[15].position;
            descent_hand.landmarksList[19].position = -lambda_rate_ang * DescentAngleSin(auxiliar_hand.landmarksList[17].position, auxiliar_hand.landmarksList[18].position, auxiliar_hand.landmarksList[19].position, 0, 1) + auxiliar_hand.landmarksList[19].position;

            descent_hand.landmarksList[8].position = -lambda_rate_ang * DescentAngleCos(auxiliar_hand.landmarksList[6].position, auxiliar_hand.landmarksList[7].position, auxiliar_hand.landmarksList[8].position, 0, 1) + auxiliar_hand.landmarksList[8].position;
            descent_hand.landmarksList[12].position = -lambda_rate_ang * DescentAngleCos(auxiliar_hand.landmarksList[10].position, auxiliar_hand.landmarksList[11].position, auxiliar_hand.landmarksList[12].position, 0, 1) + auxiliar_hand.landmarksList[12].position;
            descent_hand.landmarksList[16].position = -lambda_rate_ang * DescentAngleCos(auxiliar_hand.landmarksList[14].position, auxiliar_hand.landmarksList[15].position, auxiliar_hand.landmarksList[16].position, 0, 1) + auxiliar_hand.landmarksList[16].position;
            descent_hand.landmarksList[20].position = -lambda_rate_ang * DescentAngleCos(auxiliar_hand.landmarksList[18].position, auxiliar_hand.landmarksList[19].position, auxiliar_hand.landmarksList[20].position, 0, 1) + auxiliar_hand.landmarksList[20].position;
            descent_hand.landmarksList[8].position = -lambda_rate_ang * DescentAngleSin(auxiliar_hand.landmarksList[6].position, auxiliar_hand.landmarksList[7].position, auxiliar_hand.landmarksList[8].position, 0, 1) + auxiliar_hand.landmarksList[8].position;
            descent_hand.landmarksList[12].position = -lambda_rate_ang * DescentAngleSin(auxiliar_hand.landmarksList[10].position, auxiliar_hand.landmarksList[11].position, auxiliar_hand.landmarksList[12].position, 0, 1) + auxiliar_hand.landmarksList[12].position;
            descent_hand.landmarksList[16].position = -lambda_rate_ang * DescentAngleSin(auxiliar_hand.landmarksList[14].position, auxiliar_hand.landmarksList[15].position, auxiliar_hand.landmarksList[16].position, 0, 1) + auxiliar_hand.landmarksList[16].position;
            descent_hand.landmarksList[20].position = -lambda_rate_ang * DescentAngleSin(auxiliar_hand.landmarksList[18].position, auxiliar_hand.landmarksList[19].position, auxiliar_hand.landmarksList[20].position, 0, 1) + auxiliar_hand.landmarksList[20].position;

            descent_hand.landmarksList[6].position = -lambda_rate_dist * DescentDistance(auxiliar_hand.landmarksList[6].position, auxiliar_hand.landmarksList[5].position, finger_length) + auxiliar_hand.landmarksList[6].position;
            descent_hand.landmarksList[10].position = -lambda_rate_dist * DescentDistance(auxiliar_hand.landmarksList[10].position, auxiliar_hand.landmarksList[9].position, finger_length) + auxiliar_hand.landmarksList[10].position;
            descent_hand.landmarksList[14].position = -lambda_rate_dist * DescentDistance(auxiliar_hand.landmarksList[14].position, auxiliar_hand.landmarksList[13].position, finger_length) + auxiliar_hand.landmarksList[14].position;
            descent_hand.landmarksList[18].position = -lambda_rate_dist * DescentDistance(auxiliar_hand.landmarksList[18].position, auxiliar_hand.landmarksList[17].position, finger_length) + auxiliar_hand.landmarksList[18].position;

            descent_hand.landmarksList[7].position = -lambda_rate_dist * DescentDistance(auxiliar_hand.landmarksList[7].position, auxiliar_hand.landmarksList[6].position, finger_length2) + auxiliar_hand.landmarksList[7].position;
            descent_hand.landmarksList[11].position = -lambda_rate_dist * DescentDistance(auxiliar_hand.landmarksList[11].position, auxiliar_hand.landmarksList[10].position, finger_length2) + auxiliar_hand.landmarksList[11].position;
            descent_hand.landmarksList[15].position = -lambda_rate_dist * DescentDistance(auxiliar_hand.landmarksList[15].position, auxiliar_hand.landmarksList[14].position, finger_length2) + auxiliar_hand.landmarksList[15].position;
            descent_hand.landmarksList[19].position = -lambda_rate_dist * DescentDistance(auxiliar_hand.landmarksList[19].position, auxiliar_hand.landmarksList[18].position, finger_length2) + auxiliar_hand.landmarksList[19].position;

            descent_hand.landmarksList[8].position = -lambda_rate_dist * DescentDistance(auxiliar_hand.landmarksList[8].position, auxiliar_hand.landmarksList[7].position, finger_length3) + auxiliar_hand.landmarksList[8].position;
            descent_hand.landmarksList[12].position = -lambda_rate_dist * DescentDistance(auxiliar_hand.landmarksList[12].position, auxiliar_hand.landmarksList[11].position, finger_length3) + auxiliar_hand.landmarksList[12].position;
            descent_hand.landmarksList[16].position = -lambda_rate_dist * DescentDistance(auxiliar_hand.landmarksList[16].position, auxiliar_hand.landmarksList[15].position, finger_length3) + auxiliar_hand.landmarksList[16].position;
            descent_hand.landmarksList[20].position = -lambda_rate_dist * DescentDistance(auxiliar_hand.landmarksList[20].position, auxiliar_hand.landmarksList[19].position, finger_length3) + auxiliar_hand.landmarksList[20].position;

            auxiliar_hand = descent_hand;
        }
    }

    public Vector3 DescentDistance(Vector3 p2, Vector3 p1, float value)
    {
        return 2 * (Vector3.Dot(p2 - p1, p2 - p1) - value) * (p2 - p1).normalized;
    }

    public Vector3 DescentAngleCos(Vector3 p1, Vector3 p2, Vector3 p3, float alpha, float beta)
    {
        Vector3 v2 = (p3 - p2).normalized;
        Vector3 v1 = (p2 - p1).normalized;
        float cos = Vector3.Dot(v2, v1);

        if (cos > beta) return 2 * (cos - beta) * v1;
        if (cos < alpha) return 2 * (cos - alpha) * v1;
        return Vector3.zero;
    }

    public Matrix4x4 TransposedSkewOfVector(Vector3 input)
    {
        return new Matrix4x4
        {
            m00 = 0, m01 = input.z, m02 = -input.y,
            m10 = -input.z, m11 = 0, m12 = input.x,
            m20 = input.y, m21 = -input.x, m22 = 0
        };
    }

    public Vector3 DescentAngleSin(Vector3 p1, Vector3 p2, Vector3 p3, float alpha, float beta)
    {
        Vector3 v2 = (p3 - p2).normalized;
        Vector3 v1 = (p2 - p1).normalized;
        Vector3 sin_vector = Vector3.Cross(v1, v2);

        if (sin_vector.magnitude > beta)
            return -2 * (sin_vector.magnitude - beta) * (TransposedSkewOfVector(v1) * sin_vector.normalized);
        if (sin_vector.magnitude < alpha)
            return -2 * (sin_vector.magnitude - alpha) * (TransposedSkewOfVector(v1) * sin_vector.normalized);
        return Vector3.zero;
    }
}