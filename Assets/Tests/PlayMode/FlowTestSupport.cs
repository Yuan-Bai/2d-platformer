using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Platformer.Tests
{
    /// <summary>
    /// 流程测试夹具支持（ADR-0009）：
    /// 夹具场景 TestLevelA/B（地面 + SpawnPoint + LevelConfig）**永久在 Build Settings**
    /// （LoadScene 依赖；PlayMode 测试进入 Play 后运行时场景列表已固化，运行期改 EditorBuildSettings 不生效）。
    /// LevelBuilder.BuildAll 合并保留非关卡场景，不会把它们清掉。
    ///   TestLevelA：SpawnPoint(10,-1.3)、樱桃 3；TestLevelB：SpawnPoint(20,-1.3)、樱桃 5。
    /// </summary>
    internal static class FlowTestSupport
    {
        public const string LevelA = "TestLevelA";
        public const string LevelB = "TestLevelB";

        /// <summary>轮询等待流程到达目标状态（Additive 加载在 Play 中异步激活，最多 maxFrames 帧）。</summary>
        public static IEnumerator WaitUntilState(GameFlowController flow, FlowState target, int maxFrames = 120)
        {
            for (int i = 0; i < maxFrames && flow.State != target; i++)
                yield return null;
        }

        /// <summary>
        /// 等若干物理步：_rb.position 直接赋值后 transform 同步发生在物理步，
        /// 切关传送断言前必须等物理步（否则读到传送前位置）。
        /// </summary>
        public static IEnumerator WaitPhysics(int frames = 3)
        {
            for (int i = 0; i < frames; i++) yield return new WaitForFixedUpdate();
        }

        /// <summary>卸载测试期间加载的夹具关卡场景（避免污染下一个测试）。</summary>
        public static void UnloadFixtureLevels()
        {
            foreach (var name in new[] { LevelA, LevelB })
            {
                var scene = SceneManager.GetSceneByName(name);
                if (scene.IsValid() && scene.isLoaded)
                    SceneManager.UnloadSceneAsync(name);
            }
        }
    }
}
