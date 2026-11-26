using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class DialogueManager : MonoBehaviour
{
    public static DialogueManager instance;

    [SerializeField] private GameObject dialogueElements;
    [SerializeField] private TextMeshProUGUI nameArea;
    [SerializeField] private TextMeshProUGUI dialogueArea;
    [SerializeField] private float textSpeed;
    [SerializeField] private Image iconImage;
    [SerializeField] private AudioSource dialogueAudioSource;

    public List<Dialogue> dialogues = new List<Dialogue>();

    // Событие вызывается каждый раз, когда начинается новая реплика.
    public event System.Action<DialogueLine> OnLineDisplayed;

    private Queue<DialogueLine> linesQueue = new Queue<DialogueLine>();
    private DialogueLine currentLine;
    private int currentDialogueIndex;
    private string previousInputMap;

    private bool IsDialogueActive => dialogueElements != null && dialogueElements.activeSelf;

    private void Awake()
    {
        if (instance == null) instance = this;
    }

    public void StartDialogue(int index)
    {
        TryStartDialogue(index);
    }

    public bool TryStartDialogue(int index)
    {
        // Не оставляем катсцену в бесконечном ожидании, если ссылка на UI
        // или PlayerInput не назначена в сцене.
        if (IsDialogueActive)
        {
            Debug.LogWarning("DialogueManager: диалог уже открыт, повторный запуск пропущен.", this);
            return false;
        }

        if (dialogues == null || index < 0 || index >= dialogues.Count ||
            dialogues[index] == null || dialogues[index].dialogueLines == null ||
            dialogues[index].dialogueLines.Count == 0)
        {
            Debug.LogError("DialogueManager: для запуска не найден диалог или его реплики.", this);
            return false;
        }

        if (dialogueElements == null || nameArea == null ||
            dialogueArea == null || iconImage == null)
        {
            Debug.LogError("DialogueManager: не назначены элементы интерфейса диалога.", this);
            return false;
        }

        if (InputManager.PlayerInput == null)
        {
            Debug.LogError("DialogueManager: PlayerInput не найден, диалог не запущен.", this);
            return false;
        }

        previousInputMap = InputManager.PlayerInput.currentActionMap != null
            ? InputManager.PlayerInput.currentActionMap.name
            : "Player";

        InputManager.PlayerInput.SwitchCurrentActionMap("Dialogue");

        currentDialogueIndex = index;
        dialogueElements.SetActive(true);

        linesQueue.Clear();
        foreach (DialogueLine diLine in dialogues[currentDialogueIndex].dialogueLines)
            linesQueue.Enqueue(diLine);

        DisplayNextDialogueLine();
        return true;
    }

    private void Update()
    {
        // Кнопки диалога работают только пока диалог реально идёт
        if (!IsDialogueActive) return;

        if (InputManager.instance != null && InputManager.instance.SwitchDialogueLine)
            SwitchLine();
    }

    private void DisplayNextDialogueLine()
    {
        if (linesQueue.Count != 0)
        {
            currentLine = linesQueue.Dequeue();
            OnLineDisplayed?.Invoke(currentLine);

            if (currentLine.emotion != "Idle" && currentLine.person != null)
            {
                Animator animator = currentLine.person.GetComponent<Animator>();
                if (animator != null)
                    animator.SetBool(currentLine.emotion, true);
            }

            if (currentLine.clip != null && dialogueAudioSource != null)
            {
                dialogueAudioSource.clip = currentLine.clip;
                dialogueAudioSource.Play();
            }

            nameArea.text = currentLine.personName;
            iconImage.sprite = currentLine.icon;
            StartCoroutine(TypeSentence(currentLine));
        }
        else
        {
            EndDialogue();
        }
    }

    private void EndDialogue()
    {
        if (dialogues != null && currentDialogueIndex >= 0 &&
            currentDialogueIndex < dialogues.Count && dialogues[currentDialogueIndex] != null)
            dialogues[currentDialogueIndex].isPassed = true;

        if (dialogueAudioSource != null)
            dialogueAudioSource.Stop();

        StopAllCoroutines();

        if (dialogueArea != null)
            dialogueArea.text = string.Empty;
        if (dialogueElements != null)
            dialogueElements.SetActive(false);

        currentLine = null;

        string mapToRestore = string.IsNullOrEmpty(previousInputMap) ? "Player" : previousInputMap;
        if (InputManager.PlayerInput != null)
            InputManager.PlayerInput.SwitchCurrentActionMap(mapToRestore);
    }

    private IEnumerator TypeSentence(DialogueLine diLine)
    {
        dialogueArea.text = string.Empty;
        foreach (char let in diLine.text.ToCharArray())
        {
            dialogueArea.text += let;
            yield return new WaitForSeconds(textSpeed);
        }
        if (dialogueAudioSource != null)
            dialogueAudioSource.Stop();
    }

    public void SwitchLine()
    {
        if (currentLine == null) return;

        if (dialogueArea.text == currentLine.text)
        {
            if (currentLine.emotion != "Idle" && currentLine.person != null)
            {
                Animator animator = currentLine.person.GetComponent<Animator>();
                if (animator != null)
                    animator.SetBool(currentLine.emotion, false);
            }
            DisplayNextDialogueLine();
        }
        else
        {
            if (dialogueAudioSource != null)
                dialogueAudioSource.Stop();
            StopAllCoroutines();
            dialogueArea.text = currentLine.text;
        }
    }
}