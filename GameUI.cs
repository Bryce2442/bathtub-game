using GONet;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

public class GameUI : MonoBehaviour
{
    public GameObject LapParent;
    public TextMeshProUGUI LapCounter;
    public GameObject PlaceParent;
    public TextMeshProUGUI PlaceCounter;
    [Space]
    [Header("Menu")]
    public Button QuitBtn;
    [Space]
    [Header("Race End")]
    public GameObject WinnerPanel;
    public TextMeshProUGUI WinnerText;
    public Transform RaceEndPanel;
    public TextMeshProUGUI EndPanelWinText;
    public TextMeshProUGUI LapTimesText;
    public Button MainMenuBtn;

    void Awake()
    {
        QuitBtn.onClick.AddListener(OnQuitClick);

        MainMenuBtn.onClick.AddListener(OnMainMenuClick);
    }

    void Update()
    {
        if (RaceManager.Instance == null)
            return;

        bool activeClient = RaceManager.TryGetClientKart(out NetworkedKart clientKart);
        if (activeClient)
        {
            var racerInfo = clientKart.RacerInfo;

            int totalLaps = RaceManager.Instance.TotalLaps;
            int displayLap = Mathf.Clamp(racerInfo.CompletedLaps + 1, 0, totalLaps);
            LapCounter.text = $"Lap: {displayLap} / {(totalLaps <= 0 ? "?" : totalLaps.ToString())}";
            PlaceCounter.text = $"Place: {racerInfo.RacerPlace} / {RaceManager.Instance.RacerInfos.Count}";
        }
        LapParent.SetActiveSafe(activeClient);
        PlaceParent.SetActiveSafe(activeClient);
    }

    void OnQuitClick()
    {
        RaceManager.Instance.LeaveGame(false);
    }

    public void ShowEndRaceScreen(string winnerName)
    {
        WinnerText.text = $"WINNER: '{winnerName}'!";
        EndPanelWinText.text = $"WINNER:\n'{winnerName}'!";
        WinnerPanel.SetActive(true);
        getLapTimes();

        //Delay showing the screen.
        StartCoroutine(ShowRaceEndPanelCor());
    }
    IEnumerator ShowRaceEndPanelCor()
    {
        yield return new WaitForSeconds(5f);

        //Unlock cursor.
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        //Disable winner panel.
        WinnerPanel.SetActive(false);
        //Show end screen.
        RaceEndPanel.gameObject.SetActive(true);
        RaceManager.Instance.OnEndRaceScreenShown();
    }
    void OnMainMenuClick()
    {
        RaceManager.Instance.LeaveGame(true);
    }

    public void getLapTimes()
    {
        if (!RaceManager.TryGetClientKart(out NetworkedKart clientKart))
            return;
        RacerInfo racerInfo = clientKart.RacerInfo;

        if (racerInfo == null)
            return;

        string text = "";
        
        for (int i = 0; i < racerInfo.lapTimes.Count; i++)
        {
            float lap = racerInfo.lapTimes[i];

            int minutes = Mathf.FloorToInt(lap / 60);
            int seconds = Mathf.FloorToInt(lap % 60);
            int milliseconds = Mathf.FloorToInt((lap * 100) % 100);
            
            text += ($"Completed Lap {i+1} in: {minutes:00}:{seconds:00}:{milliseconds:00}\n");
        }

        if (racerInfo.lapTimes.Count > 0)
        {
            float avgLapTime = racerInfo.totalTime / racerInfo.lapTimes.Count;
            int minutes = Mathf.FloorToInt(avgLapTime / 60);
            int seconds = Mathf.FloorToInt(avgLapTime % 60);
            int milliseconds = Mathf.FloorToInt((avgLapTime * 100) % 100);

            text += ($"Your Average Lap Time was: {minutes:00}:{seconds:00}:{milliseconds:00}");
        }
        LapTimesText.text = text;
    }
}
