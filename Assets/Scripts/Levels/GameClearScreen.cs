using UnityEngine;
using UnityEngine.UI;

namespace Platformer.Levels
{
    /// <summary>
    /// 通关占位画面（ADR-0007）：显示跨场景樱桃累计。M4 起被正式通关画面/主菜单链取代。
    /// 文本用 legacy Text（系统字体回退可渲染中文）。
    /// </summary>
    public sealed class GameClearScreen : MonoBehaviour
    {
        [SerializeField] private Text label;
        [SerializeField] private int totalAcrossGame = 27;
        [SerializeField] private string format = "恭喜通关！\n樱桃累计 {0}/{1}";

        private void Start()
        {
            if (label != null)
                label.text = string.Format(format, LevelManager.TotalCollected, totalAcrossGame);
        }
    }
}
