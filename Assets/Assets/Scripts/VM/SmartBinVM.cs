using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

public class SmartBinVM : MonoBehaviour
{
    // =========================================================
    // INTERFAZ DE SENSORES
    // =========================================================

    public interface ISensorProvider
    {
        bool ReadSensor(SensorCode sensor);
    }

    public enum SensorCode
    {
        Recyclable    = 1,
        PET           = 2,
        Organic       = 3,
        NonRecyclable = 4,
        TEmpty        = 5,
        TFull         = 6
    }

    // =========================================================
    // OPCODES  (deben coincidir con parser_smartbin_rdcp.c)
    // =========================================================
    const int A_MOV     = 1;
    const int A_ADV     = 2;
    const int A_DEC     = 3;
    const int A_JNZ     = 4;
    const int A_JEZ     = 5;
    const int A_PUSH    = 6;
    const int A_CMP     = 7;
    const int A_END     = 8;
    const int A_TRIGHT  = 9;
    const int A_TLEFT   = 10;
    const int A_PICKUP  = 11;
    const int A_PUTDOWN = 12;
    const int A_SCAN    = 13;
    const int A_SORT    = 14;
    const int A_OPEN    = 15;
    const int A_CLOSE   = 16;
    const int A_SENSOR  = 17;

    // =========================================================
    // REGISTROS
    // =========================================================
    const int R_PC  = 1;
    const int R_IR1 = 2;
    const int R_IR2 = 3;
    const int R_SP  = 4;
    const int R_CRC = 5;
    const int R_AC  = 6;
    const int R_ACC = 7;

    // =========================================================
    // MODOS DE DIRECCIONAMIENTO
    // =========================================================
    const int AM_IMMEDT = 1;
    const int AM_DIRREG = 2;

    // =========================================================
    // MEMORIA Y REGISTROS DEL PROCESADOR
    // =========================================================
    const int MEMORY_MAX_SIZE = 512;
    const int STACK_MAX_SIZE  = 64;

    int[] mem   = new int[MEMORY_MAX_SIZE];
    int[] stack = new int[STACK_MAX_SIZE];

    // Registros
    int PC  = 0;
    int IR1 = 0;
    int IR2 = 0;
    int SP  = 0;
    int CRC = 0;
    int AC  = 0;
    int ACC = 0;

    // Flags de condicion
    bool FLAG_EQ  = false;   // ACC == 0
    bool FLAG_GT  = false;   // ACC >  0
    bool FLAG_LT  = false;   // ACC <  0

    // Estado de la VM
    bool vmRunning  = false;
    bool vmFinished = false;
    bool vmError    = false;
    bool programLoaded = false;

    // =========================================================
    // INSPECTOR: configuracion desde Unity
    // =========================================================
    [Header("Archivo binario")]
    [Tooltip("Nombre del archivo dentro de StreamingAssets/")]
    public string binaryFileName = "object.bin";

    [Header("Configuracion multi-bote")]
    [Tooltip("Indice del bote activo (0=Recyclable, 1=PET, 2=Organic, 3=NonRecyclable)")]
    public int activeBin = 0;
    [Tooltip("Controladores de sensor, uno por bote. Se auto-pueblan via RegisterBinSensor().")]
    public BinSensorController[] binSensors = new BinSensorController[4];

    [Header("Velocidad de ejecucion")]
    [Tooltip("Segundos entre cada instruccion (0 = tan rapido como posible)")]
    public float stepDelay = 0.1f;

    [Header("Modo de ejecucion")]
    [Tooltip("Si es true, la VM ejecuta instruccion por instruccion con StepOnce()")]
    public bool manualStep = false;

    // =========================================================
    // EVENTOS UNITY  (conectar en Inspector o por codigo)
    // =========================================================
    [Header("Eventos de accion")]
    public UnityEngine.Events.UnityEvent OnScan;
    public UnityEngine.Events.UnityEvent OnSort;
    public UnityEngine.Events.UnityEvent OnOpen;
    public UnityEngine.Events.UnityEvent OnClose;

    [Header("Eventos de sensor")]
    public UnityEngine.Events.UnityEvent<string> OnSensorRead;  // nombre del sensor

    [Header("Eventos de estado")]
    public UnityEngine.Events.UnityEvent OnProgramEnd;
    public UnityEngine.Events.UnityEvent<string> OnVMError;

    // =========================================================
    // PROVEEDOR DE SENSORES
    // =========================================================
    public ISensorProvider sensorProvider;

    SmartBinAnimator animator;

    public void SetSensorProvider(ISensorProvider provider)
    {
        sensorProvider = provider;
    }

    public void RegisterBinSensor(int index, BinSensorController sensor)
    {
        if (binSensors == null || binSensors.Length <= index)
            binSensors = new BinSensorController[4];
        if (index >= 0 && index < binSensors.Length)
            binSensors[index] = sensor;
    }

    // =========================================================
    // ESTADO PUBLICO (para UI / debug en Inspector)
    // =========================================================
    [Header("Estado de la VM (solo lectura)")]
    [SerializeField] int dbg_PC  = 0;
    [SerializeField] int dbg_ACC = 0;
    [SerializeField] int dbg_SP  = 0;
    [SerializeField] int dbg_CRC = 0;
    [SerializeField] int dbg_AC  = 0;
    [SerializeField] bool dbg_FLAG_EQ = false;
    [SerializeField] bool dbg_FLAG_GT = false;
    [SerializeField] bool dbg_FLAG_LT = false;
    [SerializeField] string dbg_LastInstruction = "";
    [SerializeField] string dbg_LastSensor = "";

    // =========================================================
    // UNITY LIFECYCLE
    // =========================================================

    void Awake()
    {
        if (binSensors == null || binSensors.Length != 4)
            binSensors = new BinSensorController[4];
    }

    void Start()
    {
        animator = GetComponent<SmartBinAnimator>();
        if (animator == null)
            animator = FindObjectOfType<SmartBinAnimator>();
        LoadProgram();
    }

    void Update()
    {
        // Sincronizar variables de debug con el Inspector
        dbg_PC      = PC;
        dbg_ACC     = ACC;
        dbg_SP      = SP;
        dbg_CRC     = CRC;
        dbg_AC      = AC;
        dbg_FLAG_EQ = FLAG_EQ;
        dbg_FLAG_GT = FLAG_GT;
        dbg_FLAG_LT = FLAG_LT;
    }

    // =========================================================
    // CARGA DEL PROGRAMA (con fallback para Android)
    // =========================================================

    void LoadProgram()
    {
        string path = Path.Combine(Application.streamingAssetsPath, binaryFileName);

        if (File.Exists(path))
        {
            byte[] bytes = File.ReadAllBytes(path);
            LoadBytes(bytes);
            return;
        }

        StartCoroutine(LoadProgramWebRequest(path));
    }

    IEnumerator LoadProgramWebRequest(string path)
    {
        using (UnityWebRequest www = UnityWebRequest.Get(path))
        {
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                byte[] bytes = www.downloadHandler.data;
                LoadBytes(bytes);
            }
            else
            {
                RaiseError($"Error cargando object.bin: {www.error}");
            }
        }
    }

    void LoadBytes(byte[] bytes)
    {
        int wordCount = bytes.Length / 2;

        if (wordCount > MEMORY_MAX_SIZE)
        {
            RaiseError($"Programa demasiado grande ({wordCount} words, max {MEMORY_MAX_SIZE})");
            return;
        }

        for (int i = 0; i < wordCount; i++)
        {
            // Little-endian (igual que la VM original en Windows/Linux)
            mem[i] = BitConverter.ToInt16(bytes, i * 2);
        }

        programLoaded = true;
        Debug.Log($"[SmartBinVM] Programa cargado: {wordCount} instrucciones.");
    }

    // =========================================================
    // CONTROL DE EJECUCION
    // =========================================================

    public void StartVM()
    {
        if (vmError) return;
        if (!programLoaded)
        {
            Debug.LogWarning("[SmartBinVM] Programa no cargado aun. Esperando...");
            return;
        }

        PC         = 0;
        SP         = 0;
        ACC        = 0;
        CRC        = 0;
        AC         = 0;
        FLAG_EQ    = false;
        FLAG_GT    = false;
        FLAG_LT    = false;
        vmRunning  = true;
        vmFinished = false;

        if (!manualStep)
            StartCoroutine(ExecutionLoop());
    }

    public void StopVM()
    {
        vmRunning = false;
        StopAllCoroutines();
    }

    public void StepOnce()
    {
        if (!vmRunning || vmFinished || vmError) return;
        ExecuteOneInstruction();
    }

    // =========================================================
    // CICLO PRINCIPAL DE EJECUCION
    // =========================================================

    IEnumerator ExecutionLoop()
    {
        while (vmRunning && !vmFinished && !vmError)
        {
            ExecuteOneInstruction();

            if (stepDelay > 0f)
                yield return new WaitForSeconds(stepDelay);
            else
                yield return null;
        }
    }

    void ExecuteOneInstruction()
    {
        if (PC < 0 || PC >= MEMORY_MAX_SIZE)
        {
            RaiseError($"PC fuera de rango: {PC}");
            return;
        }

        IR1 = mem[PC++];
        dbg_LastInstruction = OpName(IR1);

        switch (IR1)
        {
            case A_MOV:     I_Mov();     break;
            case A_ADV:     I_Adv();     break;
            case A_DEC:     I_Dec();     break;
            case A_JNZ:     I_Jnz();     break;
            case A_JEZ:     I_Jez();     break;
            case A_PUSH:    I_Push();    break;
            case A_CMP:     I_Cmp();     break;
            case A_END:     I_End();     break;
            case A_TRIGHT:  I_TRight();  break;
            case A_TLEFT:   I_TLeft();   break;
            case A_PICKUP:  I_PickUp();  break;
            case A_PUTDOWN: I_PutDown(); break;
            case A_SCAN:    I_Scan();    break;
            case A_SORT:    I_Sort();    break;
            case A_OPEN:    I_Open();    break;
            case A_CLOSE:   I_Close();   break;
            case A_SENSOR:  I_Sensor();  break;
            default:
                RaiseError($"Opcode desconocido: {IR1} en PC={PC - 1}");
                break;
        }
    }

    // =========================================================
    // INSTRUCCIONES DE LA VM
    // =========================================================

    int GetNextValue()
    {
        int addrMode = mem[PC++];
        switch (addrMode)
        {
            case AM_IMMEDT:
                return mem[PC++];

            case AM_DIRREG:
                int reg = mem[PC++];
                return GetRegister(reg);

            default:
                RaiseError($"Modo de direccionamiento desconocido: {addrMode}");
                return 0;
        }
    }

    void PutValue(int value)
    {
        int addrMode = mem[PC++];
        switch (addrMode)
        {
            case AM_IMMEDT:
                break;

            case AM_DIRREG:
                int reg = mem[PC++];
                SetRegister(reg, value);
                break;

            default:
                RaiseError($"Modo de direccionamiento desconocido en PutValue: {addrMode}");
                break;
        }
    }

    int GetRegister(int reg)
    {
        switch (reg)
        {
            case R_PC:  return PC;
            case R_IR1: return IR1;
            case R_IR2: return IR2;
            case R_SP:  return SP;
            case R_CRC: return CRC;
            case R_AC:  return AC;
            case R_ACC: return ACC;
            default:
                RaiseError($"Registro desconocido: {reg}");
                return 0;
        }
    }

    void SetRegister(int reg, int value)
    {
        switch (reg)
        {
            case R_PC:  PC  = value; break;
            case R_IR1: IR1 = value; break;
            case R_IR2: IR2 = value; break;
            case R_SP:  SP  = value; break;
            case R_CRC: CRC = value; break;
            case R_AC:  AC  = value; break;
            case R_ACC: ACC = value; break;
            default:
                RaiseError($"Registro destino desconocido: {reg}");
                break;
        }
    }

    void ReviewFlags()
    {
        FLAG_EQ = (ACC == 0);
        FLAG_GT = (ACC >  0);
        FLAG_LT = (ACC <  0);
    }

    void I_Mov()
    {
        int src = GetNextValue();
        PutValue(src);
    }

    void I_Adv()
    {
        Debug.LogWarning("[SmartBinVM] ADV ejecutado (no aplica al SmartBin).");
    }

    void I_Dec()
    {
        ACC--;
        ReviewFlags();
    }

    void I_Jnz()
    {
        int addr = mem[PC++];
        if (FLAG_GT || FLAG_LT)
            PC = addr;
    }

    void I_Jez()
    {
        int addr = mem[PC++];
        if (FLAG_EQ)
            PC = addr;
    }

    void I_Push()
    {
        if (SP >= STACK_MAX_SIZE)
        {
            RaiseError("Stack overflow");
            return;
        }
        stack[SP++] = GetNextValue();
    }

    void I_Cmp()
    {
        if (SP < 2)
        {
            RaiseError($"Stack underflow en CMP (SP={SP})");
            return;
        }
        int minuend    = stack[--SP];   // B (tope)
        int subtrahend = stack[--SP];   // A (debajo)
        ACC = subtrahend - minuend;
        ReviewFlags();
    }

    void I_End()
    {
        vmFinished = true;
        vmRunning  = false;
        Debug.Log("[SmartBinVM] Programa finalizado.");
        OnProgramEnd?.Invoke();
    }

    void I_TRight()  { Debug.LogWarning("[SmartBinVM] TRIGHT (no aplica)."); }
    void I_TLeft()   { Debug.LogWarning("[SmartBinVM] TLEFT (no aplica)."); }
    void I_PickUp()  { Debug.LogWarning("[SmartBinVM] PICKUP (no aplica)."); }
    void I_PutDown() { Debug.LogWarning("[SmartBinVM] PUTDOWN (no aplica)."); }

    void I_Scan()
    {
        Debug.Log("[SmartBinVM] SCAN");
        OnScan?.Invoke();
    }

    void I_Sort()
    {
        Debug.Log("[SmartBinVM] SORT");
        OnSort?.Invoke();
    }

    void I_Open()
    {
        Debug.Log("[SmartBinVM] OPEN");
        OnOpen?.Invoke();
    }

    void I_Close()
    {
        Debug.Log("[SmartBinVM] CLOSE");
        OnClose?.Invoke();
    }

    void I_Sensor()
    {
        int sensorCode = mem[PC++];
        SensorCode sensor = (SensorCode)sensorCode;
        dbg_LastSensor = sensor.ToString();

        bool result = false;

        if (animator != null)
        {
            result = animator.ReadActiveSensor(sensor);
        }
        else if (activeBin >= 0 && activeBin < binSensors.Length && binSensors[activeBin] != null)
        {
            result = binSensors[activeBin].ReadSensor(sensor);
        }
        else if (sensorProvider != null)
        {
            result = sensorProvider.ReadSensor(sensor);
        }
        else
        {
            result = SimulateSensor(sensor);
        }

        Debug.Log($"[SmartBinVM] SENSOR {sensor} -> {(result ? 1 : 0)}");
        OnSensorRead?.Invoke(sensor.ToString());

        if (SP >= STACK_MAX_SIZE)
        {
            RaiseError("Stack overflow en SENSOR");
            return;
        }
        stack[SP++] = result ? 1 : 0;
    }

    // =========================================================
    // SIMULACION DE SENSORES (solo para pruebas en Editor)
    // =========================================================

    [Header("Simulacion de sensores (Editor)")]
    public bool sim_Recyclable    = false;
    public bool sim_PET           = false;
    public bool sim_Organic       = false;
    public bool sim_NonRecyclable = false;
    public bool sim_TEmpty        = true;
    public bool sim_TFull         = false;

    bool SimulateSensor(SensorCode sensor)
    {
        switch (sensor)
        {
            case SensorCode.Recyclable:    return sim_Recyclable;
            case SensorCode.PET:           return sim_PET;
            case SensorCode.Organic:       return sim_Organic;
            case SensorCode.NonRecyclable: return sim_NonRecyclable;
            case SensorCode.TEmpty:        return sim_TEmpty;
            case SensorCode.TFull:         return sim_TFull;
            default:                       return false;
        }
    }

    // =========================================================
    // MANEJO DE ERRORES
    // =========================================================

    void RaiseError(string msg)
    {
        vmError   = true;
        vmRunning = false;
        Debug.LogError($"[SmartBinVM] ERROR: {msg}");
        OnVMError?.Invoke(msg);
    }

    // =========================================================
    // UTILIDADES
    // =========================================================

    string OpName(int opcode)
    {
        switch (opcode)
        {
            case A_MOV:     return "MOV";
            case A_ADV:     return "ADV";
            case A_DEC:     return "DEC";
            case A_JNZ:     return "JNZ";
            case A_JEZ:     return "JEZ";
            case A_PUSH:    return "PUSH";
            case A_CMP:     return "CMP";
            case A_END:     return "END";
            case A_TRIGHT:  return "TRIGHT";
            case A_TLEFT:   return "TLEFT";
            case A_PICKUP:  return "PICKUP";
            case A_PUTDOWN: return "PUTDOWN";
            case A_SCAN:    return "SCAN";
            case A_SORT:    return "SORT";
            case A_OPEN:    return "OPEN";
            case A_CLOSE:   return "CLOSE";
            case A_SENSOR:  return "SENSOR";
            default:        return $"???({opcode})";
        }
    }

    // =========================================================
    // API PUBLICA EXTRA
    // =========================================================

    public bool IsFinished => vmFinished && !vmError;
    public bool HasError => vmError;
    public bool IsRunning => vmRunning;

    public void RestartVM()
    {
        StopAllCoroutines();
        vmError    = false;
        vmFinished = false;
        StartVM();
    }

    public void DumpState()
    {
        Debug.Log(
            $"[SmartBinVM] Estado:\n" +
            $"  PC={PC}  IR1={IR1}  ACC={ACC}  SP={SP}\n" +
            $"  CRC={CRC}  AC={AC}\n" +
            $"  Flags: EQ={FLAG_EQ}  GT={FLAG_GT}  LT={FLAG_LT}\n" +
            $"  Running={vmRunning}  Finished={vmFinished}  Error={vmError}"
        );

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.Append("[SmartBinVM] Stack: [ ");
        for (int i = 0; i < SP; i++)
            sb.Append($"{stack[i]} ");
        sb.Append("]");
        Debug.Log(sb.ToString());
    }
}
