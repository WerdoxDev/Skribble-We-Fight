using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : NetworkBehaviour {
    [SerializeField] private new Camera camera;
    [SerializeField] private Transform spritesWrapper;
    [SerializeField] private float walkSpeed;
    [SerializeField] private float smoothTime;
    [SerializeField] private float rotationOffset;

    private Rigidbody2D _rb;
    private Vector2 _movementInput;
    private Vector2 _smoothedInput;
    private Vector2 _smoothedCurrentVelocity;

    private InputActions _input;

    private void Awake() {
        _rb = GetComponent<Rigidbody2D>();
    }

    public override void OnNetworkSpawn() {
        if (!IsOwner) {
            enabled = false;
            camera.gameObject.SetActive(false);
            return;
        }

        Camera.main.gameObject.SetActive(false);
        SetInputState(true);
    }

    public override void OnNetworkDespawn() {
        SetInputState(false);
    }

    private void Update() {
        Vector2 mousePosition = camera.ScreenToWorldPoint(Mouse.current.position.value);
        float AngleRad = Mathf.Atan2(mousePosition.y - transform.position.y, mousePosition.x - transform.position.x);
        float AngleDeg = (180 / Mathf.PI) * AngleRad;
        spritesWrapper.rotation = Quaternion.Euler(0, 0, AngleDeg + rotationOffset);

        //_player.CurrentSprite.transform.up = mousePosition - new Vector2(transform.position.x, transform.position.y);
    }

    private void FixedUpdate() {
        _smoothedInput = Vector2.SmoothDamp(_smoothedInput, _movementInput, ref _smoothedCurrentVelocity, smoothTime);
        _rb.velocity = _smoothedInput * walkSpeed;
    }

    private void SetInputState(bool enabled) {
        void OnWalk(InputAction.CallbackContext context) => _movementInput = context.ReadValue<Vector2>();

        if (!enabled) {
            _input.Player.Walk.performed -= OnWalk;
            _input.Player.Walk.canceled -= OnWalk;
        }
        else {
            _input = new();
            _input.Player.Enable();

            _input.Player.Walk.performed += OnWalk;
            _input.Player.Walk.canceled += OnWalk;
        }
    }
}
