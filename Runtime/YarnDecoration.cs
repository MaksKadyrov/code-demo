using EditorAttributes;
using R3;
using R3.Triggers;
using UGI.CoreUtils;
using UnityEngine;
using Random = UnityEngine.Random;

namespace UGI.Puzzle
{
    [RequireComponent(typeof(Rigidbody))]
    public class YarnDecoration : MonoBehaviour
    {
        [SerializeField] private Rigidbody _rigidbody;
        
        [Clamp(-1, 1, -1, 1, -1, 1), SerializeField] private Vector3 _direction = new(0, 1, 0);
        
        private void Awake()
        {
            _rigidbody.isKinematic = true;
        }

        private void OnDrawGizmosSelected()
        {
            GizmosUtil.DrawArrow(transform.position, transform.position + _direction, Color.blue);
        }

        private void Reset()
        {
            _direction = transform.forward;
            gameObject.GetOrInitField(ref _rigidbody).isKinematic = true;
            gameObject.GetOrAddComponent<MeshCollider>().convex = true;
        }
        
        [Button]
        public void Unpin(bool instant = false)
        {
            if (instant)
            {
                Disable();
                return;
            }
            
            var data = AnimationLibrary.DecorationData;
            
            _rigidbody.useGravity = true;
            _rigidbody.isKinematic = false;
            _rigidbody.AddRelativeForce(_direction * Random.Range(data.ForcePower.x, data.ForcePower.y));
            _rigidbody.AddRelativeTorque(_direction * Random.Range(data.TorquePower.x, data.TorquePower.y));

            if (Application.isPlaying)
            {
                gameObject.FixedUpdateAsObservable().Subscribe(this, (_, self) => self.FallRoutine()).AddTo(gameObject);
            }
        }

        public void SetDirection(Vector3 direction)
        {
            _direction = direction;
        }

        private void FallRoutine()
        {
            if (transform.position.y < -20)
            {
                Disable();
            }
        }

        public void Disable()
        {
            gameObject.SetActive(false);
        }
        
        [System.Serializable]
        public class Data
        {
            [SerializeField, MinMaxSlider(0, 1000)] public Vector2 ForcePower = new Vector2(100, 200);
            [SerializeField, MinMaxSlider(0, 1000)] public Vector2 TorquePower = new Vector2(100, 200);
        }
    }
}