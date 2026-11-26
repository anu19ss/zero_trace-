
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class HidingSpot : MonoBehaviour
{
    public string prompt = "Press E to Hide";

    void Reset()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        ThiefController thief = other.GetComponent<ThiefController>();

        if (thief != null)
            thief.SetNearbyHidingSpot(this);
    }

    void OnTriggerExit2D(Collider2D other)
    {
        ThiefController thief = other.GetComponent<ThiefController>();

        if (thief != null)
            thief.RemoveNearbyHidingSpot(this);
    }
}
