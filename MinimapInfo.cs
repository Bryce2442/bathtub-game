using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MinimapInfo : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private RectTransform Track;
    [SerializeField] private RawImage PlayerMarker;

    [Header("Marker Size")]
    [SerializeField] private Vector2 markerSize = new Vector2(2f, 5f);

    [Header("Map Area")]
    [SerializeField] private Vector2 mapMinXZ;
    [SerializeField] private Vector2 mapMaxXZ;

    private readonly Dictionary<Transform, RawImage> markers = new();

    private readonly HashSet<Transform> activeMarkers = new();
    private readonly List<Transform> removeMarkers = new();

    private void LateUpdate()
    {
        if (RaceManager.Instance == null || Track == null || PlayerMarker == null)
            return;

        activeMarkers.Clear();

        foreach (NetworkedKart kart in RaceManager.Instance.NetworkedKarts)
        {
            if (kart == null)
                continue;
            UpdateMarker(kart.transform);
        }

        foreach (OpponentKartAI kart in RaceManager.Instance.KartAIs)
        {
            if (kart == null)
                continue;
            UpdateMarker(kart.transform);
        }

        RemoveOldMarkers();
    }

    private void UpdateMarker(Transform pMarker)
    {
        activeMarkers.Add(pMarker);

        if (!markers.TryGetValue(pMarker, out RawImage markerObj))
        {
            markerObj = Instantiate(PlayerMarker, Track);

            markerObj.gameObject.name = $"Map Marker for: {pMarker.name}";
            markerObj.gameObject.SetActive(true);
            markerObj.raycastTarget = false;

            RectTransform cMarker = markerObj.rectTransform;
            cMarker.anchorMin = new Vector2(0.5f, 0.5f);
            cMarker.anchorMax = new Vector2(0.5f, 0.5f);
            cMarker.pivot = new Vector2(0.5f, 0.5f);
            cMarker.localScale = Vector3.one;
            cMarker.localRotation = Quaternion.identity;
            cMarker.sizeDelta = markerSize;
            cMarker.anchoredPosition =Vector2.zero;

            markers.Add(pMarker, markerObj);
        }

        Vector3 worldPos = pMarker.position;

        float normalizedX = Mathf.InverseLerp(mapMinXZ.x, mapMaxXZ.x, worldPos.x);
        float normalizedY = Mathf.InverseLerp(mapMinXZ.y, mapMaxXZ.y, worldPos.z);

        float trueX = (normalizedX - 0.5f) * Track.rect.width;
        float trueY = (normalizedY - 0.5f) * Track.rect.height;

        markerObj.rectTransform.anchoredPosition = new Vector2(trueX, trueY);
    }

    private void RemoveOldMarkers()
    {
        removeMarkers.Clear();

        foreach (var pair in markers)
        {
            if (pair.Key == null || !activeMarkers.Contains(pair.Key))
            {
                if (pair.Value != null)
                    Destroy(pair.Value.gameObject);
                removeMarkers.Add(pair.Key);
            }
        }
        foreach (Transform pMarker in removeMarkers)
        {
            markers.Remove(pMarker);
        }
    }
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (RaceManager.Instance == null)
            return;

       /* foreach (NetworkedKart kart in RaceManager.Instance.NetworkedKarts)
        {
            if (kart == null)
                continue;
            Debug.Log($"Player: {kart.name} | Place: {kart.transform.position}");
        }

        foreach (OpponentKartAI kart in RaceManager.Instance.KartAIs)
        {
            if (kart == null)
                continue;
            Debug.Log($"CPU: {kart.name} | Place: {kart.transform.position}");
        } */
    }
}
