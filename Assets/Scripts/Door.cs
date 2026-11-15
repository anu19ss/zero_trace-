using UnityEngine;

// Added automatically by MuseumBuilder to keycard doors.
// The thief opens a door by walking into it with the matching keycard.
// Guards open every door they bump into (they carry keys).
// A door closes by itself after openTime seconds, once nobody is standing in the doorway.
public class Door : MonoBehaviour
{
    public int number = 1;
    public float openTime = 3f;
    public bool IsOpen { get; private set; }

    SpriteRenderer sr;
    Color baseColor = Color.white;
    Collider2D[] cols;
    float closeAt;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        if (sr != null) baseColor = sr.color;
        cols = GetComponents<Collider2D>();
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (IsOpen) return;

        GameObject other = collision.collider.gameObject;

        if (other.GetComponent<GuardAI>() != null)
        {
            Open();
            return;
        }

        if (!other.CompareTag("Player") || GameManager.Instance == null) return;

        if (GameManager.Instance.HasKey(number)) Open();
        else GameManager.Instance.Message("Door " + number + " is locked. Find keycard " + number + ".");
    }

    void Update()
    {
        if (!IsOpen || Time.time < closeAt) return;

        if (SomeoneInDoorway()) closeAt = Time.time + 0.5f;   // wait until the doorway is clear
        else Close();
    }

    public void Open()
    {
        IsOpen = true;
        closeAt = Time.time + openTime;
        SetColliders(false);
        SetAlpha(0.25f);
    }

    void Close()
    {
        IsOpen = false;
        SetColliders(true);
        if (sr != null) sr.color = baseColor;
    }

    bool SomeoneInDoorway()
    {
        Collider2D[] hits = Physics2D.OverlapBoxAll(transform.position, Vector2.one * 1.2f, 0f);
        foreach (Collider2D h in hits)
        {
            if (h.gameObject == gameObject || h.isTrigger) continue;
            if (h.CompareTag("Player") || h.GetComponent<GuardAI>() != null) return true;
        }
        return false;
    }

    void SetColliders(bool enabledState)
    {
        foreach (Collider2D col in cols) col.enabled = enabledState;
    }

    void SetAlpha(float a)
    {
        if (sr == null) return;
        Color c = baseColor;
        c.a = a;
        sr.color = c;
    }
}