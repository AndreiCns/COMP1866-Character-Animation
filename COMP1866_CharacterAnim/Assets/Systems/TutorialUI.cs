using UnityEngine;

// Toggles a tutorial UI root GameObject with a keypress and initial visibility.
public class TutorialUI : MonoBehaviour
{
    [Header("Assign the Panel (or the whole Canvas) you want to toggle")]
    [SerializeField] private GameObject controlsRoot;

    [Header("Start Visible")]
    [SerializeField] private bool showOnStart = true;

    [Header("Toggle Key")]
    [SerializeField] private KeyCode toggleKey = KeyCode.Tab;

    void Awake()
    {
        if (controlsRoot == null)
            controlsRoot = gameObject; // fallback to this object
    }

    void Start()
    {
        controlsRoot.SetActive(showOnStart);
    }

    void Update()
    {
        if (Input.GetKeyDown(toggleKey))
            controlsRoot.SetActive(!controlsRoot.activeSelf);
    }
}
