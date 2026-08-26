using UnityEngine;
using Platformer.Player;

namespace Platformer.Tests
{
    /// <summary>PlayMode 测试的最小场景构建工厂（不依赖场景资产）。</summary>
    internal static class PlayerTestScene
    {
        public static GameObject CreateGround(Vector3 pos, Vector2 size)
        {
            var go = new GameObject("Ground");
            go.transform.position = pos;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            go.AddComponent<SpriteRenderer>();
            var col = go.AddComponent<BoxCollider2D>();
            col.size = Vector2.one;
            return go;
        }

        public static GameObject CreatePlayer(Vector3 pos)
        {
            var go = new GameObject("Player");
            go.transform.position = pos;
            go.AddComponent<SpriteRenderer>();
            var col = go.AddComponent<BoxCollider2D>();
            col.size = Vector2.one;
            go.AddComponent<Rigidbody2D>();
            go.AddComponent<InputReader>();
            go.AddComponent<PlayerBody>();
            return go;
        }
    }
}
