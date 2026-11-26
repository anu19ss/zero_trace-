using UnityEngine;

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

        if (other.GetComponent<GuardAI>() != null ||
            other.GetComponent<CopAI>() != null)
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

        if (SomeoneInDoorway()) closeAt = Time.time + 0.5f;
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
            if (h.CompareTag("Player") ||
                h.GetComponent<GuardAI>() != null ||
                h.GetComponent<CopAI>() != null) return true;
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