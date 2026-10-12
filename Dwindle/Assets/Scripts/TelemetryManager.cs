using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Persistent metric tracker that operates all throughout a
// gameplay session and writes findings to disk

public class TelemetryManager : MonoBehaviour
{
    [Header("Tweaks")]
    [SerializeField]
    private bool generateFilesInEditor = false;


    private static TelemetryManager instance;

    private string filePath;
    private StreamWriter fileWriter;

    private float levelStartTime = 0.0f;
    private int currLevel = 0;

    // Can change manager to intercept actions rather than get them passed in at the end

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        if (!Application.isEditor || (Application.isEditor && generateFilesInEditor))
        {
            filePath = Path.Combine(Application.persistentDataPath, "DwindleMetrics.csv");

            fileWriter = new StreamWriter(filePath, true);

            // TODO: See if granularity needed in the way of
            // checking the amount of times specific pieces are used
            fileWriter.WriteLine("Level,TimeElapsed,PiecesUsed,StarLevel,HiScore");
            fileWriter.Flush();
        }
    }

    public void LevelStart(int level)
    {
        currLevel = level;
        levelStartTime = Time.timeSinceLevelLoad;
    }

    public void LogCurrentLevel(int piecesUsed, int starAchieved, int score)
    {
        if (fileWriter != null)
        {
            fileWriter.WriteLine($"Level{currLevel},{Time.timeSinceLevelLoad - levelStartTime},{piecesUsed},{starAchieved},{score}");
            fileWriter.Flush();
        }
    }

    public void CleanUp()
    {
        if (fileWriter != null)
        {
            fileWriter.Close();
        }

    }
}
