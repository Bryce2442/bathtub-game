using UnityEngine;

[CreateAssetMenu(
    fileName = "Track_",
    menuName = "Bathtub Racing/Track Definition"
)]
public class TrackDefinition : ScriptableObject
{
    [Header("Track Information")]
    public string trackName = "New Track";

    public string subtitle = "";

    [TextArea(3, 8)]
    public string description = "";

    [Header("Track Images")]
    public Texture thumbnail;
    public Texture mapImage;

    [Header("Scene")]
    [Tooltip("The Unity scene name that should be loaded for this track.")]
    public string sceneName = "Racetrack";
}
