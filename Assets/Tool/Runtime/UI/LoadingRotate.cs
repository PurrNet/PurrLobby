using UnityEngine;

namespace PurrLobby
{
    public class LoadingRotate : MonoBehaviour
    {
        private Transform _transform;

        [SerializeField] private  AnimationCurve _curve;
        [SerializeField] private float _rotationSpeed = 1f;

        private void Awake()
        {
            _transform = transform;
        }

        private void Update()
        {
            float rotationAmount = _rotationSpeed * Time.deltaTime;
            float curveValue = _curve.Evaluate(Time.time * _rotationSpeed % 1f);
            _transform.Rotate(0, 0, rotationAmount * curveValue);
        }
    }
}
