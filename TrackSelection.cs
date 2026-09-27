using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System;

public class TrackSelectionManager : MonoBehaviour
{
    public const string SavedLapCountKey = "LapCount";
    public const int DefaultLapCount = 3;
    public const int MaxLapCount = 50;

    public const string SavedAICountKey = "AICount";
    public const int DefaultAICount = 3;

    [Header("Lap / Opponent Inputs")]
    public TMP_InputField lapsInput; // For 'Number of Laps'
    public TMP_InputField opponentsInput; // For 'Number of Opponents'

    [Space]
    [Header("Main Buttons")]
    public Button BackBtn;
    public Button StartBtn;

    [Space]
    [Header("Lap / Opponent Buttons")]
    public Button LapDecreaseBtn;
    public Button LapIncreaseBtn;
    public Button OpponentDecreaseBtn;
    public Button OpponentIncreaseBtn;

    [Space]
    [Header("Track Selection Buttons")]
    public Button PreviousTrackBtn;
    public Button NextTrackBtn;

    [Space]
    [Header("Track Selection UI")]
    public TMP_Text trackNameText;
    public TMP_Text trackSubtitleText;
    public TMP_Text trackDescriptionText;
    public TMP_Text trackCounterText;
    public RawImage trackThumbnail;
    public RawImage trackMap;

    [Space]
    [Header("Available Tracks")]
    public TrackDefinition[] tracks;

    private int currentTrackIndex = 0;

    public int MaxAICount => SteamManager.InSteamLobby
        //If we're in a lobby, max AI count is max players - current players (including host).
        ? NetworkManager.MaxPlayerCount - SteamManager.GetLobbyPlayerCount()
        : NetworkManager.MaxPlayerCount - 1;
    //If we're in a lobby and there's at least another player, allow 0 AI opponents.
    public int MinAICount => SteamManager.InSteamLobby && SteamManager.GetLobbyPlayerCount() > 1 ? 0 : 1;

    void Awake()
    {
        lapsInput.onValueChanged.AddListener(OnLapsInputChanged);
        opponentsInput.onValueChanged.AddListener(OnOpponentsInputChanged);

        BackBtn.onClick.AddListener(OnBackClick);
        StartBtn.onClick.AddListener(OnStartClick);

        LapDecreaseBtn.onClick.AddListener(OnLapDecreaseClick);
        LapIncreaseBtn.onClick.AddListener(OnLapIncreaseClick);

        OpponentDecreaseBtn.onClick.AddListener(OnOpponentDecreaseClick);
        OpponentIncreaseBtn.onClick.AddListener(OnOpponentIncreaseClick);

        PreviousTrackBtn.onClick.AddListener(OnPreviousTrackClick);
        NextTrackBtn.onClick.AddListener(OnNextTrackClick);
    }

    void OnEnable()
    {
        //Load saved values.
        lapsInput.text = GetSavedLapCount().ToString();
        opponentsInput.text = GetSavedAICount().ToString();

        //Load the last selected track.
        if (tracks != null && tracks.Length > 0)
        {
            currentTrackIndex = Mathf.Clamp(
                TrackSelectionState.SelectedTrackIndex,
                0,
                tracks.Length - 1
            );

            UpdateTrackUI();
        }
        else
        {
            Debug.LogWarning(
                $"{nameof(TrackSelectionManager)}: No TrackDefinition assets have been assigned."
            );
        }
    }

    void OnLapsInputChanged(string input)
    {
        if (int.TryParse(input, out int parsedLaps))
        {
            parsedLaps = Mathf.Clamp(parsedLaps, 1, MaxLapCount);
            lapsInput.SetTextWithoutNotify(parsedLaps.ToString());
        }
        else
            lapsInput.SetTextWithoutNotify(DefaultLapCount.ToString());
    }

    void OnOpponentsInputChanged(string input)
    {
        if (int.TryParse(input, out int parsedAICount))
        {
            parsedAICount = Mathf.Clamp(parsedAICount, MinAICount, MaxAICount);
            opponentsInput.SetTextWithoutNotify(parsedAICount.ToString());
        }
        else
            opponentsInput.SetTextWithoutNotify(DefaultAICount.ToString());
    }

    void OnLapDecreaseClick()
    {
        int.TryParse(lapsInput.text, out int parsedLaps);

        parsedLaps--;
        parsedLaps = Mathf.Clamp(parsedLaps, 1, MaxLapCount);

        lapsInput.text = parsedLaps.ToString();

        ClearButtonSelection();
    }

    void OnLapIncreaseClick()
    {
        int.TryParse(lapsInput.text, out int parsedLaps);

        parsedLaps++;
        parsedLaps = Mathf.Clamp(parsedLaps, 1, MaxLapCount);

        lapsInput.text = parsedLaps.ToString();

        ClearButtonSelection();
    }

    void OnOpponentDecreaseClick()
    {
        int.TryParse(opponentsInput.text, out int parsedAICount);

        parsedAICount--;
        parsedAICount = Mathf.Clamp(parsedAICount, MinAICount, MaxAICount);

        opponentsInput.text = parsedAICount.ToString();

        ClearButtonSelection();
    }

    void OnOpponentIncreaseClick()
    {
        int.TryParse(opponentsInput.text, out int parsedAICount);

        parsedAICount++;
        parsedAICount = Mathf.Clamp(parsedAICount, MinAICount, MaxAICount);

        opponentsInput.text = parsedAICount.ToString();

        ClearButtonSelection();
    }

    void OnPreviousTrackClick()
    {
        ChangeTrack(-1);
        ClearButtonSelection();
    }

    void OnNextTrackClick()
    {
        ChangeTrack(1);
        ClearButtonSelection();
    }

    void ChangeTrack(int direction)
    {
        if (tracks == null || tracks.Length == 0)
            return;

        currentTrackIndex += direction;

        //Wrap around from the first track to the last track and vice versa.
        if (currentTrackIndex < 0)
            currentTrackIndex = tracks.Length - 1;
        else if (currentTrackIndex >= tracks.Length)
            currentTrackIndex = 0;

        UpdateTrackUI();
    }

    void UpdateTrackUI()
    {
        if (tracks == null || tracks.Length == 0)
            return;

        TrackDefinition track = tracks[currentTrackIndex];

        if (track == null)
            return;

        if (trackNameText != null)
            trackNameText.text = track.trackName;

        if (trackSubtitleText != null)
            trackSubtitleText.text = track.subtitle;

        if (trackDescriptionText != null)
            trackDescriptionText.text = track.description;

        if (trackThumbnail != null)
            trackThumbnail.texture = track.thumbnail;

        if (trackMap != null)
            trackMap.texture = track.mapImage;

        if (trackCounterText != null)
            trackCounterText.text = $"{currentTrackIndex + 1} / {tracks.Length}";
    }

    void ClearButtonSelection()
    {
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);
    }

    void OnBackClick()
    {
        MainMenuManager.Instance.ShowTrackSelection(false);
    }

    void OnStartClick()
    {
        int.TryParse(lapsInput.text, out int parsedLaps);
        parsedLaps = Mathf.Clamp(parsedLaps, 1, MaxLapCount);
        int.TryParse(opponentsInput.text, out int parsedAICount);
        parsedAICount = Mathf.Clamp(parsedAICount, MinAICount, MaxAICount);

        //int laps = 3;
        //if (!int.TryParse(lapsInput.text, out laps))
        //    laps = 3;
        //// Lap Limit
        //laps = Mathf.Clamp(laps, 1, 50);

        //Save to player prefs.
        PlayerPrefs.SetInt(SavedLapCountKey, parsedLaps);
        PlayerPrefs.SetInt(SavedAICountKey, parsedAICount);

        //Save the selected track.
        if (tracks != null && tracks.Length > 0 && tracks[currentTrackIndex] != null)
        {
            TrackDefinition selectedTrack = tracks[currentTrackIndex];

            TrackSelectionState.SaveSelectedTrack(
                currentTrackIndex,
                selectedTrack.sceneName
            );

            Debug.Log(
                $"{nameof(TrackSelectionManager)}: Selected track: " +
                $"{selectedTrack.trackName}, scene: {selectedTrack.sceneName}"
            );
        }

        PlayerPrefs.Save();

        Debug.Log($"{nameof(TrackSelectionManager)}: Set lap count: {parsedLaps}, set opponent count: {parsedAICount}");

        //Start the game.
        NetworkManager.Instance.StartGame();
    }

    public static int GetSavedLapCount()
    {
        return PlayerPrefs.GetInt(SavedLapCountKey, DefaultLapCount);
    }

    public static int GetSavedAICount()
    {
        return PlayerPrefs.GetInt(SavedAICountKey, DefaultAICount);
    }
}
