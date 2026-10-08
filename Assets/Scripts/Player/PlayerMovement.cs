using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Atributos de Movimento e Aceleração")]
    public float maxSpeed = 8f;
    public float acceleration = 12f;      // Quão rápido ele vai de andando para correndo
    public float deceleration = 20f;      // Quão rápido ele derrapa até parar ao soltar o direcional
    public float rotationSpeed = 900f;    // Aumentado para fazer a curva em "U" (U-turn) fluir melhor
    
    [Header("Atributos de Pulo e Gravidade")]
    public float jumpHeight = 2f;
    public float gravity = -19.62f; 

    [Header("Movimentos Especiais (Mario)")]
    public float crouchSpeed = 3f;           
    public float longJumpHeight = 1.2f;      
    public float longJumpForwardSpeed = 15f; 
    public float diveBoost = 14f;            

    // Ações do Novo Input System
    private InputAction moveAction;
    private InputAction jumpAction;
    private InputAction crouchAction;

    private CharacterController controller;
    private Vector3 velocity;
    private Vector3 airMomentum;
    private bool isGrounded;

    // Máquina de Estados e Inércia
    private float currentSpeed; // Guarda o embalo real do personagem frame a frame
    private bool isCrouching;
    private bool isLongJumping;
    private bool isDiving;

    void Awake()
    {
        controller = GetComponent<CharacterController>();

        // 1. Ação de Andar
        moveAction = new InputAction("Move");
        moveAction.AddCompositeBinding("Dpad")
            .With("Up", "<Keyboard>/w")
            .With("Down", "<Keyboard>/s")
            .With("Left", "<Keyboard>/a")
            .With("Right", "<Keyboard>/d")
            .With("Up", "<Gamepad>/leftStick/up")
            .With("Down", "<Gamepad>/leftStick/down")
            .With("Left", "<Gamepad>/leftStick/left")
            .With("Right", "<Gamepad>/leftStick/right");

        // 2. Ação de Pulo
        jumpAction = new InputAction("Jump", binding: "<Keyboard>/space");
        jumpAction.AddBinding("<Gamepad>/buttonSouth"); 

        // 3. Ação de Agachar/Dive 
        crouchAction = new InputAction("Crouch", binding: "<Keyboard>/leftShift");
        crouchAction.AddBinding("<Gamepad>/buttonEast"); 
        crouchAction.AddBinding("<Gamepad>/rightTrigger"); 
    }

    void OnEnable()
    {
        moveAction.Enable();
        jumpAction.Enable();
        crouchAction.Enable(); 
    }

    void OnDisable()
    {
        moveAction.Disable();
        jumpAction.Disable();
        crouchAction.Disable();
    }

    void Update()
    {
        isGrounded = controller.isGrounded;
        
        bool crouchHeld = crouchAction.IsPressed();
        bool crouchPressed = crouchAction.WasPressedThisFrame();
        bool jumpPressed = jumpAction.WasPressedThisFrame();

        if (isGrounded && velocity.y < 0)
        {
            velocity.y = -2f; 
            isLongJumping = false;
            isDiving = false;
            airMomentum = Vector3.zero;
        }

        Vector2 inputDir = moveAction.ReadValue<Vector2>();
        
        // O magnitude lê o quão forte o analógico do Gamepad está inclinado (0 a 1)
        float inputMagnitude = Mathf.Clamp01(inputDir.magnitude);
        Vector3 move = new Vector3(inputDir.x, 0f, inputDir.y).normalized;

        float targetSpeed = 0f;

        // --- LÓGICA NO CHÃO ---
        if (isGrounded)
        {
            if (crouchHeld)
            {
                isCrouching = true;
                targetSpeed = crouchSpeed * inputMagnitude; 
            }
            else
            {
                isCrouching = false;
                targetSpeed = maxSpeed * inputMagnitude;
            }

            // A Mágica da Aceleração: Interpolação de Embalo
            if (inputMagnitude > 0.1f)
            {
                // Ganha velocidade progressivamente (Andar -> Correr)
                currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, acceleration * Time.deltaTime);
            }
            else
            {
                // Derrapa até parar ao soltar o direcional
                currentSpeed = Mathf.MoveTowards(currentSpeed, 0f, deceleration * Time.deltaTime);
            }

            if (jumpPressed)
            {
                // Agora o Long Jump exige que o jogador já tenha ganhado um pouco de embalo
                if (isCrouching && currentSpeed > 2f)
                {
                    velocity.y = Mathf.Sqrt(longJumpHeight * -2f * gravity);
                    // Usa o maior valor: O boost fixo ou o embalo atual potencializado
                    airMomentum = transform.forward * Mathf.Max(longJumpForwardSpeed, currentSpeed * 1.5f); 
                    isLongJumping = true;
                }
                else
                {
                    velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity); 
                }
            }
        }
        // --- LÓGICA NO AR ---
        else
        {
            if (crouchPressed && !isDiving)
            {
                isDiving = true;
                isLongJumping = false; 
                
                velocity.y = 3f; 
                airMomentum = transform.forward * Mathf.Max(diveBoost, currentSpeed * 1.2f); 
            }

            // Controle aéreo "escorregadio" (Acelera e desacelera muito mais devagar no ar)
            if (!isLongJumping && !isDiving)
            {
                targetSpeed = maxSpeed * inputMagnitude;
                currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, (acceleration * 0.3f) * Time.deltaTime);
            }
            else
            {
                currentSpeed = Mathf.MoveTowards(currentSpeed, 0f, (deceleration * 0.1f) * Time.deltaTime);
            }
        }

        // --- ROTAÇÃO ---
        if (move != Vector3.zero)
        {
            Quaternion toRotation = Quaternion.LookRotation(move, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, toRotation, rotationSpeed * Time.deltaTime);
        }

        // --- APLICAÇÃO DO MOVIMENTO HORIZONTAL ---
        // Pulo do Gato para Game Design: O vetor base deixou de ser 'move' e virou 'transform.forward'.
        // Isso significa que se você correr pra frente e puxar o analógico pra trás bruscamente,
        // o personagem começa a rotacionar, mas o corpo é jogado num arco aberto (U-Turn).
        Vector3 finalMove = (transform.forward * currentSpeed) + airMomentum;
        controller.Move(finalMove * Time.deltaTime);

        // --- APLICAÇÃO DA GRAVIDADE ---
        float currentGravity = isDiving ? gravity * 1.8f : gravity;
        velocity.y += currentGravity * Time.deltaTime;
        
        controller.Move(velocity * Time.deltaTime);
    }
}