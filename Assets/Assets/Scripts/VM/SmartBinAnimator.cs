using System.Collections;
using UnityEngine;

public class SmartBinAnimator : MonoBehaviour
{
    // =========================================================
    // REFERENCIAS A LA ESCENA
    // =========================================================

    [Header("VM y sensor")]
    public SmartBinVM           vm;
    public BinSensorController  sensorCtrl;

    [Tooltip("Sensores de los 4 botes (multi-bote). Se usan si estan asignados.")]
    public BinSensorController[] binSensors;

    [Header("Compuertas (Transforms que se rotan / mueven)")]
    [Tooltip("Compuerta del compartimento RECYCLABLE")]
    public Transform gateRecyclable;
    [Tooltip("Compuerta del compartimento PET")]
    public Transform gatePET;
    [Tooltip("Compuerta del compartimento ORGANIC")]
    public Transform gateOrganic;
    [Tooltip("Compuerta del compartimento NONRECYCLABLE")]
    public Transform gateNonRecyclable;

    [Header("Efectos visuales (opcional)")]
    [Tooltip("Particulas que se activan al SCAN")]
    public ParticleSystem fxScan;
    [Tooltip("Luz que indica residuo detectado")]
    public Light          detectionLight;

    // =========================================================
    // CONFIGURACION DE ANIMACION
    // =========================================================

    [Header("Configuracion de compuertas")]
    [Tooltip("Angulo de rotacion en Y cuando la compuerta esta abierta")]
    public float gateOpenAngle  = 90f;
    [Tooltip("Angulo de rotacion en Y cuando la compuerta esta cerrada")]
    public float gateCloseAngle = 0f;
    [Tooltip("Segundos que tarda en abrir o cerrar una compuerta")]
    public float gateAnimTime   = 0.5f;

    [Header("Configuracion del SORT")]
    [Tooltip("Segundos que espera el residuo antes de destruirse (simula caida)")]
    public float sortDelay = 0.8f;

    // =========================================================
    // ESTADO INTERNO
    // =========================================================

    Transform activeGate = null;
    bool      gateIsOpen = false;

    // =========================================================
    // UNITY LIFECYCLE
    // =========================================================

    void Awake()
    {
        if (vm == null)
            vm = GetComponentInParent<SmartBinVM>();
        if (vm == null)
            vm = FindObjectOfType<SmartBinVM>();

        if (sensorCtrl == null)
            sensorCtrl = FindObjectOfType<BinSensorController>();

        if (vm != null)
        {
            vm.OnScan.AddListener(OnScan);
            vm.OnSort.AddListener(OnSort);
            vm.OnOpen.AddListener(OnOpen);
            vm.OnClose.AddListener(OnClose);
        }
        else
        {
            Debug.LogError("[SmartBinAnimator] No se encontro SmartBinVM.");
        }
    }

    // =========================================================
    // MANEJADORES DE EVENTOS DE LA VM
    // =========================================================

    public void OnScan()
    {
        int ab = vm != null ? vm.activeBin : -1;
        Debug.Log($"[SmartBinAnimator] SCAN ejecutado. activeBin antes={ab}");

        if (fxScan != null)
            fxScan.Play();

        BinSensorController activeSensor = GetActiveSensor();
        string wasteType = activeSensor != null
            ? activeSensor.GetCurrentWasteType()
            : "";

        string sn = activeSensor != null ? activeSensor.name : "NULL";
        int ad = vm != null ? vm.activeBin : -1;
        Debug.Log($"[SmartBinAnimator] activeSensor={sn}, wasteType='{wasteType}', activeBin despues={ad}");

        activeGate = GetGateForType(wasteType);

        if (detectionLight != null)
            detectionLight.enabled = (activeGate != null);

        Debug.Log($"[SmartBinAnimator] Tipo detectado: '{wasteType}' " +
                  $"-> compuerta: {(activeGate != null ? activeGate.name : "ninguna")}");
    }

    public void OnOpen()
    {
        if (activeGate == null)
        {
            Debug.LogWarning("[SmartBinAnimator] OPEN: sin compuerta activa.");
            return;
        }

        Debug.Log($"[SmartBinAnimator] OPEN -> {activeGate.name}");
        StartCoroutine(AnimateGate(activeGate, gateOpenAngle));
        gateIsOpen = true;
    }

    public void OnSort()
    {
        Debug.Log("[SmartBinAnimator] SORT ejecutado.");
        StartCoroutine(SortSequence());
    }

    public void OnClose()
    {
        if (activeGate == null)
        {
            Debug.LogWarning("[SmartBinAnimator] CLOSE: sin compuerta activa.");
            return;
        }

        Debug.Log($"[SmartBinAnimator] CLOSE -> {activeGate.name}");
        StartCoroutine(AnimateGate(activeGate, gateCloseAngle));
        gateIsOpen = false;

        if (detectionLight != null)
            detectionLight.enabled = false;

        activeGate = null;
    }

    // =========================================================
    // SECUENCIA DE SORT
    // =========================================================

    IEnumerator SortSequence()
    {
        yield return new WaitForSeconds(gateAnimTime);

        BinSensorController activeSensor = GetActiveSensor();
        if (activeSensor == null)
        {
            Debug.LogWarning("[SmartBinAnimator] SORT: sensor no encontrado.");
            yield break;
        }

        GameObject waste = activeSensor.GetCurrentWaste();

        if (waste != null)
        {
            Rigidbody rb = waste.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.useGravity  = true;
            }
        }

        yield return new WaitForSeconds(sortDelay);

        if (waste != null)
        {
            Debug.Log($"[SmartBinAnimator] Destruyendo residuo: {waste.name}");
            Destroy(waste);
        }

        activeSensor.DestroyAllWaste();
    }

    // =========================================================
    // ANIMACION DE COMPUERTA
    // =========================================================

    IEnumerator AnimateGate(Transform gate, float targetAngle)
    {
        if (gate == null) yield break;

        float startAngle = gate.localEulerAngles.y;
        float elapsed    = 0f;

        while (elapsed < gateAnimTime)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / gateAnimTime);

            float smooth = Mathf.SmoothStep(0f, 1f, t);
            float angle  = Mathf.LerpAngle(startAngle, targetAngle, smooth);

            gate.localEulerAngles = new Vector3(
                gate.localEulerAngles.x,
                angle,
                gate.localEulerAngles.z
            );

            yield return null;
        }

        gate.localEulerAngles = new Vector3(
            gate.localEulerAngles.x,
            targetAngle,
            gate.localEulerAngles.z
        );
    }

    // =========================================================
    // UTILIDADES
    // =========================================================

    Transform GetGateForType(string wasteType)
    {
        switch (wasteType)
        {
            case "Recyclable":    return gateRecyclable;
            case "PET":           return gatePET;
            case "Organic":       return gateOrganic;
            case "NonRecyclable": return gateNonRecyclable;
            default:              return null;
        }
    }

    BinSensorController GetActiveSensor()
    {
        int binLen = vm?.binSensors?.Length ?? -1;
        int ab = vm != null ? vm.activeBin : -1;
        Debug.Log($"[GetActiveSensor] DIAG: vm={vm != null}, binSensors.Length={binLen}, activeBin={ab}");
        if (vm != null && vm.binSensors != null)
        {
            for (int i = 0; i < vm.binSensors.Length; i++)
            {
                var s = vm.binSensors[i];
                string sn = s != null ? s.name : "NULL";
                bool hasWaste = s != null && !string.IsNullOrEmpty(s.GetCurrentWasteType());
                string wt = s != null ? s.GetCurrentWasteType() : "N/A";
                string wn = (s != null && s.GetCurrentWaste() != null) ? s.GetCurrentWaste().name : "NULL";
                Debug.Log($"[GetActiveSensor]   binSensors[{i}]={sn}, currentWaste={wn}, currentTag='{wt}'");
                if (hasWaste)
                {
                    vm.activeBin = i;
                    Debug.Log($"[GetActiveSensor]   -> SELECCIONADO binSensors[{i}]");
                    return s;
                }
            }
            Debug.Log("[GetActiveSensor]   -> NINGUN sensor tiene currentTag != vacio");
        }
        if (vm != null && vm.sensorProvider is BinSensorController sensor)
        {
            Debug.Log($"[GetActiveSensor]   -> fallback: sensorProvider={sensor.name}");
            return sensor;
        }
        string scn = sensorCtrl != null ? sensorCtrl.name : "NULL";
        Debug.Log($"[GetActiveSensor]   -> fallback: sensorCtrl={scn}");
        return sensorCtrl;
    }

    public bool ReadActiveSensor(SmartBinVM.SensorCode sensorCode)
    {
        BinSensorController s = GetActiveSensor();
        return s != null && s.ReadSensor(sensorCode);
    }
}
