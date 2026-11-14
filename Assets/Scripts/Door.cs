using UnityEngine;

// Added automatically by MuseumBuilder to keycard doors.
// Walk into the door with the matching keycard to open it.
public class Door : MonoBehaviour
{
    public int number = 1;
    public bool IsOpen { get; private set; }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (IsOpen || !collision.collider.CompareTag("Player")) return;
        if (GameManager.Instance == null) return;

        if (GameManager.Instance.HasKey(number)) Open();
        else GameManager.Instance.Message("Door " + number + " is locked. Find keycard " + number + ".");
    }

    public void Open()
    {
        IsOpen = true;
        foreach (Collider2D col in GetComponents<Collider2D>()) col.enabled = false;

        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            Color c = sr.color;
            c.a = 0.25f;
            sr.color = c;
        }
    }
}
