using System.Collections.Generic;
using UnityEngine;

public class BinSensorController : MonoBehaviour,
                                   SmartBinVM.ISensorProvider
{
    // =========================================================
    // REFERENCIAS
    // =========================================================

    [Header("Referencia a la VM")]
    [Tooltip("Si esta vacio se busca automaticamente en la escena.")]
    public SmartBinVM vm;

    // =========================================================
    // ESTADO INTERNO
    // =========================================================

    GameObject currentWaste = null;
    string currentTag = "";
    bool binEmpty = true;
    bool binFull  = false;

    List<GameObject> wastesInZone = new List<GameObject>();

    [Header("Configuracion multi-bote")]
    [Tooltip("Indice del bote (-1 = modo single, 0-3 = multi-bote)")]
    public int binIndex = -1;

    // =========================================================
    // INSPECTOR: estado visible durante Play Mode
    // =========================================================
    [Header("Estado actual (solo lectura)")]
    [SerializeField] string dbg_CurrentWaste = "ninguno";
    [SerializeField] bool   dbg_BinEmpty     = true;
    [SerializeField] bool   dbg_BinFull      = false;
    [SerializeField] int    dbg_WastesInZone = 0;

    // =========================================================
    // UNITY LIFECYCLE
    // =========================================================

    void Awake()
    {
        if (vm == null)
            vm = GetComponentInParent<SmartBinVM>();

        if (vm == null)
            vm = FindObjectOfType<SmartBinVM>();

        if (binIndex < 0)
        {
            BinLidController lid = GetComponentInParent<BinLidController>();
            if (lid != null)
            {
                binIndex = lid.binIndex;
                Debug.Log($"[BinSensorController] {name} auto-detecto binIndex={binIndex} de {lid.name}");
            }
        }

        if (vm == null)
            Debug.LogError($"[BinSensorController] {name}: No se encontro SmartBinVM en la escena.");
        else if (binIndex >= 0)
        {
            vm.RegisterBinSensor(binIndex, this);
            Debug.Log($"[BinSensorController] {name} registrado en vm.binSensors[{binIndex}]");
        }
        else
        {
            vm.SetSensorProvider(this);
            Debug.Log($"[BinSensorController] {name} registrado como sensorProvider (binIndex={binIndex})");
        }
    }

    void Update()
    {
        wastesInZone.RemoveAll(w => w == null);

        dbg_CurrentWaste = currentWaste != null ? currentWaste.name : "ninguno";
        dbg_BinEmpty     = binEmpty;
        dbg_BinFull      = binFull;
        dbg_WastesInZone = wastesInZone.Count;

        if (wastesInZone.Count > 0)
        {
            string prevTag = currentTag;
            currentWaste = wastesInZone[0];
            currentTag   = currentWaste.tag;
            if (currentTag != prevTag)
                Debug.Log($"[BinSensorController] {name} Update: currentTag cambio '{prevTag}' -> '{currentTag}', wastesInZone.Count={wastesInZone.Count}");
        }
    }

    // =========================================================
    // DETECCION POR TRIGGER
    // =========================================================

    void OnTriggerEnter(Collider other)
    {
        if (IsWasteTag(other.tag))
        {
            if (!wastesInZone.Contains(other.gameObject))
            {
                wastesInZone.Add(other.gameObject);
                currentWaste = other.gameObject;
                currentTag   = other.tag;
                Debug.Log($"[BinSensorController] {name} (binIndex={binIndex}): Residuo entro: " +
                          $"{other.gameObject.name} (Tag: {other.tag}), wastesInZone.Count={wastesInZone.Count}");
            }
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (wastesInZone.Contains(other.gameObject))
        {
            wastesInZone.Remove(other.gameObject);
            if (wastesInZone.Count > 0)
            {
                currentWaste = wastesInZone[0];
                currentTag   = currentWaste.tag;
            }
            Debug.Log($"[BinSensorController] Residuo salio: {other.gameObject.name}");
        }
    }

    // =========================================================
    // IMPLEMENTACION DE ISensorProvider
    // =========================================================

    public bool ReadSensor(SmartBinVM.SensorCode sensor)
    {
        switch (sensor)
        {
            case SmartBinVM.SensorCode.Recyclable:    return currentTag == "Recyclable";
            case SmartBinVM.SensorCode.PET:           return currentTag == "PET";
            case SmartBinVM.SensorCode.Organic:       return currentTag == "Organic";
            case SmartBinVM.SensorCode.NonRecyclable: return currentTag == "NonRecyclable";
            case SmartBinVM.SensorCode.TEmpty:        return binEmpty;
            case SmartBinVM.SensorCode.TFull:         return binFull;
            default:
                Debug.LogWarning($"[BinSensorController] Sensor desconocido: {sensor}");
                return false;
        }
    }

    // =========================================================
    // API PUBLICA
    // =========================================================

    public void SetBinEmpty()
    {
        binEmpty = true;
        binFull  = false;
        Debug.Log("[BinSensorController] Bin: EMPTY.");
    }

    public void SetBinFull()
    {
        binFull  = true;
        binEmpty = false;
        Debug.Log("[BinSensorController] Bin: FULL.");
    }

    public void RemoveCurrentWaste()
    {
        if (currentWaste != null)
        {
            Debug.Log($"[BinSensorController] Residuo removido: {currentWaste.name}");
            wastesInZone.Remove(currentWaste);
        }
        currentWaste = null;
        currentTag   = "";
    }

    public void DestroyAllWaste()
    {
        foreach (GameObject waste in wastesInZone)
        {
            if (waste != null)
            {
                Debug.Log($"[BinSensorController] Destruyendo residuo acumulado: {waste.name}");
                Destroy(waste);
            }
        }
        wastesInZone.Clear();
        currentWaste = null;
        currentTag   = "";
    }

    // =========================================================
    // UTILIDADES
    // =========================================================

    bool IsWasteTag(string tag)
    {
        return tag == "Recyclable"    ||
               tag == "PET"           ||
               tag == "Organic"       ||
               tag == "NonRecyclable";
    }

    public GameObject GetCurrentWaste()   => currentWaste;
    public string     GetCurrentWasteType() => currentTag;
    public int        GetWasteCount()     => wastesInZone.Count;
}
