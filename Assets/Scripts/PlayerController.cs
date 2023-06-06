using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : NetworkBehaviour {
    public NetworkVariable<int> Health = new();

    [Header("General")]
    [SerializeField] private new Camera camera;
    [SerializeField] private Transform spritesWrapper;
    [SerializeField] private string playerLayer;
    [SerializeField] private string localPlayerLayer;
    [SerializeField] private string ignoreLayer;
    [SerializeField] private LayerMask obstacleLayer;
    [SerializeField] private BoxCollider2D[] weaponColliders;
    [SerializeField] private GameObject[] weaponSprites;
    [SerializeField] private SpriteRenderer[] sprites;

    [Header("Audio")]
    [SerializeField] private AudioSource[] sounds;
    [SerializeField] private AudioSource damageSound;
    [SerializeField] private float nonLocalSoundVolume;
    [SerializeField] private float nonLocalSpatialBalance;

    [Header("Gameplay")]
    [SerializeField] private int maxHealth;
    [SerializeField] private int weaponNormalDamage;
    [SerializeField] private int weaponSecondaryDamage;
    [SerializeField] private float walkSpeed;
    [SerializeField] private float smoothTime;
    [SerializeField] private float rotationOffset;
    [SerializeField] private float obstacleCheckDistance;
    [SerializeField] private Color weaponShineColor;

    [Header("Animation")]
    [SerializeField] private float minTimeBetweenAttack;
    [SerializeField] private float timeUntilAttackReset;
    [SerializeField] private float timeUntilSecondaryAttack;

    public int NormalDamage => weaponNormalDamage;
    public int SecondaryDamage => weaponSecondaryDamage;
    public Animator Animator => _animator;

    private Animator _animator;
    private Rigidbody2D _rb;
    private Vector2 _movementInput;
    private Vector2 _smoothedInput;
    private Vector2 _smoothedCurrentVelocity;

    private IEnumerator _attackPhaseCoroutine;
    private bool _canDoSecondaryAttack;
    private bool _canAttack = true;

    private InputActions _input;

    private void Awake() {
        _animator = GetComponent<Animator>();
        _rb = GetComponent<Rigidbody2D>();
    }

    public override void OnNetworkSpawn() {
        Health.OnValueChanged += (oldHealth, newHealth) => {
            if (IsOwner && newHealth != 0 && newHealth != maxHealth) {
                GameController.Instance.SetHealthBarProgress(newHealth, maxHealth);
                damageSound.Play();
            }

            for (int i = 0; i < sprites.Length; i++)
                sprites[i].material.SetFloat("_GrayscaleAmount", 1 - (newHealth / (float)maxHealth));
        };

        if (IsServer) {
            Health.Value = maxHealth;
        }

        if (!IsOwner) {
            enabled = false;
            camera.gameObject.SetActive(false);
            SetLayerRecursively(transform, LayerMask.NameToLayer(playerLayer));
            return;
        }

        GameController.Instance.SetHealthBarProgress(Health.Value, maxHealth);

        SetInputState(true);
    }

    public override void OnNetworkDespawn() {
        if (!IsOwner) return;
        SetInputState(false);
    }

    private void Update() {
        Vector2 mousePosition = camera.ScreenToWorldPoint(Mouse.current.position.value);
        float AngleRad = Mathf.Atan2(mousePosition.y - transform.position.y, mousePosition.x - transform.position.x);
        float AngleDeg = (180 / Mathf.PI) * AngleRad;
        spritesWrapper.rotation = Quaternion.Euler(0, 0, AngleDeg + rotationOffset);
    }

    private void FixedUpdate() {
        _smoothedInput = Vector2.SmoothDamp(_smoothedInput, _movementInput, ref _smoothedCurrentVelocity, smoothTime);
        _rb.velocity = _smoothedInput * walkSpeed;
    }

    private void OnTriggerEnter2D(Collider2D other) {
        if (!IsServer) return;
        if (!_canAttack || other.gameObject.layer == LayerMask.NameToLayer(ignoreLayer)) return;

        int pLayer = LayerMask.NameToLayer(playerLayer);
        int lpLayer = LayerMask.NameToLayer(localPlayerLayer);

        RaycastHit2D[] hits = Physics2D.RaycastAll(transform.position, spritesWrapper.up, obstacleCheckDistance, obstacleLayer);

        if (hits.Length >= 2 && hits[0].collider.gameObject == gameObject &&
            hits[1].collider.gameObject.layer != pLayer && hits[1].collider.gameObject.layer != lpLayer) return;

        if (other.TryGetComponent(out PlayerController player)) {
            if (player.OwnerClientId == OwnerClientId) return;
            player.TakeDamage(_animator.GetInteger("AttackPhase") == 1 ? weaponNormalDamage : weaponSecondaryDamage);
            GameController.Instance.SpawnPlayerHitParticleClientRpc(other.ClosestPoint(transform.position));
            PlayDamageSoundClientRpc(new() { Send = new() { TargetClientIds = new[] { OwnerClientId } } });
            StartCoroutine(AvoidAttackDupeEnumerator());
        }
    }

    // This is because animation event cannot see methods with parameters
    public void EnableAttack() => SetAttackActive(true);
    public void DisableAttack() => SetAttackActive(false);

    public void PlayAttackSound(int soundIndex) {
        if (!IsOwner) sounds[soundIndex].spatialBlend = nonLocalSpatialBalance;
        sounds[soundIndex].PlayOneShot(sounds[soundIndex].clip, IsOwner ? sounds[soundIndex].volume : sounds[soundIndex].volume * nonLocalSoundVolume);
    }

    private void Attack() {
        if (_attackPhaseCoroutine != null && _animator.GetInteger("AttackPhase") != 2) StopCoroutine(_attackPhaseCoroutine);

        if (_animator.GetInteger("AttackPhase") == 1 && _canDoSecondaryAttack)
            _animator.SetInteger("AttackPhase", 2);
        else if (_animator.GetInteger("AttackPhase") == 0)
            _animator.SetInteger("AttackPhase", 1);

        _attackPhaseCoroutine = AttackPhaseEnumerator();
        StartCoroutine(_attackPhaseCoroutine);
    }

    public void TakeDamage(int damage) {
        Health.Value -= damage;

        if (Health.Value <= 0) {
            GameController.Instance.PlayerDied(OwnerClientId);
            Health.Value = 0;
        }
    }

    private IEnumerator AttackPhaseEnumerator() {
        yield return new WaitForSeconds(timeUntilSecondaryAttack);
        foreach (GameObject weapon in weaponSprites) LeanTween.color(weapon, weaponShineColor, 0.125f);
        _canDoSecondaryAttack = true;
        yield return new WaitForSeconds(timeUntilAttackReset - timeUntilSecondaryAttack);
        _canDoSecondaryAttack = false;
        foreach (GameObject weapon in weaponSprites) LeanTween.color(weapon, Color.white, 0.125f);
        _animator.SetInteger("AttackPhase", 0);
    }

    private IEnumerator AvoidAttackDupeEnumerator() {
        _canAttack = false;
        yield return new WaitForSeconds(minTimeBetweenAttack);
        _canAttack = true;
    }

    private void SetInputState(bool enabled) {
        void OnWalk(InputAction.CallbackContext context) => _movementInput = context.ReadValue<Vector2>();
        void OnAttack(InputAction.CallbackContext context) {
            if (context.performed) Attack();
        }

        if (!enabled) {
            _input.Player.Attack.performed -= OnAttack;
            _input.Player.Attack.canceled -= OnAttack;
            _input.Player.Walk.performed -= OnWalk;
            _input.Player.Walk.canceled -= OnWalk;
        }
        else {
            _input = new();
            _input.Player.Enable();

            _input.Player.Attack.performed += OnAttack;
            _input.Player.Attack.canceled += OnAttack;
            _input.Player.Walk.performed += OnWalk;
            _input.Player.Walk.canceled += OnWalk;
        }
    }

    private void SetAttackActive(bool isActive) {
        if (!IsServer) return;
        for (int i = 0; i < weaponColliders.Length; i++) {
            weaponColliders[i].enabled = isActive;
        }
    }

    private void SetLayerRecursively(Transform parent, int newLayer) {
        parent.gameObject.layer = newLayer;

        for (int i = 0, count = parent.childCount; i < count; i++) {
            if (parent.GetChild(i).gameObject.layer == LayerMask.NameToLayer(ignoreLayer)) continue;
            SetLayerRecursively(parent.GetChild(i), newLayer);
        }
    }

    private void OnDrawGizmos() {
        Gizmos.DrawRay(transform.position, spritesWrapper.up * obstacleCheckDistance);
    }

    [ClientRpc]
    public void PlayDamageSoundClientRpc(ClientRpcParams rpcParams) {
        damageSound.Play();
    }
}
