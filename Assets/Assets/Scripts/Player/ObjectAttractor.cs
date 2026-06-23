using UnityEngine;

public class ObjectAttractor : MonoBehaviour
{
    [Header("Configuración de Atracción")]
    public Transform holdPoint;
    public float pickupRange = 5f;
    public float attractForce = 10f;
    
    private Rigidbody heldObject;
    private float originalDrag;

    void Update()
    {
        // Clic izquierdo para agarrar o soltar
        if (Input.GetMouseButtonDown(0))
        {
            if (heldObject == null)
            {
                TryGrabObject();
            }
            else
            {
                DropObject();
            }
        }
    }

    void FixedUpdate()
    {
        // Si tenemos un objeto, lo movemos hacia el HoldPoint usando físicas
        if (heldObject != null)
        {
            MoveObjectToHoldPoint();
        }
    }

    void TryGrabObject()
    {
        RaycastHit hit;
        // Lanza un rayo desde la cámara hacia adelante
        if (Physics.Raycast(transform.position, transform.forward, out hit, pickupRange))
        {
            // Verifica si el objeto tiene un Rigidbody (esencial para que funcione)
            Rigidbody rb = hit.transform.GetComponent<Rigidbody>();
            
            if (rb != null)
            {
                heldObject = rb;
                originalDrag = heldObject.linearDamping;
                
                // Ajustamos las físicas del objeto mientras lo sostenemos
                heldObject.useGravity = false;
                heldObject.linearDamping = 10f; // Mucha fricción para que no se vuelva loco
                heldObject.freezeRotation = true;
            }
        }
    }

    void DropObject()
    {
        if (heldObject != null)
        {
            // Restauramos las físicas normales del objeto
            heldObject.useGravity = true;
            heldObject.linearDamping = originalDrag;
            heldObject.freezeRotation = false;
            heldObject = null;
        }
    }

    void MoveObjectToHoldPoint()
    {
        // Calcula la dirección y la distancia hacia el HoldPoint
        Vector3 directionToPoint = holdPoint.position - heldObject.position;
        float distanceToPoint = directionToPoint.magnitude;

        // Aplica velocidad al Rigidbody para que viaje hacia el punto
        heldObject.linearVelocity = directionToPoint * attractForce;
    }
}