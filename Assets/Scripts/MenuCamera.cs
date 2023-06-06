using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MenuCamera : MonoBehaviour {

    [Header("Position")]
    [SerializeField] private float minPosition;
    [SerializeField] private float maxPosition;
    [SerializeField] private float minPositionDistance;
    [SerializeField] private float positionSpeed;

    [Header("Rotation")]
    [SerializeField] private float maxRotation;
    [SerializeField] private float minRotation;
    [SerializeField] private float minRotationDistance;
    [SerializeField] private float rotationSpeed;

    private bool _reachedRotation;
    private bool _reachedPosition;
    private Vector3 _currentRotation;
    private Vector3 _currentPosition;

    private void Update() {
        RandomPosition();
        RandomRotation();
    }

    private void RandomPosition() {
        if (_reachedPosition || _currentPosition == Vector3.zero) {
            _currentPosition = transform.position;

            _currentPosition.x = Random.Range(minPosition, maxPosition);
            _currentPosition.y = Random.Range(minPosition, maxPosition);

            _reachedPosition = false;
        }

        transform.position = Vector3.Lerp(transform.position, _currentPosition, Time.deltaTime * positionSpeed);

        if (Vector3.Distance(transform.position, _currentPosition) <= minPositionDistance) _reachedPosition = true;
    }

    private void RandomRotation() {
        if (_reachedRotation || _currentRotation == Vector3.zero) {
            _currentRotation = transform.eulerAngles;

            _currentRotation.x = Random.Range(minRotation, maxRotation);
            _currentRotation.y = Random.Range(minRotation, maxRotation);

            _reachedRotation = false;
        }

        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(_currentRotation), Time.deltaTime * rotationSpeed);

        if (Approximately(transform.rotation, Quaternion.Euler(_currentRotation), minRotationDistance)) _reachedRotation = true;
    }

    private bool Approximately(Quaternion quatA, Quaternion value, float acceptableRange) {
        return 1 - Mathf.Abs(Quaternion.Dot(quatA, value)) < acceptableRange;
    }
}
