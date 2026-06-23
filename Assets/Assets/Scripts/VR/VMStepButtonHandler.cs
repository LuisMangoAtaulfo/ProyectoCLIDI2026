using UnityEngine;

public class VMStepButtonHandler : MonoBehaviour
{
    [Header("Referencia a la VM")]
    [Tooltip("Si esta vacio se busca automaticamente en la escena.")]
    public SmartBinVM vm;

    void Awake()
    {
        if (vm == null)
            vm = FindObjectOfType<SmartBinVM>();

        if (vm == null)
            Debug.LogError("[VMStepButtonHandler] No se encontro SmartBinVM en la escena.");
    }

    public void StepVM()
    {
        if (vm == null)
        {
            Debug.LogError("[VMStepButtonHandler] SmartBinVM no asignada.");
            return;
        }

        if (!vm.IsRunning && !vm.HasError)
        {
            vm.StartVM();
            vm.manualStep = true;
        }

        vm.StepOnce();
        Debug.Log("[VMStepButtonHandler] STEP ejecutado.");
    }
}
