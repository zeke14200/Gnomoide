using UnityEngine;
using UnityEngine.InputSystem; // Importação obrigatória para o novo sistema

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Atributos de Movimento")]
    public float speed = 8f;
    public float rotationSpeed = 720f;
    
    [Header("Atributos de Pulo e Gravidade")]
    public float jumpHeight = 2f;
    public float gravity = -19.62f; 

    // Ações do Novo Input System
    private InputAction moveAction;
    private InputAction jumpAction;

    private CharacterController controller;
    private Vector3 velocity;
    private bool isGrounded;

    void Awake()
    {
        controller = GetComponent<CharacterController>();

        // 1. Configurando a ação de Andar via código (Suporta Teclado e Gamepad)
        moveAction = new InputAction("Move");
        moveAction.AddCompositeBinding("Dpad")
            .With("Up", "<Keyboard>/w")
            .With("Down", "<Keyboard>/s")
            .With("Left", "<Keyboard>/a")
            .With("Right", "<Keyboard>/d")
            .With("Up", "<Keyboard>/upArrow")
            .With("Down", "<Keyboard>/downArrow")
            .With("Left", "<Keyboard>/leftArrow")
            .With("Right", "<Keyboard>/rightArrow")
            .With("Up", "<Gamepad>/leftStick/up")
            .With("Down", "<Gamepad>/leftStick/down")
            .With("Left", "<Gamepad>/leftStick/left")
            .With("Right", "<Gamepad>/leftStick/right");

        // 2. Configurando a ação de Pulo (Espaço no Teclado ou Botão A/X no Gamepad)
        jumpAction = new InputAction("Jump", binding: "<Keyboard>/space");
        jumpAction.AddBinding("<Gamepad>/buttonSouth"); 
    }

    // É obrigatório ativar as ações quando o objeto liga
    void OnEnable()
    {
        moveAction.Enable();
        jumpAction.Enable();
    }

    // E desativar quando o objeto desliga para evitar erros de memória
    void OnDisable()
    {
        moveAction.Disable();
        jumpAction.Disable();
    }

    void Update()
    {
        isGrounded = controller.isGrounded;
        
        if (isGrounded && velocity.y < 0)
        {
            velocity.y = -2f; 
        }

        // Lê os valores de movimento do Novo Input System como um Vector2
        Vector2 inputDir = moveAction.ReadValue<Vector2>();
        
        // Transforma o Vector2 (X, Y) em um Vector3 (X, 0, Z) para o mundo 3D
        Vector3 move = new Vector3(inputDir.x, 0f, inputDir.y).normalized;

        controller.Move(move * speed * Time.deltaTime);

        // Rotação do personagem
        if (move != Vector3.zero)
        {
            Quaternion toRotation = Quaternion.LookRotation(move, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, toRotation, rotationSpeed * Time.deltaTime);
        }

        // Lógica de pulo adaptada para o Novo Input System (WasPressedThisFrame)
        if (jumpAction.WasPressedThisFrame() && isGrounded)
        {
            // Fórmula do pulo: $v = \sqrt{h \cdot -2 \cdot g}$
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity); 
        }

        // Aplica a gravidade continuamente
        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }
}