using UnityEngine;

public class FootstepAudio : MonoBehaviour
{
    // Campi che imposterai dall'Inspector
    public Transform joint;               // trascina qui l'oggetto "Joint" del player
    public AK.Wwise.Event footstepEvent;  // scegli Play_footsteps dal menu
    public float minSpeed = 0.5f;         // sotto questa velocita' il player conta come fermo

    Rigidbody rb;       // il Rigidbody del player, per leggere la velocita'
    float lastY;        // altezza della camera nel frame precedente
    bool goingDown;     // la camera stava scendendo?

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        lastY = joint.localPosition.y;
    }

    void Update()
    {
        float y = joint.localPosition.y;   // altezza attuale della camera (head bob)

        // Il player si sta muovendo? (solo velocita' orizzontale)
        Vector3 v = rb.velocity;
        v.y = 0f;
        bool moving = v.magnitude > minSpeed;

        // Il player e' a terra? Raggio verso il basso, ignorando le zone trigger
        bool grounded = Physics.Raycast(transform.position, Vector3.down, 1.1f,
                                        ~0, QueryTriggerInteraction.Ignore);

        // Prima scendeva, ora risale = punto piu' basso del bob = piede a terra
        if (goingDown && y > lastY && moving && grounded)
            footstepEvent.Post(gameObject);

        goingDown = y < lastY;
        lastY = y;
    }
}