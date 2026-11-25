
using UnityEngine;
using UnityEngine.SceneManagement;

// Tracks loot, keycards, dog treats, game over and win.
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public int Loot { get; private set; }
    public bool GameOver { get; private set; }
    public bool Won { get; private set; }

    // Dog treat system
    public bool HasDogTreat { get; private set; }
    public int DogTreatCost = 50;
    public float DogTreatUseRadius = 3f;
    public float DogTreatDistractionDuration = 5f;

    bool key1, key2;
    string message = "";
    float messageUntil;

    GUIStyle bigStyle, smallStyle, msgStyle;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoCreate()
    {
        if (Instance == null)
            new GameObject("GameManager").AddComponent<GameManager>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        if (Instance == this)
            SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Loot = 0;
        key1 = key2 = false;
        HasDogTreat = false;

        GameOver = false;
        Won = false;
        message = "";
        Time.timeScale = 1f;
    }

    void Update()
    {
        // Restart game
        if (Input.GetKeyDown(KeyCode.R))
        {
            SceneManager.LoadScene(
                SceneManager.GetActiveScene().buildIndex
            );
        }

        // Buy dog treat
        if (Input.GetKeyDown(KeyCode.B))
        {
            BuyDogTreat();
        }

        // Use dog treat
        if (Input.GetKeyDown(KeyCode.T))
        {
            UseDogTreat();
        }
    }

    public void AddLoot(int value)
    {
        Loot += value;
        Message("Loot +$" + value);
    }

    // Purchase a dog treat using collected loot.
    public bool BuyDogTreat()
    {
        if (HasDogTreat)
        {
            Message("You already have a dog treat!");
            return false;
        }

        if (Loot < DogTreatCost)
        {
            Message("Not enough money for a dog treat!");
            return false;
        }

        Loot -= DogTreatCost;
        HasDogTreat = true;

        Message("Dog treat purchased! Press T to use it.");
        return true;
    }

    // Use a purchased treat to distract the nearest dog.
    public bool UseDogTreat()
    {
        if (!HasDogTreat)
        {
            Message("You don't have a dog treat!");
            return false;
        }

        DogAI[] dogs = FindObjectsOfType<DogAI>();

        DogAI nearestDog = null;
        float nearestDistance = Mathf.Infinity;

        Vector2 playerPosition = GetPlayerPosition();

        foreach (DogAI dog in dogs)
        {
            if (dog == null)
                continue;

            float distance = Vector2.Distance(
                playerPosition,
                dog.transform.position
            );

            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestDog = dog;
            }
        }

        if (nearestDog == null)
        {
            Message("No dog nearby!");
            return false;
        }

        // Try to distract the nearest dog.
        bool distracted = nearestDog.TryDistract(
            playerPosition,
            DogTreatUseRadius,
            DogTreatDistractionDuration
        );

        if (!distracted)
        {
            Message("Get closer to the dog to use the treat!");
            return false;
        }

        // Consume the treat only if distraction succeeds.
        HasDogTreat = false;

        Message("Dog distracted! Run!");
        return true;
    }

    // Finds the player position using the dog's perception system.
    Vector2 GetPlayerPosition()
    {
        DogAI[] dogs = FindObjectsOfType<DogAI>();

        if (dogs.Length > 0)
        {
            DogPerception perception =
                dogs[0].GetComponent<DogPerception>();

            if (perception != null)
                return perception.ThiefPosition;
        }

        // Fallback if no dog perception is available.
        GameObject player = GameObject.FindGameObjectWithTag("Player");

        if (player != null)
            return player.transform.position;

        return Vector2.zero;
    }

    public void GiveKey(int number)
    {
        if (number == 1)
            key1 = true;
        else
            key2 = true;

        Message("Got keycard " + number);
    }

    public bool HasKey(int number)
    {
        return number == 1 ? key1 : key2;
    }

    public void Message(string text)
    {
        message = text;
        messageUntil = Time.unscaledTime + 2.5f;
    }

    public void Caught()
    {
        if (GameOver)
            return;

        GameOver = true;
        Won = false;
        Time.timeScale = 0f;
    }

    public void ReachedExit()
    {
        if (GameOver)
            return;

        if (Loot <= 0)
        {
            Message("Grab some loot before you escape!");
            return;
        }

        GameOver = true;
        Won = true;
        Time.timeScale = 0f;
    }

    void OnGUI()
    {
        if (bigStyle == null)
        {
            bigStyle = new GUIStyle(GUI.skin.label);
            bigStyle.fontSize = 40;
            bigStyle.fontStyle = FontStyle.Bold;
            bigStyle.alignment = TextAnchor.MiddleCenter;
            bigStyle.normal.textColor = Color.white;

            smallStyle = new GUIStyle(GUI.skin.label);
            smallStyle.fontSize = 18;
            smallStyle.normal.textColor = Color.white;

            msgStyle = new GUIStyle(GUI.skin.label);
            msgStyle.fontSize = 22;
            msgStyle.alignment = TextAnchor.MiddleCenter;
            msgStyle.normal.textColor = Color.yellow;
        }

        string keys = (key1 ? "1 " : "") + (key2 ? "2" : "");

        GUI.Label(
            new Rect(10, 8, 600, 28),
            "Loot: $" + Loot + "    Keycards: " + keys,
            smallStyle
        );

        GUI.Label(
            new Rect(10, 32, 850, 28),
            "WASD move | Shift run | Ctrl/C crouch | B buy treat | T use treat | R restart",
            smallStyle
        );

        // Display dog treat status.
        string treatStatus = HasDogTreat
            ? "Dog Treat: Owned - Press T to use"
            : "Dog Treat: $" + DogTreatCost + " - Press B to buy";

        GUI.Label(
            new Rect(10, 56, 600, 28),
            treatStatus,
            smallStyle
        );

        if (Time.unscaledTime < messageUntil)
        {
            GUI.Label(
                new Rect(0, Screen.height - 70, Screen.width, 40),
                message,
                msgStyle
            );
        }

        if (GameOver)
        {
            string text = Won
                ? "ESCAPED with $" + Loot + "!\nPress R to play again"
                : "CAUGHT!\nPress R to restart";

            GUI.Label(
                new Rect(0, 0, Screen.width, Screen.height),
                text,
                bigStyle
            );
        }
    }
}
