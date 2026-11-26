using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class Distractible : MonoBehaviour
{
    public KeyCode useKey = KeyCode.F;
    public float noiseRadius = 10f;
    public string prompt = "Press F to knock this over";
    public string doneMessage = "Crash! The guards heard that.";

    bool playerInRange;
    bool used;
    SpriteRenderer sr;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
    }

    void Reset()
    {
        Collider2D c = GetComponent<Collider2D>();
        if (c != null) c.isTrigger = true;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        playerInRange = true;
        if (!used && GameManager.Instance != null) GameManager.Instance.Message(prompt);
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player")) playerInRange = false;
    }

    void Update()
    {
        if (used || !playerInRange || !Input.GetKeyDown(useKey)) return;
        Knock();
    }

    void Knock()
    {
        used = true;

        transform.Rotate(0f, 0f, 90f);
        Color c = sr.color;
        c.a = 0.6f;
        sr.color = c;

        GuardAI.AlertWithin(transform.position, noiseRadius);
        if (GameManager.Instance != null) GameManager.Instance.Message(doneMessage);
    }
}