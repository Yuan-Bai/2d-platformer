using UnityEngine;

namespace Platformer.Mechanics
{
    /// <summary>
    /// 弹簧视觉（M3 机关外观）：订阅 <see cref="Bumper.Bounced"/> 做"压缩-回弹"程序化动画。
    /// 动画作用在子物体 "Visual"（或其上的第一个 SpriteRenderer）上——绝不能缩放根物体，
    /// 否则会连带缩放触发器碰撞体，缩小时触发"重进入"产生二次弹起。
    /// Sunny Land 无弹簧素材，视觉由生成器注入 block 类精灵 + 黄色 tint（设计文档 §8 待定项）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BumperVisual : MonoBehaviour
    {
        [SerializeField] private Sprite defaultSprite;
        [SerializeField] private Color tint = new Color(1f, 0.85f, 0.3f);
        [SerializeField] private float squashAmount = 0.35f;
        [SerializeField] private float recoverSpeed = 6f;

        private Bumper _bumper;
        private Transform _visual;
        private Vector3 _baseScale; // 视觉子物体基础缩放（生成器可预调），动画在此之上压缩-回弹
        private float _squash; // 1 = 刚触发，向 0 衰减

        private void Awake()
        {
            _bumper = GetComponent<Bumper>();
            if (_bumper != null) _bumper.Bounced += OnBounced;

            // 查找或创建视觉子物体（优先复用生成器已建好的 "Visual"）
            _visual = transform.Find("Visual");
            if (_visual == null)
            {
                _visual = new GameObject("Visual").transform;
                _visual.SetParent(transform, false);
            }

            var sr = _visual.GetComponent<SpriteRenderer>();
            if (sr == null) sr = _visual.gameObject.AddComponent<SpriteRenderer>();
            if (sr.sprite == null && defaultSprite != null) sr.sprite = defaultSprite;
            sr.color = tint;

            _baseScale = _visual.localScale;
        }

        private void OnDestroy()
        {
            if (_bumper != null) _bumper.Bounced -= OnBounced;
        }

        private void OnBounced() => _squash = 1f;

        private void Update()
        {
            if (_visual == null) return;
            _squash = Mathf.MoveTowards(_squash, 0f, recoverSpeed * Time.deltaTime);
            float y = 1f - squashAmount * _squash;
            float x = 1f + squashAmount * 0.5f * _squash; // 体积感：压扁时横向略胀
            _visual.localScale = new Vector3(_baseScale.x * x, _baseScale.y * y, _baseScale.z);
        }
    }
}
