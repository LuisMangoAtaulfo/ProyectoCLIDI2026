using UnityEngine;
using TMPro;
using System.Collections.Generic;

public class VRConsole : MonoBehaviour
{
    [Header("Configuración de UI")]
    [Tooltip("Arrastra aquí tu TextMeshPro donde se verán los logs")]
    public TextMeshProUGUI textoConsola;
    
    [Tooltip("Cuántas líneas quieres mantener en pantalla antes de borrar las más viejas")]
    public int maxLineas = 15;

    private Queue<string> logQueue = new Queue<string>();

    void OnEnable()
    {
        // Nos suscribimos a los eventos de la consola de Unity
        Application.logMessageReceived += HandleLog;
    }

    void OnDisable()
    {
        // Nos desuscribimos para evitar errores de memoria
        Application.logMessageReceived -= HandleLog;
    }

    void HandleLog(string logString, string stackTrace, LogType type)
    {
        // Podemos cambiar el color dependiendo de si es error, warning o log normal
        string colorHex = "#FFFFFF"; // Blanco por defecto para Debug.Log
        
        if (type == LogType.Error || type == LogType.Exception)
            colorHex = "#FF4444"; // Rojo
        else if (type == LogType.Warning)
            colorHex = "#FFCC00"; // Amarillo

        // Formatear el mensaje
        string nuevoMensaje = $"<color={colorHex}>{logString}</color>";
        
        // Agregar a la cola
        logQueue.Enqueue(nuevoMensaje);

        // Si nos pasamos del límite de líneas, borramos la más vieja
        if (logQueue.Count > maxLineas)
        {
            logQueue.Dequeue();
        }

        // Actualizar el Canvas
        if (textoConsola != null)
        {
            textoConsola.text = string.Join("\n", logQueue);
        }
    }
}