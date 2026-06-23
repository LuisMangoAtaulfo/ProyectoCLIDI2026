using System;
using UnityEngine;
using Random = UnityEngine.Random;

public class SpawnButtonHandler : MonoBehaviour
{
    [Header("Prefab de basura VR (Individual)")]
    [Tooltip("Arrastra aqui el prefab Trash_VR_Ready.prefab")]
    public GameObject trashPrefab;

    [Header("Prefabs Aleatorios (Múltiples)")]
    [Tooltip("Arrastra aqui todos los prefabs (Metal, Papel, etc.)")]
    public GameObject[] arregloDeBasuras; // <-- Aquí guardaremos la lista para el Inspector

    [Header("Posicion de spawn")]
    [Tooltip("Punto desde donde se spawnea la basura")]
    public Transform spawnPoint;

    [Header("Configuracion")]
    [Tooltip("Velocidad inicial con que se lanza la basura")]
    public float throwForce = 5f;

    public void SpawnTrash()
    {
        if (trashPrefab == null)
        {
            Debug.LogError("[SpawnButtonHandler] No hay prefab de basura asignado.");
            return;
        }

        Vector3 pos = spawnPoint != null ? spawnPoint.position : transform.position;
        Quaternion rot = spawnPoint != null ? spawnPoint.rotation : Random.rotation;

        GameObject instance = Instantiate(trashPrefab, pos, rot);

        Rigidbody rb = instance.GetComponent<Rigidbody>();
        if (rb != null)
        {
            Vector3 dir = spawnPoint != null ? spawnPoint.forward : transform.forward;
            rb.AddForce(dir * throwForce, ForceMode.Impulse);
        }

        Debug.Log($"[SpawnButtonHandler] Basura spawneada: {instance.name}");
    }

    // Le quitamos el parámetro a la función para que el botón OnClick() pueda verla
    public void SpawnRandomTrash() 
    {
        // Usamos la variable arregloDeBasuras que declaramos arriba
        if (arregloDeBasuras == null || arregloDeBasuras.Length == 0)
        {
            Debug.LogError("[SpawnButtonHandler] No hay prefabs de basura en el arregloDeBasuras.");
            return;
        }

        int randomIndex = Random.Range(0, arregloDeBasuras.Length);
        GameObject chosen = arregloDeBasuras[randomIndex];

        Vector3 pos = spawnPoint != null ? spawnPoint.position : transform.position;
        Quaternion rot = spawnPoint != null ? spawnPoint.rotation : Random.rotation;

        GameObject instance = Instantiate(chosen, pos, rot);

        Rigidbody rb = instance.GetComponent<Rigidbody>();
        if (rb != null)
        {
            Vector3 dir = spawnPoint != null ? spawnPoint.forward : transform.forward;
            rb.AddForce(dir * throwForce, ForceMode.Impulse);
        }

        Debug.Log($"[SpawnButtonHandler] Basura spawneada: {instance.name} (aleatoria)");
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.G))
        {
            SpawnRandomTrash();
        }
    }
}