using UnityEngine;

public class Pickup : MonoBehaviour
{
    public enum Kind { Loot, Exit, Keycard1, Keycard2 }

    public Kind kind = Kind.Loot;
    public int value = 100;

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player") || GameManager.Instance == null) return;

        switch (kind)
        {
            case Kind.Loot:
                GameManager.Instance.AddLoot(value);
                Destroy(gameObject);
                break;
            case Kind.Keycard1:
                GameManager.Instance.GiveKey(1);
                Destroy(gameObject);
                break;
            case Kind.Keycard2:
                GameManager.Instance.GiveKey(2);
                Destroy(gameObject);
                break;
            case Kind.Exit:
                GameManager.Instance.ReachedExit();
                break;
        }
    }
}
