
using UnityEngine;

[System.Serializable]
public class ScentTrail
{
    public Vector2 Position { get; private set; }

    public float Strength { get; private set; }

    public float InitialStrength => initialStrength;

    private float creationTime;
    private float lifetime;
    private float initialStrength;

    public bool IsExpired =>
        Time.time >= creationTime + lifetime;

    public ScentTrail(
        Vector2 position,
        float strength,
        float creationTime,
        float lifetime)
    {
        Position = position;
        Strength = strength;
        initialStrength = strength;
        this.creationTime = creationTime;
        this.lifetime = lifetime;
    }

    public void UpdateStrength()
    {
        float elapsed = Time.time - creationTime;

        float remaining = Mathf.Clamp01(
            1f - elapsed / lifetime
        );

        Strength = initialStrength * remaining;
    }
}
