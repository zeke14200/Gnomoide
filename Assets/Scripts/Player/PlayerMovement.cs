using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Atributos de Movimento e Aceleração")]
    public float maxSpeed = 8f;
    public float acceleration = 12f;      
    public float deceleration = 20f;      
    public float rotationSpeed = 900f;    
    
    [Header("Atributos de Pulo e Gravidade")]
    public float jumpHeight = 2f;
    public float gravity = -19.62f; 

    [Header("Movimentos Especiais")]
    public float crouchSpeed = 3f;           
    public float longJumpHeight = 1.2f;      
    public float longJumpForwardSpeed = 15f; 
    public float diveBoost = 14f;            

    private InputAction moveAction;
    private InputAction jumpAction;
    private InputAction crouchAction;

    private CharacterController controller;
    private Animator animator;
    private Vector3 velocity;
    private Vector3 airMomentum;
    private bool isGrounded;

    private float currentSpeed; 
    
    public bool isCrouching { get; private set; }
    
    private bool isLongJumping;
    private bool isDiving;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        animator = GetComponentInChildren<Animator>(true);        
        
        if (animator != null)
        {
            animator.SetFloat("Velocidade", currentSpeed);
        }
    
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

        jumpAction = new InputAction("Jump", binding: "<Keyboard>/space");
        jumpAction.AddBinding("<Gamepad>/buttonSouth"); 

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
        float inputMagnitude = Mathf.Clamp01(inputDir.magnitude);
        Vector3 move = new Vector3(inputDir.x, 0f, inputDir.y).normalized;
        float targetSpeed = 0f;

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

            if (inputMagnitude > 0.1f)
            {
                currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, acceleration * Time.deltaTime);
            }
            else
            {
                currentSpeed = Mathf.MoveTowards(currentSpeed, 0f, deceleration * Time.deltaTime);
            }

            if (jumpPressed)
            {
                if (isCrouching && currentSpeed > 2f)
                {
                    velocity.y = Mathf.Sqrt(longJumpHeight * -2f * gravity);
                    airMomentum = transform.forward * Mathf.Max(longJumpForwardSpeed, currentSpeed * 1.5f); 
                    isLongJumping = true;
                }
                else
                {
                    velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity); 
                }
            }
        }
        else
        {
            if (crouchPressed && !isDiving)
            {
                isDiving = true;
                isLongJumping = false; 
                velocity.y = 3f; 
                airMomentum = transform.forward * Mathf.Max(diveBoost, currentSpeed * 1.2f); 
            }

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

        if (move != Vector3.zero)
        {
            Quaternion toRotation = Quaternion.LookRotation(move, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, toRotation, rotationSpeed * Time.deltaTime);
        }

        Vector3 finalMove = (transform.forward * currentSpeed) + airMomentum;
        controller.Move(finalMove * Time.deltaTime);

        float currentGravity = isDiving ? gravity * 1.8f : gravity;
        velocity.y += currentGravity * Time.deltaTime;
        
        controller.Move(velocity * Time.deltaTime);
        
        if (animator != null)
        {
            animator.SetFloat("Velocidade", currentSpeed);        }
    }

    void OnControllerColliderHit(ControllerColliderHit hit)
    {
        FurniturePhysics furniture = hit.collider.GetComponent<FurniturePhysics>();
        if (furniture != null)
        {
            if (hit.normal.y > 0.5f) return;

            bool isSpecialMove = isLongJumping || isDiving;
            float impactForce = isSpecialMove ? airMomentum.magnitude : currentSpeed;
            Vector3 impactDirection = isSpecialMove ? airMomentum.normalized : transform.forward;

            furniture.ReceiveImpact(impactDirection, impactForce, isSpecialMove, hit.point);
        }
    }
}