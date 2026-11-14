using UnityEngine;
using UnityEngine.SceneManagement;

// Tracks loot, keycards, game over and win. It creates itself when you press Play,
// so you do not need to add it to the scene.
// Press R at any time to restart. (Add the scene in File > Build Settings > Add Open Scenes.)
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public int Loot { get; private set; }
    public bool GameOver { get; private set; }
    public bool Won { get; private set; }

    bool key1, key2;
    string message = "";
    float messageUntil;
    GUIStyle bigStyle, smallStyle, msgStyle;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoCreate()
    {
        if (Instance == null) new GameObject("GameManager").AddComponent<GameManager>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        if (Instance == this) SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    // Reset everything whenever a scene (re)loads.
    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Loot = 0;
        key1 = key2 = false;
        GameOver = false;
        Won = false;
        message = "";
        Time.timeScale = 1f;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.R))
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void AddLoot(int value)
    {
        Loot += value;
        Message("Loot +$" + value);
    }

    public void GiveKey(int number)
    {
        if (number == 1) key1 = true; else key2 = true;
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
        if (GameOver) return;
        GameOver = true;
        Won = false;
        Time.timeScale = 0f;
    }

    public void ReachedExit()
    {
        if (GameOver) return;
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
        GUI.Label(new Rect(10, 8, 600, 28), "Loot: $" + Loot + "    Keycards: " + keys, smallStyle);
        GUI.Label(new Rect(10, 32, 700, 28), "WASD move  |  Shift run  |  Ctrl or C crouch  |  R restart", smallStyle);

        if (Time.unscaledTime < messageUntil)
            GUI.Label(new Rect(0, Screen.height - 70, Screen.width, 40), message, msgStyle);

        if (GameOver)
        {
            string text = Won ? "ESCAPED with $" + Loot + "!\nPress R to play again" : "CAUGHT!\nPress R to restart";
            GUI.Label(new Rect(0, 0, Screen.width, Screen.height), text, bigStyle);
        }
    }
}
