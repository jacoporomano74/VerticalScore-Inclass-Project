using System.Collections.Generic;
using UnityEngine;

public class FootstepAudio : MonoBehaviour
{
    // Campi che imposterai dall'Inspector
    public Transform joint;               // trascina qui l'oggetto "Joint" del player
    public AK.Wwise.Event footstepEvent;  // scegli Play_footsteps dal menu
    public float minSpeed = 0.5f;         // sotto questa velocita' il player conta come fermo

    [Header("Surface")]
    // Valore dello Switch "Surface" per ogni Terrain Layer (indice = indice del layer nel Terrain)
    public string[] terrainLayerSurfaces =
    {
        "Grass",  // 0  Grass
        "Ground", // 1  Rockwall
        "Ground", // 2  Pebbles
        "Ground", // 3  Pebbles_Sand
        "Ground", // 4  Rockwall
        "Grass",  // 5  Flowers
        "Ground", // 6  Leaves
        "Ground", // 7  Leaves_02
        "Ground", // 8  Sand
        "Ground", // 9  Snow
        "Mud",    // 10 Mud
        "Ground", // 11 Sand_Darker
        "Grass",  // 12 Grass_02
        "Ground", // 13 Leaves_03
        "Ground", // 14 Leaves_04
        "Ground", // 15 Pebbles_Sand
        "Ground", // 16 Sand_Desert
    };
    public string defaultSurface = "Ground";
    public bool debugSurface = false;

    static readonly string[] surfaceTags = { "Grass", "Ground", "Mud", "Wood", "Water" };

    Rigidbody rb;       // il Rigidbody del player, per leggere la velocita'
    float lastY;        // altezza della camera nel frame precedente
    bool goingDown;     // la camera stava scendendo?

    // Zone acqua (trigger con tag "Water") in cui il player si trova ora; un set gestisce le zone sovrapposte
    readonly HashSet<Collider> waterZones = new HashSet<Collider>();

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
        bool grounded = Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, 1.1f,
                                        ~0, QueryTriggerInteraction.Ignore);

        // Prima scendeva, ora risale = punto piu' basso del bob = piede a terra
        if (goingDown && y > lastY && moving && grounded)
        {
            string surface = DetectSurface(hit);
            if (debugSurface)
                Debug.Log($"Footstep surface: {surface} (hit: {hit.collider.name})", this);

            AkSoundEngine.SetSwitch("Surface", surface, gameObject);
            footstepEvent.Post(gameObject);
        }

        goingDown = y < lastY;
        lastY = y;
    }

    string DetectSurface(RaycastHit hit)
    {
        // OnTriggerExit non scatta se una zona viene disattivata o distrutta: la togliamo qui
        waterZones.RemoveWhere(z => z == null || !z.enabled || !z.gameObject.activeInHierarchy);
        if (waterZones.Count > 0)
            return "Water";

        if (hit.collider is TerrainCollider)
            return TerrainSurface(hit.collider.GetComponent<Terrain>(), hit.point);

        // .tag invece di CompareTag: CompareTag da' errore se il tag non e' ancora definito nel progetto
        string objectTag = hit.collider.tag;
        if (System.Array.IndexOf(surfaceTags, objectTag) >= 0)
            return objectTag;

        return defaultSurface;
    }

    string TerrainSurface(Terrain terrain, Vector3 worldPoint)
    {
        TerrainData data = terrain.terrainData;

        // Posizione del punto dentro il Terrain (0..1); il Terrain non supporta rotazioni, quindi niente InverseTransformPoint
        Vector3 local = worldPoint - terrain.GetPosition();
        int x = Mathf.Clamp((int)(local.x / data.size.x * data.alphamapWidth), 0, data.alphamapWidth - 1);
        int z = Mathf.Clamp((int)(local.z / data.size.z * data.alphamapHeight), 0, data.alphamapHeight - 1);

        // Peso di ogni layer in quel punto: prendiamo il piu' alto
        float[,,] weights = data.GetAlphamaps(x, z, 1, 1);
        int best = 0;
        for (int i = 1; i < weights.GetLength(2); i++)
        {
            if (weights[0, 0, i] > weights[0, 0, best])
                best = i;
        }

        if (best < terrainLayerSurfaces.Length && !string.IsNullOrEmpty(terrainLayerSurfaces[best]))
            return terrainLayerSurfaces[best];
        return defaultSurface;
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.tag == "Water")
            waterZones.Add(other);
    }

    void OnTriggerExit(Collider other)
    {
        waterZones.Remove(other);
    }
}
