using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Tilemaps;

public class PacmanController : MonoBehaviour
{
    public InputAction MoveAction;
    public float Speed = 5.0f;

    [Header("Debug")]
    public bool limitFPS = false;
    
    private Rigidbody2D rb;
    private Vector2 currentDirection = Vector2.right;
    private Vector2 nextDirection = Vector2.right;

    // Exposto para a IA dos fantasmas (Pinky/Inky miram à frente do Pac-Man)
    public Vector2 CurrentDirection => currentDirection;
    
    void Start()
    {
        rb = GetComponent<Rigidbody2D>();

        RecenterPositionOnGrid();
        
        MoveAction.Enable();

        if (Application.isEditor && limitFPS)
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 10;
        }
    }

    private void RecenterPositionOnGrid()
    {
    }

    void Update()
    {
        // O Update só guarda a intenção. Quem decide é o FixedUpdate, onde a
        // posição física é consistente e dá para saber se está no centro da célula.
        var moveVector = MoveAction.ReadValue<Vector2>();

        if (moveVector.y > 0) nextDirection = Vector2.up;
        else if (moveVector.y < 0) nextDirection = Vector2.down;
        else if (moveVector.x < 0) nextDirection = Vector2.left;
        else if (moveVector.x > 0) nextDirection = Vector2.right;
    }

    void FixedUpdate()
    {
        currentDirection = nextDirection;
        rb.linearVelocity = currentDirection * Speed;
    }

    public void HasDied()
    {
        // gameObject.SetActive(false);
        Debug.Log("Game Over!");
    }
    
    private void OnTriggerStay2D(Collider2D other)
    {
        if (other.CompareTag("Pellet"))
        {
            var tilemap = other.GetComponent<Tilemap>();
            
            var cellPosition = tilemap.WorldToCell(transform.position);
            if (tilemap.HasTile(cellPosition))
            {
                tilemap.SetTile(cellPosition, null);
            }
        }
    }
}
