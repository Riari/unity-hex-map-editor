using UnityEngine;
using UnityEngine.InputSystem;

public class FlyingCamera : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float fastMoveSpeed = 10f;
    
    [Header("Look Settings")]
    [SerializeField] private float mouseSensitivity = 2f;
    [SerializeField] private bool invertY = false;
    
    [Header("Zoom Settings")]
    [SerializeField] private float zoomSpeed = 2f;

    [Header("Input Actions")]
    [SerializeField] private InputActionReference moveActionRef;
    [SerializeField] private InputActionReference lookActionRef;
    [SerializeField] private InputActionReference rightMouseActionRef;
    [SerializeField] private InputActionReference fastMoveActionRef;
    [SerializeField] private InputActionReference verticalMoveActionRef;
    [SerializeField] private InputActionReference zoomActionRef;

    private bool _isRightMousePressed = false;
    private float _currentMoveSpeed;
    
    void Start()
    {
        _currentMoveSpeed = moveSpeed;
        Cursor.lockState = CursorLockMode.None;
        
        SetupInputCallbacks();
    }
    
    void OnEnable()
    {
        EnableInputActions();
    }
    
    void OnDisable()
    {
        DisableInputActions();
        Cursor.lockState = CursorLockMode.None;
    }
    
    void Update()
    {
        HandleMouseInput();
        HandleKeyboardInput();
        HandleZoom();
    }
    
    private void SetupInputCallbacks()
    {
        if (rightMouseActionRef?.action != null)
        {
            rightMouseActionRef.action.performed += OnRightMousePressed;
            rightMouseActionRef.action.canceled += OnRightMouseReleased;
        }
    }
    
    private void EnableInputActions()
    {
        moveActionRef?.action?.Enable();
        lookActionRef?.action?.Enable();
        rightMouseActionRef?.action?.Enable();
        fastMoveActionRef?.action?.Enable();
        verticalMoveActionRef?.action?.Enable();
        zoomActionRef?.action?.Enable();
    }
    
    private void DisableInputActions()
    {
        moveActionRef?.action?.Disable();
        lookActionRef?.action?.Disable();
        rightMouseActionRef?.action?.Disable();
        fastMoveActionRef?.action?.Disable();
        verticalMoveActionRef?.action?.Disable();
        zoomActionRef?.action?.Disable();
    }
    
    private void OnRightMousePressed(InputAction.CallbackContext context)
    {
        _isRightMousePressed = true;
        Cursor.lockState = CursorLockMode.Locked;
    }
    
    private void OnRightMouseReleased(InputAction.CallbackContext context)
    {
        _isRightMousePressed = false;
        Cursor.lockState = CursorLockMode.None;
    }
    
    private void HandleMouseInput()
    {
        if (!_isRightMousePressed || lookActionRef?.action == null) return;

        Vector2 mouseDelta = lookActionRef.action.ReadValue<Vector2>();
        
        float mouseX = mouseDelta.x * mouseSensitivity * Time.deltaTime;
        float mouseY = mouseDelta.y * mouseSensitivity * Time.deltaTime;
            
        if (invertY)
            mouseY = -mouseY;

        transform.Rotate(Vector3.up * mouseX, Space.World);
        transform.Rotate(Vector3.right * -mouseY, Space.Self);
    }
    
    private void HandleKeyboardInput()
    {
        bool isFastMove = fastMoveActionRef?.action?.IsPressed() ?? false;
        
        _currentMoveSpeed = isFastMove ? fastMoveSpeed : moveSpeed;

        Vector2 moveInput = moveActionRef?.action?.ReadValue<Vector2>() ?? Vector2.zero;
        float verticalInput = verticalMoveActionRef?.action?.ReadValue<float>() ?? 0f;
        
        Vector3 movement = Vector3.zero;
        
        movement += transform.forward * moveInput.y;
        movement += transform.right * moveInput.x;
        
        movement += Vector3.up * verticalInput;
        
        if (movement != Vector3.zero)
        {
            movement = movement.normalized * _currentMoveSpeed * Time.deltaTime;
            transform.Translate(movement, Space.World);
        }
    }
    
    private void HandleZoom()
    {
        if (zoomActionRef?.action == null) return;
        
        Vector2 scroll = zoomActionRef.action.ReadValue<Vector2>();
        if (scroll.Equals(Vector2.zero)) return;

        Vector3 zoomMovement = transform.forward * scroll * zoomSpeed * Time.deltaTime;
        transform.Translate(zoomMovement, Space.World);
    }
    
    void OnDestroy()
    {
        if (rightMouseActionRef?.action != null)
        {
            rightMouseActionRef.action.performed -= OnRightMousePressed;
            rightMouseActionRef.action.canceled -= OnRightMouseReleased;
        }
    }
}