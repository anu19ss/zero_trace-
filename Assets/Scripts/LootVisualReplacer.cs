
using System.Collections;
using System.Diagnostics;
using UnityEngine;

[DefaultExecutionOrder(10000)]
public class LootVisualReplacer : MonoBehaviour
{
    [Header("Loot Sprites")]
    public Sprite crownSprite;
    public Sprite gobletSprite;

    [Header("Existing Loot Objects")]
    public string firstLootName = "Loot_1";
    public string secondLootName = "Loot_2";

    private IEnumerator Start()
    {
        // Wait until MuseumBuilder has generated the loot objects.
        yield return null;

        ReplaceLootSprite(firstLootName, crownSprite);
        ReplaceLootSprite(secondLootName, gobletSprite);
    }

    private void ReplaceLootSprite(string objectName, Sprite newSprite)
    {
        if (newSprite == null)
        {
            UnityEngine.Debug.LogWarning("Loot sprite is not assigned: " + objectName);
            return;
        }

        GameObject lootObject = GameObject.Find(objectName);

        if (lootObject == null)
        {
            UnityEngine.Debug.LogWarning("Could not find loot object: " + objectName);
            return;
        }

        SpriteRenderer spriteRenderer =
            lootObject.GetComponentInChildren<SpriteRenderer>(true);

        if (spriteRenderer == null)
        {
            spriteRenderer = lootObject.AddComponent<SpriteRenderer>();
        }

        spriteRenderer.sprite = newSprite;
        spriteRenderer.color = Color.white;

        UnityEngine.Debug.Log("Replaced visual for " + objectName);
    }
}
