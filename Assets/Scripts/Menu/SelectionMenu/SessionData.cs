public static class SessionData
{
    public static string gameMode = "Normal";
    public static int turnTimerValue = 0; 
    public static int gameTimerValue = 0; 
    public static int maxErrors = 0;

    public static void ResetToDefaults()
    {
        gameMode = "Normal";
        turnTimerValue = 0;
        gameTimerValue = 0;
        maxErrors = 0;
    }
}
