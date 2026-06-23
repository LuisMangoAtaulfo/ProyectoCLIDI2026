# Implementacion Manual en Unity — 4 Botes Independientes

Los scripts C# ya estan actualizados. Sigue estos pasos en el Editor de Unity.

---

## 1. Cambiar los Tags de los prefabs de basura

| Tag viejo      | Tag nuevo      |
|----------------|----------------|
| `Metal`        | `Recyclable`   |
| `Paper`        | `PET`          |
| `Plastic`      | `NonRecyclable`|
| `Organic`      | `Organic` (sin cambio) |

**Window > Asset Management > Tags and Layers**: elimina los tags viejos y crea los nuevos.
Luego selecciona cada prefab de basura y cambiale el Tag en el Inspector.

---

## 2. Montar los 4 botes en la escena

Cada bote necesita esta jerarquia:

```
Bin_Recyclable                       (GameObject vacio)
  ├── LidPivot                       (Transform que rota en X)
  │     └── LidMesh                  (Modelo 3D de la tapa)
  ├── DetectionZone                  (Collider IsTrigger=true)
  │     └── (modelo opcional)
  └── Gate                           (Transform de la compuerta)

Bin_PET                               (igual estructura)
Bin_Organic                           (igual estructura)
Bin_NonRecyclable                     (igual estructura)
```

### Por cada bote, configura:

| Componente | GameObject | Script | Campo clave |
|------------|------------|--------|-------------|
| Tapa | Raiz del bote | `BinLidController` | `binIndex` = 0,1,2,3 segun bote |
| Tapa | Raiz del bote | `BinLidController` | `lidPivot` = el Transform que rota |
| Tapa | Raiz del bote | `BinLidController` | `vm` = referencia al SmartBinVM |
| Deteccion | DetectionZone | `BinSensorController` | `binIndex` = 0,1,2,3 (mismo que su bote) |
| Deteccion | DetectionZone | Collider | `Is Trigger = true` |
| Deteccion | DetectionZone | Collider | Que no choque con paredes |

### Indices de botes:

| binIndex | Bote |
|----------|------|
| 0 | Recyclable |
| 1 | PET |
| 2 | Organic |
| 3 | NonRecyclable |

---

## 3. Configurar el SmartBinVM (GameObject central)

En el Inspector del GameObject que tiene `SmartBinVM`:

- El array `binSensors` se auto-puebla via `RegisterBinSensor()` al iniciar la escena. No necesitas asignarlo manualmente.
- `activeBin` se actualiza automaticamente cuando un `BinLidController` activa su secuencia.

---

## 4. Configurar el SmartBinAnimator (GameObject central)

1. Asigna los 4 gate Transforms en el Inspector:
   - `gateRecyclable` = Transform de la compuerta del bote Recyclable
   - `gatePET` = Transform de la compuerta del bote PET
   - `gateOrganic` = Transform de la compuerta del bote Organic
   - `gateNonRecyclable` = Transform de la compuerta del bote NonRecyclable

2. El campo `binSensors` es opcional si los `BinSensorController` tienen `binIndex >= 0`.
   Si quieres asignarlo manualmente, arrastra cada BinSensorController al slot correspondiente.

3. El campo `sensorCtrl` puede quedar vacio — se usa como fallback si `binSensors` no esta configurado.

---

## 5. Capa "Basura"

Asegurate de que:
- Existe la layer `Basura` en **Project Settings > Tags and Layers > Layers**
- Todos los prefabs de residuo tienen asignada esta layer
- Es independiente del tag (layer es para fisica/triggers, tag es para clasificacion)

---

## 6. Verificar el flujo completo

1. Dale **Play**.
2. Coloca un residuo con tag `PET` sobre la tapa de `Bin_PET`.
3. Debes ver en consola:
   ```
   [SmartBinVM] SCAN
   [SmartBinVM] SENSOR PET -> 1
   [SmartBinAnimator] Tipo detectado: 'PET' -> compuerta: gatePET
   [SmartBinVM] OPEN
   [SmartBinAnimator] OPEN -> gatePET
   [SmartBinVM] SORT
   [SmartBinVM] CLOSE
   [SmartBinAnimator] CLOSE -> gatePET
   [SmartBinVM] Programa finalizado.
   ```
4. Prueba cada bote con su tipo de residuo correspondiente.

---

## 7. Bug en el parser C (opcional)

En `parser_smartbin_rdcp.c`, funcion `Condition()`, bloque del operador `OR`:

```c
        /* Both false */
        GC(A_PUSH);
        GC(AM_IMMEDT);
        GC(0);
        GC(A_PUSH);
        GC(AM_IMMEDT);
        GC(1);
        GC(A_CMP);
        GC(A_JEZ);          // <-- CAMBIAR A JNZ
        int pos_or_end = PC;
        GC(0);
```

Cambia `GC(A_JEZ);` por `GC(A_JNZ);`. El bug actual causa que `false OR false` retorne `1` en vez de `0`.
