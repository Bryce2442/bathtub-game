using UnityEngine;

public static class TrackSelectionState
{
    public const string SelectedTrackIndexKey = "SelectedTrackIndex";
    public const string SelectedTrackSceneKey = "SelectedTrackScene";

    public static int SelectedTrackIndex =>
        PlayerPrefs.GetInt(SelectedTrackIndexKey, 0);

    public static string SelectedTrackSceneName =>
        PlayerPrefs.GetString(SelectedTrackSceneKey, "Racetrack");

    public static void SaveSelectedTrack(int trackIndex, string sceneName)
    {
        PlayerPrefs.SetInt(SelectedTrackIndexKey, trackIndex);
        PlayerPrefs.SetString(SelectedTrackSceneKey, sceneName);
        PlayerPrefs.Save();
    }
}
