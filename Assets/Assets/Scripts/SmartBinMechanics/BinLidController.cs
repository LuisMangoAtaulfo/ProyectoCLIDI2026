using System.Collections;
using UnityEngine;
using TMPro;

public class BinLidController : MonoBehaviour
{
    [Header("Referencias")]
    public SmartBinVM vm;
    public Transform lidPivot;

    [Header("Configuracion de Animacion")]
    public float openAngle = 90f;
    public float animTime = 0.5f;

    [Tooltip("Segundos que espera la tapa abierta para que la basura caiga")]
    public float tiempoDeCaida = 1.5f;

    [Tooltip("Indice del bote (0=Recyclable, 1=PET, 2=Organic, 3=NonRecyclable)")]
    public int binIndex = 0;

    [Header("Mensaje de error")]
    [Tooltip("TextMeshPro en el Canvas donde se muestra el mensaje")]
    public TextMeshProUGUI wrongBinMessage;

    [Tooltip("Segundos que dura el mensaje en pantalla")]
    public float messageDuration = 2.5f;

    private bool isProcessing = false;

    private static readonly string[] expectedTags =
    {
        "Recyclable",
        "PET",
        "Organic",
        "NonRecyclable"
    };

    void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.layer != LayerMask.NameToLayer("Basura") || isProcessing)
            return;

        string expected = expectedTags[binIndex];
        Debug.Log($"[BinLidController] {name} (binIndex={binIndex}, espera='{expected}'): recibio {other.name} (Tag: {other.tag})");

        if (other.tag == expected)
        {
            Debug.Log($"[BinLidController] {name} -> Tag coincide, iniciando TrapdoorSequence");
            StartCoroutine(TrapdoorSequence());
        }
        else
        {
            Debug.Log($"[BinLidController] {name} -> Tag NO coincide, mostrando mensaje");
            StartCoroutine(ShowWrongMessage(other.tag, expected));
        }
    }

    IEnumerator ShowWrongMessage(string wasteTag, string expectedTag)
    {
        if (wrongBinMessage != null)
        {
            wrongBinMessage.text = $"Ese residuo no va en este lugar. Es {wasteTag}, debe ir en {expectedTag}.";
            wrongBinMessage.enabled = true;
            yield return new WaitForSeconds(messageDuration);
            wrongBinMessage.enabled = false;
        }
    }

    IEnumerator TrapdoorSequence()
    {
        isProcessing = true;

        yield return StartCoroutine(AnimateLid(openAngle));
        yield return new WaitForSeconds(tiempoDeCaida);
        yield return StartCoroutine(AnimateLid(0f));

        yield return new WaitForSeconds(0.2f);

        if (vm != null)
        {
            Debug.Log($"[BinLidController] {name} -> vm.activeBin={binIndex}, llamando RestartVM()");
            vm.activeBin = binIndex;
            vm.RestartVM();
        }

        isProcessing = false;
    }

    IEnumerator AnimateLid(float targetAngle)
    {
        float startAngle = lidPivot.localEulerAngles.x;
        float elapsed = 0f;

        while (elapsed < animTime)
        {
            elapsed += Time.deltaTime;
            float smooth = Mathf.SmoothStep(0f, 1f, elapsed / animTime);
            float angle = Mathf.LerpAngle(startAngle, targetAngle, smooth);

            lidPivot.localEulerAngles = new Vector3(angle, 0, 0);

            yield return null;
        }

        lidPivot.localEulerAngles = new Vector3(targetAngle, 0, 0);
    }
}
