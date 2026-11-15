using UnityEngine;

// Added automatically by MuseumBuilder to every laser cell.
// All lasers blink together: on for onTime seconds, off for offTime seconds.
// Touching a laser while it is on alerts the nearest guards.
public class Laser : MonoBehaviour
{
    public static float onTime = 2.5f;
    public static float offTime = 1.5f;
    public int guardsToAlert = 2;

    static float nextAlertTime;

    SpriteRenderer sr;
    Color baseColor;

    public static bool IsOn
    {
        get { return Time.time % (onTime + offTime) < onTime; }
    }

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        baseColor = sr.color;
    }

    void Update()
    {
        Color c = baseColor;
        c.a = IsOn ? 1f : 0.12f;
        sr.color = c;
    }

    void OnTriggerStay2D(Collider2D other)
    {
        if (!IsOn || !other.CompareTag("Player") || Time.time < nextAlertTime) return;

        nextAlertTime = Time.time + 4f;
        GuardAI.AlertNearest(other.transform.position, guardsToAlert);
        if (GameManager.Instance != null) GameManager.Instance.Message("Laser tripped! Guards are coming.");
    }
}
