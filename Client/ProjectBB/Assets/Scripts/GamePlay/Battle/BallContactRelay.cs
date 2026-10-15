using UnityEngine;

namespace ProjectBB.GamePlay
{
    /// <summary>随物理节点创建和销毁；回调仅复制实体接触标识，不结算、不回收、不模拟。</summary>
    public sealed class BallContactRelay : MonoBehaviour
    {
        private BallCollisionSystem _owner;
        private int _entityId;
        private int _wall;
        internal void Initialize(BallCollisionSystem owner, int entityId, int wall)
        {
            _owner = owner; _entityId = entityId; _wall = wall;
        }
        private void OnCollisionEnter2D(Collision2D collision) { Collect(collision); }
        private void OnCollisionStay2D(Collision2D collision) { Collect(collision); }
        private void Collect(Collision2D collision)
        {
            if (_entityId == 0 || _owner == null) return;
            BallContactRelay other = collision.rigidbody != null
                ? collision.rigidbody.GetComponent<BallContactRelay>() : collision.collider.GetComponent<BallContactRelay>();
            if (other == null || other._owner != _owner) return;
            Vector2 point = collision.contactCount > 0 ? collision.GetContact(0).point : (Vector2)transform.position;
            _owner.RecordContact(_entityId, other._entityId, other._wall, BallMotor.FromUnity(point));
        }
    }
}
