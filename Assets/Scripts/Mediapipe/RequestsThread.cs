using System.Net.Sockets;
using System.Text;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine;

using static MediapipeBridge;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;

using Debug = UnityEngine.Debug;

public class RequestsThread
{
    // Constants
    private const int RETRYING_TIME = 5000; // millisecondi
    private const int GETTING_DATA_FREQUENCY = 40; // al secondo

    private readonly Thread thread;
    private bool isRunning = true;
    private Socket socket;
    public string ip = "127.0.0.1";
    public int port = 65432;
    public float retryTimer = 0;
    public bool connectionOk = false;

    public object lockObject = new();
    public string answer;

    private Stopwatch stopwatch = new();


    /// <summary>
    /// Costruttore della classe RequestsThread. Inizializza e avvia il thread.
    /// </summary>
    public RequestsThread()
    {
        thread = new Thread(ProcessRequests)
        {
            Priority = System.Threading.ThreadPriority.Highest
        };
        thread.Start();
    }

    /// <summary>
    /// Ferma il thread e chiude la connessione.
    /// </summary>
    public void Stop()
    {
        isRunning = false;
        if (thread != null && thread.IsAlive)
        {
            thread.Join();
        }
        CloseConnection();
    }

    /// <summary>
    /// Metodo eseguito dal thread per processare le richieste.
    /// </summary>
    private void ProcessRequests()
    {
        stopwatch.Start();
        long currentTimestamp = 0;
        while (isRunning)
        {
            currentTimestamp = stopwatch.ElapsedMilliseconds;
            try
            {
                // Initialize socket if needed
                if (socket == null || !socket.Connected)
                {
                    InitializeSocket();

                    if (!connectionOk)
                    {
                        Thread.Sleep(RETRYING_TIME);
                        continue;
                    }
                }

                // Prepara e invia il messaggio
                SendMessageAndReceiveData();

                long elapsed = stopwatch.ElapsedMilliseconds - currentTimestamp;
                long sleepTime = (1000 / GETTING_DATA_FREQUENCY) - elapsed;
                if (sleepTime > 0)
                {
                    // Usa un ciclo di attesa attiva per migliorare la precisione
                    long targetTime = stopwatch.ElapsedMilliseconds + sleepTime;
                    while (stopwatch.ElapsedMilliseconds < targetTime)
                    {
                        Thread.SpinWait(1);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Errore non gestito nel ciclo principale: {e.Message}");
                CleanupConnection();
                Thread.Sleep(RETRYING_TIME);
            }
        }
    }

    /// <summary>
    /// Inizializza il socket e stabilisce la connessione.
    /// </summary>
    private void InitializeSocket()
    {
        try
        {
            // Chiudi il socket esistente se presente
            CleanupConnection();

            // Create connection with client          
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
            {
                SendTimeout = 500,
                ReceiveTimeout = 1000
            };
            socket.Connect(ip, port);
            connectionOk = socket.Connected;

            if (connectionOk)
            {
                Debug.Log("Connessione con il socket stabilita");
            }
            else
            {
                Debug.Log("Connessione fallita - socket non connesso dopo Connect()");
            }
        }
        catch (SocketException se)
        {
            //Debug.LogWarning($"Errore socket durante la creazione della connessione: {se.Message} (Codice: {se.ErrorCode})");
            CleanupConnection();
        }
        catch (Exception e)
        {
            Debug.LogError($"Errore durante la creazione del socket: {e.Message}");
            CleanupConnection();
        }
    }

    /// <summary>
    /// Prepara e invia un messaggio, quindi riceve i dati di risposta.
    /// </summary>
    private void SendMessageAndReceiveData()
    {
        try
        {
            // Prepara il messaggio
            MpMessage requestMessage = new() { close = false };
            string messageToSend = JsonUtility.ToJson(requestMessage);
            byte[] messageToSendBytes = Encoding.UTF8.GetBytes(messageToSend);

            // Invia la dimensione del messaggio (4 byte)
            byte[] messageSizeBytes = BitConverter.GetBytes(Convert.ToInt32(messageToSendBytes.Length));
            socket.Send(messageSizeBytes);

            // Invia il messaggio effettivo
            socket.Send(messageToSendBytes);

            // Ricevi la risposta
            byte[] receivedBytes = ReceiveData(socket);

            // Salva la risposta
            lock (lockObject)
            {
                answer = Encoding.UTF8.GetString(receivedBytes);
            }
        }
        catch (SocketException se)
        {
            Debug.LogWarning($"Errore socket durante l'invio/ricezione: {se.Message}, Codice: {se.ErrorCode}");
            CleanupConnection();
            throw; // Rilancia l'eccezione per gestirla nel ciclo principale
        }
        catch (Exception e)
        {
            Debug.LogError($"Errore durante l'invio/ricezione dati: {e.Message}");
            CleanupConnection();
            throw; // Rilancia l'eccezione per gestirla nel ciclo principale
        }
    }

    /// <summary>
    /// Pulisce la connessione chiudendo il socket.
    /// </summary>
    private void CleanupConnection()
    {
        connectionOk = false;

        if (socket != null)
        {
            try
            {
                if (socket.Connected)
                {
                    socket.Shutdown(SocketShutdown.Both);
                }
                socket.Close();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Errore durante la chiusura del socket: {e.Message}");
            }
            finally
            {
                socket = null;
            }
        }
    }

    /// <summary>
    /// Riceve i dati dal socket.
    /// </summary>
    /// <param name="client">Il socket client da cui ricevere i dati.</param>
    /// <returns>Un array di byte contenente i dati ricevuti.</returns>
    private byte[] ReceiveData(Socket client)
    {
        // Buffer per ricevere la dimensione dei dati
        byte[] dataSizeBuffer = new byte[4];

        // Ricevi i 4 byte che rappresentano la dimensione dei dati
        int receivedBytes = client.Receive(dataSizeBuffer, 0, 4, SocketFlags.None);
        if (receivedBytes != 4)
        {
            throw new Exception("Error while receiving data size");
        }

        // Converti i 4 byte in un intero che rappresenta la dimensione dei dati
        int dataSize = BitConverter.ToInt32(dataSizeBuffer, 0);

        // Buffer per ricevere i dati effettivi
        byte[] dataBuffer = new byte[dataSize];

        // Ricevi i dati effettivi
        int totalReceivedBytes = 0;
        while (totalReceivedBytes < dataSize)
        {
            receivedBytes = client.Receive(dataBuffer, totalReceivedBytes, dataSize - totalReceivedBytes, SocketFlags.None);
            if (receivedBytes == 0)
            {
                throw new Exception("Error while receiving data");
            }
            totalReceivedBytes += receivedBytes;
            //Debug.Log("Total received bytes: " + totalReceivedBytes);
        }
        return dataBuffer;
    }

    /// <summary>
    /// Chiude la connessione e invia un messaggio di chiusura.
    /// </summary>
    private void CloseConnection()
    {
        if (socket != null)
        {
            try
            {
                MpMessage requestMessage = new() { close = true };
                string messageToSend = JsonUtility.ToJson(requestMessage);
                byte[] messageToSendBytes = Encoding.UTF8.GetBytes(messageToSend);

                // Send some data for the request
                byte[] messageSizeBytes = BitConverter.GetBytes(Convert.ToInt32(messageToSendBytes.Length));
                socket.Send(messageSizeBytes);

                // Invia il messaggio effettivo
                socket.Send(messageToSendBytes);
            }
            catch (Exception e)
            {
                Debug.LogError($"Errore durante l'invio del messaggio di chiusura: {e.Message}");
            }
        }
        CleanupConnection();
    }
}