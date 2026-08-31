using System;
using UnityEngine;

namespace Platformer.Levels
{
    /// <summary>
    /// 两层视差背景（设计文档 §9）：每层 = 精灵 + 滚动因子 + 高度 + 排序层。
    /// LateUpdate 按「相机 x × 滚动因子」对齐世界坐标网格摆放平铺精灵：
    /// 因子 0 = 无限远静止，1 = 完全跟随。层池在 Awake 一次性创建（缓存 Transform，零逐帧分配）。
    /// 超出视野的精灵由相机剔除，无绘制开销，池容量取固定上限即可。
    /// </summary>
    public sealed class ParallaxBackground : MonoBehaviour
    {
        [Serializable]
        public sealed class Layer
        {
            public Sprite sprite;
            [Range(0f, 1f)] public float scrollFactor = 0.5f;
            public float y = 0f;
            public int sortingOrder = 0;
        }

        [SerializeField] private Camera targetCamera;
        [SerializeField] private Layer[] layers = Array.Empty<Layer>();

        private const int PoolPerLayer = 8;
        private SpriteRenderer[][] _pools;

        /// <summary>生成器装配入口：一次性注入相机与层定义（Awake 消费）。</summary>
        public void Configure(Camera cam, Layer[] layerDefs)
        {
            targetCamera = cam;
            layers = layerDefs;
        }

        private void Awake()
        {
            if (layers == null) return;
            _pools = new SpriteRenderer[layers.Length][];
            for (int i = 0; i < layers.Length; i++)
            {
                if (layers[i] == null || layers[i].sprite == null)
                {
                    _pools[i] = Array.Empty<SpriteRenderer>();
                    continue;
                }

                _pools[i] = new SpriteRenderer[PoolPerLayer];
                for (int j = 0; j < PoolPerLayer; j++)
                {
                    var go = new GameObject($"Layer{i}_{j}");
                    go.transform.SetParent(transform, false);
                    var sr = go.AddComponent<SpriteRenderer>();
                    sr.sprite = layers[i].sprite;
                    sr.sortingOrder = layers[i].sortingOrder;
                    _pools[i][j] = sr;
                }
            }
        }

        private void LateUpdate()
        {
            if (_pools == null) return;
            if (targetCamera == null) targetCamera = Camera.main;
            if (targetCamera == null) return;

            float camX = targetCamera.transform.position.x;
            for (int i = 0; i < _pools.Length; i++)
            {
                var pool = _pools[i];
                if (pool.Length == 0) continue;
                Layer layer = layers[i];

                float tileW = layer.sprite.bounds.size.x;
                int baseIndex = Mathf.FloorToInt(camX * layer.scrollFactor / tileW);
                for (int j = 0; j < pool.Length; j++)
                {
                    var t = pool[j].transform;
                    t.position = new Vector3((baseIndex + j) * tileW, layer.y, 0f);
                }
            }
        }
    }
}
