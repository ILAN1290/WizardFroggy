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
        // Защита от повторного запуска: иначе previousInputMap станет "Dialogue"
        // и контроль с инвентарём не вернутся никогда
        if (IsDialogueActive) return;

        if (index < 0 || index >= dialogues.Count) return;
        if (dialogues[index].dialogueLines.Count == 0) return;

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
    }

    private void Update()
    {
        // Кнопки диалога работают только пока диалог реально идёт
        if (!IsDialogueActive) return;

        if (InputManager.instance.SwitchDialogueLine)
            SwitchLine();
    }

    private void DisplayNextDialogueLine()
    {
        if (linesQueue.Count != 0)
        {
            currentLine = linesQueue.Dequeue();
            OnLineDisplayed?.Invoke(currentLine);

            if (currentLine.emotion != "Idle" && currentLine.person != null)
                currentLine.person.GetComponent<Animator>().SetBool(currentLine.emotion, true);

            if (currentLine.clip != null)
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
        dialogues[currentDialogueIndex].isPassed = true;
        dialogueAudioSource.Stop();
        StopAllCoroutines();
        dialogueArea.text = string.Empty;
        dialogueElements.SetActive(false);
        currentLine = null;

        string mapToRestore = string.IsNullOrEmpty(previousInputMap) ? "Player" : previousInputMap;
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
        dialogueAudioSource.Stop();
    }

    public void SwitchLine()
    {
        if (currentLine == null) return;

        if (dialogueArea.text == currentLine.text)
        {
            if (currentLine.emotion != "Idle" && currentLine.person != null)
                currentLine.person.GetComponent<Animator>().SetBool(currentLine.emotion, false);
            DisplayNextDialogueLine();
        }
        else
        {
            dialogueAudioSource.Stop();
            StopAllCoroutines();
            dialogueArea.text = currentLine.text;
        }
    }
}