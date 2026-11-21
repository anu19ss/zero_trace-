
using System.Collections.Generic;
using UnityEngine;

public class ScentManager : MonoBehaviour
{
    public static ScentManager Instance { get; private set; }

    [Header("Scent Settings")]
    public float scentInterval = 0.5f;
    public float scentLifetime = 15f;

    public float runningScentStrength = 1f;
    public float walkingScentStrength = 0.6f;
    public float crouchingScentStrength = 0.2f;

    [Header("Visual Settings")]
    public GameObject scentMarkerPrefab;

    public Color runningColor = Color.red;
    public Color walkingColor = Color.yellow;
    public Color crouchingColor = Color.cyan;

    private readonly List<ScentTrail> scentTrails =
        new List<ScentTrail>();

    private readonly List<GameObject> scentMarkers =
        new List<GameObject>();

    private ThiefController thief;

    private float scentTimer;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    void Start()
    {
        GameObject player =
            GameObject.FindGameObjectWithTag("Player");

        if (player != null)
        {
            thief = player.GetComponent<ThiefController>();
        }

        if (thief == null)
        {
            Debug.LogError(
                "ScentManager: ThiefController not found!"
            );
        }

        if (scentMarkerPrefab == null)
        {
            Debug.LogError(
                "ScentManager: Assign the ScentMarker prefab!"
            );
        }
    }

    void Update()
    {
        if (thief == null || scentMarkerPrefab == null)
            return;

        scentTimer += Time.deltaTime;

        if (scentTimer >= scentInterval)
        {
            scentTimer = 0f;
            CreateScent();
        }

        UpdateScentTrails();
    }

    void CreateScent()
    {
        // No scent when standing still.
        if (thief.CurrentNoiseRadius <= 0f &&
            !thief.IsCrouching)
        {
            return;
        }

        float strength;
        Color scentColor;

        if (thief.IsCrouching)
        {
            strength = crouchingScentStrength;
            scentColor = crouchingColor;
        }
        else if (thief.CurrentNoiseRadius >= 6f)
        {
            strength = runningScentStrength;
            scentColor = runningColor;
        }
        else
        {
            strength = walkingScentStrength;
            scentColor = walkingColor;
        }

        // Store the scent information.
        ScentTrail trail = new ScentTrail(
            thief.transform.position,
            strength,
            Time.time,
            scentLifetime
        );

        scentTrails.Add(trail);

        // Create a visible marker in the Game view.
        GameObject marker = Instantiate(
            scentMarkerPrefab,
            thief.transform.position,
            Quaternion.identity
        );

        SpriteRenderer sprite =
            marker.GetComponent<SpriteRenderer>();

        if (sprite != null)
        {
            sprite.color = scentColor;
        }

        scentMarkers.Add(marker);
    }

    void UpdateScentTrails()
    {
        for (int i = scentTrails.Count - 1; i >= 0; i--)
        {
            ScentTrail trail = scentTrails[i];

            trail.UpdateStrength();

            if (i < scentMarkers.Count &&
                scentMarkers[i] != null)
            {
                SpriteRenderer sprite =
                    scentMarkers[i].GetComponent<SpriteRenderer>();

                if (sprite != null)
                {
                    Color color = sprite.color;

                    color.a = trail.Strength /
                        Mathf.Max(trail.InitialStrength, 0.001f);

                    sprite.color = color;
                }
            }

            // Remove expired scent traces and their markers.
            if (trail.IsExpired)
            {
                if (i < scentMarkers.Count &&
                    scentMarkers[i] != null)
                {
                    Destroy(scentMarkers[i]);
                }

                scentTrails.RemoveAt(i);
                scentMarkers.RemoveAt(i);
            }
        }
    }

    public ScentTrail GetStrongestScent(Vector2 dogPosition)
    {
        ScentTrail strongest = null;
        float highestStrength = 0f;

        foreach (ScentTrail trail in scentTrails)
        {
            float distance = Vector2.Distance(
                dogPosition,
                trail.Position
            );

            if (trail.Strength > highestStrength)
            {
                highestStrength = trail.Strength;
                strongest = trail;
            }
        }

        return strongest;
    }
}
