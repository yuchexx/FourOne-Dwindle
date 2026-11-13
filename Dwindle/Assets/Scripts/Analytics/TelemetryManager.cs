using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
// using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;
// using NUnit.Framework.Internal;
// using System.Runtime.CompilerServices;
// using UnityEditor.AdaptivePerformance.Editor;
// using Unity.VisualScripting;


// Persistent metric tracker that operates all throughout a
// gameplay session and writes findings to disk

public class TelemetryManager : MonoBehaviour
{
    [Header("Google Form URLS")]
    [SerializeField]
    private string levelCompletionForm = "https://docs.google.com/forms/d/e/1FAIpQLScsOUa-nAnfAcUCfXClZVxhDMw3WuBDLeh9d9UVdUEIBGq4KA/formResponse";
    [SerializeField]
    private string levelAttemptsPreForm = "https://docs.google.com/forms/d/e/1FAIpQLSfWSrd91_bDOi5Soq1hJyparZxPvLobM7WrLV3ZM9nxDfP8qA/formResponse";
    [SerializeField]
    private string levelAttemptsPostForm = "https://docs.google.com/forms/d/e/1FAIpQLScCDs0U5H07NAn9Es0PmgvXtM5_9z4wHhnYVKrzcyBbRE4Qrg/formResponse";

    [Header("Parameters")]
    [SerializeField]
    private int LEVEL_COUNT = Levels.All.Count; // Just some overshooting so new levels don't break the array

    [Header("Testing")]
    [Tooltip("Also send from the Unity editor. Keep off, or test plays land in the sheet")]
    [SerializeField]
    private bool sendInEditor = false;

    public static TelemetryManager Instance { get; private set; }

    public GameObject gameManager;
    private GameManager gameManagerScript;

    bool levelBeat = false;


    private string sessionID;
    private float levelStartTime = 0.0f;
    private int currLevel = 0;
    private int finalPiecesUsed = 0;
    private List<int> levelAttemptsPreCompletion; // Holds for each level until they beat it
    private List<int> levelAttemptsPostCompletion; // Holds for each level until they get 3 Stars
    private List<bool> levelCompletion;

    // Current Attempt
    private bool passedAlready; // Determine whether counted for pre or post
    private bool counted; // To account for quits or closes even after one tile
    private bool ended;
    private string firstTile; // "board,x,y,tile" formatting

    private Dictionary<Vector3Int, List<TileType?>> tilePlacements = new Dictionary<Vector3Int, List<TileType?>>();
    private List<Step> steps = new List<Step>(); // Mirrors undo history
    private List<(int step, HashSet<string> group)> deliveries = new List<(int, HashSet<string>)>();
    private int takeBackFrame = -1; //a  move reports a take-back, then a placement, in the same frame

    private struct Step
    {
        public GameActionKind kind; // To track PlaceTile or TakeBack
        public Vector3Int at, from; // From is only for a move made
        public TileType tile;
        public bool move;
    }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            gameManagerScript = gameManager.GetComponent<GameManager>();
            sessionID = Guid.NewGuid().ToString();
            levelAttemptsPreCompletion = new List<int>();
            levelAttemptsPostCompletion = new List<int>();
            levelCompletion = new List<bool>(LEVEL_COUNT);
            //tilePlacements = new Dictionary<Vector3Int, List<TileType?>>();

        }
        else
        {
            Destroy(gameObject);
        }
    }


    private void OnEnable()

    {
        if (Application.isEditor && !sendInEditor) return;
        gameManagerScript.ActionDone += eventSub;

    }


    private void OnDisable()
    {
        if (Application.isEditor && !sendInEditor) return;
        gameManagerScript.ActionDone -= eventSub;
    }


    public void StartLevel(int level)
    {
        //levelBeat = false;
        currLevel = level + 1;
        while (levelCompletion.Count < currLevel)
        {
            levelAttemptsPreCompletion.Add(0);
            levelAttemptsPostCompletion.Add(0);
            levelCompletion.Add(false);
        }
        passedAlready = levelCompletion[currLevel - 1];
        counted = false;
        ended = false;
        firstTile = null;
        tilePlacements.Clear();
        steps.Clear();
        deliveries.Clear();
        levelStartTime = Time.realtimeSinceStartup;
    }


    public void eventSub(GameAction gameAction)
    {
        Debug.Log($"GETTING the EVENT");
        if (currLevel == 0) return;
        if (gameAction.kind == GameActionKind.Undo || gameAction.kind == GameActionKind.TakeBack)
        {
            int board = gameAction.board;
            Vector2Int cellNum = gameAction.square;
            tilePlacements.Remove(new Vector3Int(board, cellNum.x, cellNum.y));
            finalPiecesUsed--;
        }
        else if (gameAction.kind == GameActionKind.PlaceTile)
        {
            int board = gameAction.board;
            Vector2Int cellNum = gameAction.square;
            Vector3Int fullCell = new Vector3Int(board, cellNum.x, cellNum.y);
            if (!tilePlacements.TryGetValue(fullCell, out List<TileType?> tilesHistory))
            {
                tilesHistory = new List<TileType?>();
                tilePlacements[fullCell] = tilesHistory;
            }
            tilePlacements[fullCell].Add(gameAction.tile);
            finalPiecesUsed++;
        }
        else if (gameAction.kind == GameActionKind.Restart)
        {
            // Send telemetrics only if they haven't beaten the level on the same attempt (to account for restarting after beating the level
            // immediately
            if (levelBeat)
            {
                levelBeat = false;
            }
            else
            {

                if (currLevel != 0)
                {
                    // Fine if sent multiple times over, google sheet will replace duplicates with the one with more attempts
                    if (levelCompletion[currLevel - 1])
                    {
                        levelAttemptsPostCompletion[currLevel - 1]++;
                        StartCoroutine(FillOutAnalytics("LevelAttemptsPostCompletion",
                                  sessionID.ToString(),
                                  currLevel.ToString(),
                                  (Time.realtimeSinceStartup - levelStartTime).ToString("0.00"),
                                  finalPiecesUsed.ToString(), gameAction.stars.ToString(), gameAction.score.ToString(),
                                  levelAttemptsPreCompletion[currLevel - 1].ToString(),
                                  levelAttemptsPostCompletion[currLevel - 1].ToString(),
                                  "0"));
                    }
                    else
                    {
                        if (gameAction.stars >= Levels.All[currLevel - 1].starsToPass) // passed = the next level unlocks
                        {
                            levelCompletion[currLevel - 1] = true;
                        }
                        levelAttemptsPreCompletion[currLevel - 1]++;
                        StartCoroutine(FillOutAnalytics("LevelAttemptsPreCompletion",
                                  sessionID.ToString(),
                                  currLevel.ToString(),
                                  (Time.realtimeSinceStartup - levelStartTime).ToString("0.00"),
                                  finalPiecesUsed.ToString(), gameAction.stars.ToString(), gameAction.score.ToString(),
                                  levelAttemptsPreCompletion[currLevel - 1].ToString(),
                                  levelAttemptsPostCompletion[currLevel - 1].ToString(),
                                  "0"));
                    }
                }


            }
        }
        else if (gameAction.kind == GameActionKind.LevelEnd)
        {

            // the level's own best plans - i only changed this bc its 28 lvls now
            LevelSolution currSolution = Levels.All[currLevel - 1].solution;
            if (currSolution == null) return;

            float maxSimilarity = 0.0f;
            foreach (RoadFamily currFamily in currSolution.families)
            {
                Dictionary<Vector3Int, List<TileType?>> solutionHolder = new Dictionary<Vector3Int, List<TileType?>>();
                foreach (AllowedTiles currAllowed in currFamily.squares)
                {
                    int tempBoard = currAllowed.at.board;
                    Vector3Int tempCoords = new Vector3Int(tempBoard, currAllowed.at.square.x, currAllowed.at.square.y);
                    List<TileType?> tempTiles = new List<TileType?>();
                    foreach (TileType tileType in currAllowed.tiles)
                    {
                        tempTiles.Add(tileType);
                    }

                    solutionHolder.Add(tempCoords, tempTiles);

                }

                // Amount that is taken away from the starting similarity of 100 to simulate
                // deviation from the optimal solution
                float startingSimilarity = 100.0f;
                float deviationAmount = startingSimilarity / solutionHolder.Count; // Maybe should change this to be total squares across the boards
                float halfDeviation = deviationAmount / 2.0f;

                foreach (var (cellPos, tiles) in tilePlacements)
                {

                    if (solutionHolder.TryGetValue(cellPos, out List<TileType?> compareTile))
                    {
                        // Cell Pos is correct but what about tile selection
                        foreach (TileType userPlaced in tiles)
                        {
                            if (!compareTile.Contains(userPlaced))
                            {
                                startingSimilarity -= halfDeviation;
                                break;
                            }
                        }
                    }
                    else
                    {
                        // Neither are correct
                        startingSimilarity -= deviationAmount;
                    }

                }

                if (startingSimilarity > maxSimilarity)
                {
                    maxSimilarity = startingSimilarity;
                }
            }

            StartCoroutine(FillOutAnalytics("LevelComplete",
                          sessionID.ToString(),
                          currLevel.ToString(),
                          (Time.realtimeSinceStartup - levelStartTime).ToString("0.00"),
                          finalPiecesUsed.ToString(), gameAction.stars.ToString(), gameAction.score.ToString(),
                          levelAttemptsPreCompletion[currLevel - 1].ToString(),
                          levelAttemptsPostCompletion[currLevel - 1].ToString(),
                          maxSimilarity.ToString()));

            if (currLevel != 0)
            {
                // Fine if sent multiple times over, google sheet will replace duplicates with the one with more attempts
                if (levelCompletion[currLevel - 1])
                {
                    levelAttemptsPostCompletion[currLevel - 1]++;
                    StartCoroutine(FillOutAnalytics("LevelAttemptsPostCompletion",
                              sessionID.ToString(),
                              currLevel.ToString(),
                              (Time.realtimeSinceStartup - levelStartTime).ToString("0.00"),
                              finalPiecesUsed.ToString(), gameAction.stars.ToString(), gameAction.score.ToString(),
                              levelAttemptsPreCompletion[currLevel - 1].ToString(),
                              levelAttemptsPostCompletion[currLevel - 1].ToString(),
                              maxSimilarity.ToString()));
                }
                else
                {
                    if (gameAction.stars >= Levels.All[currLevel - 1].starsToPass) // passed = the next level unlocks
                    {
                        levelCompletion[currLevel - 1] = true;
                    }
                    levelAttemptsPreCompletion[currLevel - 1]++;
                    StartCoroutine(FillOutAnalytics("LevelAttemptsPreCompletion",
                              sessionID.ToString(),
                              currLevel.ToString(),
                              (Time.realtimeSinceStartup - levelStartTime).ToString("0.00"),
                              finalPiecesUsed.ToString(), gameAction.stars.ToString(), gameAction.score.ToString(),
                              levelAttemptsPreCompletion[currLevel - 1].ToString(),
                              levelAttemptsPostCompletion[currLevel - 1].ToString(),
                              maxSimilarity.ToString()));
                }
            }

            finalPiecesUsed = 0;
            tilePlacements.Clear();
            levelBeat = true;
        }
    }

    // Attempts are counted when it starts: its first tile, or the end screen's Undo (or level end if no tile).
    // Fine if sent multiple times over, google sheet will replace duplicates with the one with more attempts
    private void CountAttempt()
    {
        if (counted || currLevel == 0) return;
        counted = true;
        int i = currLevel - 1;
        if (passedAlready)
        {
            levelAttemptsPostCompletion[i]++;
            StartCoroutine(Send(levelAttemptsPostForm, new Dictionary<string, string>
            {
                { "entry.1181161218", sessionID },
                { "entry.336054205", currLevel.ToString() },
                { "entry.820996616", levelAttemptsPostCompletion[i].ToString() },
            }));
        }
        else
        {
            levelAttemptsPreCompletion[i]++;
            StartCoroutine(Send(levelAttemptsPreForm, new Dictionary<string, string>
            {
                { "entry.795127675", sessionID },
                { "entry.575500443", currLevel.ToString() },
                { "entry.1099898166", levelAttemptsPreCompletion[i].ToString() },
            }));
        }
    }
    private static Vector3Int GetCell(GameAction a) => new Vector3Int(a.board, a.square.x, a.square.y);
    private static Vector3Int GetKey(Square s) => new Vector3Int(s.board, s.square.x, s.square.y);
    private static string LogEntry(Vector3Int cell, TileType tile) => $"{cell.x},{cell.y},{cell.z},{tile}"; // board,x,y,tile

    private void Add(Vector3Int fullCell, TileType tile)
    {
        if (!tilePlacements.TryGetValue(fullCell, out List<TileType?> list)) tilePlacements[fullCell] = list = new List<TileType?>();
        list.Add(tile);
    }

    private void Remove(Vector3Int fullCell)
    {
        if (!tilePlacements.TryGetValue(fullCell, out List<TileType?> list) || list.Count == 0) return;
        list.RemoveAt(list.Count - 1);
        if (list.Count == 0) tilePlacements.Remove(fullCell);
    }

    // Members must be passed in to copy values from start of coroutine so they aren't modified immediately after it's
    // called due to levelStart overwriting their values in the class right after EndLevel is called
    // Only really a problem when level is reset because these calls are on the same frame
    private IEnumerator Send(string url, Dictionary<string, string> formData)
    {
        using (UnityWebRequest webReq = UnityWebRequest.Post(url, formData))
        {
            yield return webReq.SendWebRequest();

            if (webReq.result == UnityWebRequest.Result.Success)
                Debug.Log("Telemetry Form Successfully Sent");
            else
                Debug.LogError($"Error when submitting Telemetry Form: {webReq.error}");
        }
    }


    private IEnumerator FillOutAnalytics(string formType, string mySession, string finalLevel,
                                         string timeElapsed, string totalPieces, string totalStars, string totalScore,
                                         string levelAttemptsPre, string levelAttemptsPost, string similarity)
    {
        // Members must be passed in to copy values from start of coroutine so they aren't modified immediately after it's
        // called due to levelStart overwriting their values in the class right after EndLevel is called
        // Only really a problem when level is reset because these calls are on the same frame

        Dictionary<string, string> formData = new Dictionary<string, string>();

        // All questions are listed in order of how they are in
        // their respective Google Form
        string formToSend = "";

        if (formType.Equals("LevelComplete"))
        {
            formToSend = levelCompletionForm;
            formData.Add("entry.825569823", mySession);
            formData.Add("entry.1775572003", finalLevel);
            formData.Add("entry.348468337", timeElapsed);
            formData.Add("entry.1155704640", totalPieces);
            formData.Add("entry.1940427468", totalStars);
            formData.Add("entry.1679653451", totalScore);
            formData.Add("entry.1315119076", similarity);
        }
        else if (formType.Equals("LevelAttemptsPreCompletion"))
        {
            formToSend = levelAttemptsPreForm;
            formData.Add("entry.795127675", mySession);
            formData.Add("entry.575500443", finalLevel);
            formData.Add("entry.1099898166", levelAttemptsPre);
        }
        else if (formType.Equals("LevelAttemptsPostCompletion"))
        {
            formToSend = levelAttemptsPostForm;
            formData.Add("entry.1181161218", mySession);
            formData.Add("entry.336054205", finalLevel);
            formData.Add("entry.820996616", levelAttemptsPost);
        }

        using (UnityWebRequest webReq = UnityWebRequest.Post(formToSend, formData))
        {
            yield return webReq.SendWebRequest();

            if (webReq.result == UnityWebRequest.Result.Success)
            {
                Debug.Log("Telemetry Form Successfully Sent");
            }
            else
            {
                Debug.LogError($"Error when submitting Telemetry Form: {webReq.error}");
            }
        }

    }
}
