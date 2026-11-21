using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// the game's sounds. drag clips into the slots: a slot can hold several (one is picked at random each time), and an
// empty slot is silent. the music follows the screen (menu or level); button presses play Click (or their own sound)
// by themselves; the game plays the rest (AudioManager.Play). volumes come from Settings > Sound
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [System.Serializable]
    public class Slot
    {
        public AudioClip[] clips = new AudioClip[0];
        [Range(0f, 1f)] public float volume = 1f;
        [Tooltip("A random pitch change each time (0.05 = up to 5% higher or lower), so repeats don't all sound the same")]
        [Range(0f, 0.3f)] public float pitchVariation;
        public bool Empty => clips == null || clips.Length == 0;
    }

    [Header("Music (loops, crossfades between the menus and a level)")]
    public AudioClip menuMusic;
    public AudioClip levelMusic;
    [Range(0f, 1f)] public float musicVolume = 0.6f;
    [Min(0f)] public float crossfadeSeconds = 1f;

    [Header("UI")]
    [Tooltip("Any button, unless it has its own sound (Undo and Restart use this too)")]
    public Slot click = new Slot { volume = 0.6f };
    [Tooltip("A Settings switch (empty: Click)")]
    public Slot toggle = new Slot { volume = 0.6f };

    [Header("Tiles")]
    [Tooltip("A supply tile picked or dragged")]
    public Slot pickTile = new Slot { volume = 0.5f };
    [Tooltip("A tile goes on the board: the most heard sound, keep it soft")]
    public Slot placeTile = new Slot { volume = 0.7f, pitchVariation = 0.05f };
    [Tooltip("A tile goes back to the supply")]
    public Slot takeBack = new Slot { volume = 0.5f };
    [Tooltip("The arrows shake: something that can't be used was clicked")]
    public Slot nope = new Slot { volume = 0.5f };

    [Header("Objects")]
    [Tooltip("An object moves onto a tile: very quiet")]
    public Slot step = new Slot { volume = 0.25f, pitchVariation = 0.08f };
    [Tooltip("An object stops at a + or T (or a door) and waits for a way")]
    public Slot arrowsAppear = new Slot { volume = 0.6f };
    [Tooltip("An arrow is clicked (instead of Click)")]
    public Slot arrowPicked = new Slot { volume = 0.6f };
    [Tooltip("Through a door")]
    public Slot door = new Slot { volume = 0.6f };
    [Tooltip("An object reaches its ring. Several in one move: each next one plays higher (up the major scale)")]
    public Slot delivered = new Slot { volume = 0.8f };
    private int successiveDeliveries = 0;
    [Tooltip("A delivery breaks a tile")]
    public Slot tileBreak = new Slot { volume = 0.7f, pitchVariation = 0.05f };
    [Tooltip("Delivering together earns a + tile")]
    public Slot plusEarned = new Slot { volume = 0.7f };

    [Header("Level")]
    [Tooltip("The end screen opens and the level is passed")]
    public Slot levelComplete = new Slot { volume = 0.8f };
    [Tooltip("Optional: one note per star on the end screen, each higher")]
    public Slot star = new Slot { volume = 0.7f };
    [Min(0f)] public float starGap = 0.18f;
    [Tooltip("The stuck notice appears")]
    public Slot stuckNotice = new Slot { volume = 0.6f };
    [Tooltip("The Hint button, either stage (instead of Click)")]
    public Slot hint = new Slot { volume = 0.7f };

    [Header("Found by itself (set only if there are several)")]
    public GameManager game;
    public MenuView menu;
    public HintButton hintButton;

    AudioSource musicA, musicB; // crossfading: the one playing and the one fading out
    readonly List<AudioSource> pool = new List<AudioSource>();
    int next;
    bool takeBackPending; // a take-back waits a frame: a tile moved on the board is a take-back and a placement at once
    bool placedThisFrame;

    void Awake()
    {
        Instance = this;
        musicA = NewSource(true);
        musicB = NewSource(true);
        for (int i = 0; i < 10; i++) pool.Add(NewSource(false));
        if (game == null) game = FindFirstObjectByType<GameManager>();
        if (menu == null) menu = FindFirstObjectByType<MenuView>(FindObjectsInactive.Include);
        if (hintButton == null) hintButton = FindFirstObjectByType<HintButton>(FindObjectsInactive.Include);
    }

    void OnEnable() { if (game != null) game.ActionDone += OnAction; }
    void OnDisable() { if (game != null) game.ActionDone -= OnAction; }

    AudioSource NewSource(bool loop)
    {
        var s = gameObject.AddComponent<AudioSource>();
        s.playOnAwake = false;
        s.loop = loop;
        return s;
    }

    void Update()
    {
        FollowScreen();
        ButtonPressed();
        musicA.volume = Mathf.MoveTowards(musicA.volume, musicVolume * Session.musicVolume, Time.unscaledDeltaTime / Mathf.Max(0.01f, crossfadeSeconds));
        musicB.volume = Mathf.MoveTowards(musicB.volume, 0f, Time.unscaledDeltaTime / Mathf.Max(0.01f, crossfadeSeconds));
        if (musicB.isPlaying && musicB.volume <= 0f) musicB.Stop();
    }

    void LateUpdate()
    {
        if (takeBackPending && !placedThisFrame) PlaySlot(takeBack);
        takeBackPending = placedThisFrame = false;
    }

    // ---- music

    void FollowScreen()
    {
        bool inLevel = menu != null && menu.gameScreen != null && menu.gameScreen.activeInHierarchy;
        var want = inLevel ? levelMusic : menuMusic;
        if (musicA.clip == want) return;
        // the old track fades out on B while the new one fades in on A
        (musicA, musicB) = (musicB, musicA);
        musicA.clip = want;
        musicA.volume = 0f;
        if (want != null) musicA.Play();
    }

    // ---- effects

    public enum Sfx { Click, Toggle, PickTile, PlaceTile, TakeBack, Nope, Step, ArrowsAppear, ArrowPicked, Door, Delivered, TileBreak, PlusEarned, LevelComplete, Star, StuckNotice, Hint }

    Slot SlotFor(Sfx id) => id switch
    {
        Sfx.Click => click,
        Sfx.Toggle => toggle.Empty ? click : toggle,
        Sfx.PickTile => pickTile,
        Sfx.PlaceTile => placeTile,
        Sfx.TakeBack => takeBack,
        Sfx.Nope => nope,
        Sfx.Step => step,
        Sfx.ArrowsAppear => arrowsAppear,
        Sfx.ArrowPicked => arrowPicked.Empty ? click : arrowPicked,
        Sfx.Door => door,
        Sfx.Delivered => delivered,
        Sfx.TileBreak => tileBreak,
        Sfx.PlusEarned => plusEarned,
        Sfx.LevelComplete => levelComplete,
        Sfx.Star => star,
        Sfx.StuckNotice => stuckNotice,
        Sfx.Hint => hint.Empty ? click : hint,
        _ => null,
    };

    // a sound by name; pitch: 1 = as recorded (see Note for a musical step up)
    public static void Play(Sfx id, float pitch = 1f)
    {
        if (Instance != null) Instance.PlaySlot(Instance.SlotFor(id), pitch);
    }

    // the i-th note up the major scale from the recorded one (0 = as recorded), for sounds that climb
    public static float Note(int i)
    {
        int[] major = { 0, 2, 4, 5, 7, 9, 11, 12, 14, 16, 17, 19 };
        return Mathf.Pow(2f, major[Mathf.Clamp(i, 0, major.Length - 1)] / 12f);
    }

    // the end screen opened: the level-complete sound if it was passed, then a note per star
    public static void LevelEnd(int stars, bool passed)
    {
        if (Instance != null) Instance.StartCoroutine(Instance.EndSounds(stars, passed));
    }

    IEnumerator EndSounds(int stars, bool passed)
    {
        if (passed && !levelComplete.Empty) { PlaySlot(levelComplete); yield return new WaitForSecondsRealtime(starGap * 2f); }
        for (int i = 0; i < stars; i++)
        {
            PlaySlot(star, Note(i * 2)); // 1st, 3rd, 5th: a rising chord
            yield return new WaitForSecondsRealtime(starGap);
        }
    }

    void PlaySlot(Slot s, float pitch = 1f)
    {
        if (s == null || s.Empty) return;
        var clip = s.clips[Random.Range(0, s.clips.Length)];
        if (s == delivered)
        {
            clip = s.clips[successiveDeliveries];
            successiveDeliveries++;
        }
        else if (s == tileBreak)
        {
            // This is guaranteed to happen after delivery chain is done so
            // reset combo here
            successiveDeliveries = 0;
        }

        if (clip == null) return;
        var src = pool[next];
        next = (next + 1) % pool.Count;
        src.pitch = pitch * (1f + Random.Range(-s.pitchVariation, s.pitchVariation));
        src.volume = s.volume * Session.effectsVolume;
        src.clip = clip;
        src.Play();
    }

    // ---- what the game reports

    void OnAction(GameAction a)
    {
        switch (a.kind)
        {
            case GameActionKind.SelectTile: if (a.tile != null) PlaySlot(pickTile); break;
            case GameActionKind.PlaceTile: placedThisFrame = true; PlaySlot(placeTile); break;
            case GameActionKind.TakeBack: takeBackPending = true; break;
            case GameActionKind.ChooseDirection: Play(Sfx.ArrowPicked); break;
            case GameActionKind.Delivered: if (a.arrivals != null && a.arrivals.Length >= 2) PlaySlot(plusEarned); break;
        }
    }

    // ---- button presses: Click, a switch's sound, or the Hint button's. the board, the supply and the arrows are
    // left to the game (a tile picked, an arrow picked...)

    void ButtonPressed()
    {
        var pointer = UnityEngine.InputSystem.Pointer.current;
        var events = EventSystem.current;
        if (pointer == null || events == null || !pointer.press.wasPressedThisFrame) return;
        var hits = new List<RaycastResult>();
        events.RaycastAll(new PointerEventData(events) { position = pointer.position.ReadValue() }, hits);
        var top = hits.Count > 0 ? hits[0].gameObject : null;
        var sel = top != null ? top.GetComponentInParent<Selectable>() : null;
        if (sel == null || !sel.IsInteractable() || sel is Slider || sel is Scrollbar) return;
        var board = game != null ? game.board : null;
        if (board != null && (board.OnSquare(top) || board.IsDoor(top) || top.transform.IsChildOf(board.choicesRoot))) return;
        if (game != null && game.hud != null && game.hud.OnDock(top)) return;
        if (hintButton != null && hintButton.button == sel) { Play(Sfx.Hint); return; }
        Play(sel is Toggle ? Sfx.Toggle : Sfx.Click);
    }
}
