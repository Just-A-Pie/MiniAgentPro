using UnityEngine;
using UnityEngine.UI;
using System.Linq;

public class AgentAnimationController : MonoBehaviour
{
    // 指定行走图贴图的根名称
    public string spriteSheetName;

    private Sprite[] allFrames;               // 期望 12 帧
    private Image image;

    public enum Direction { Down, Left, Right, Up }
    private Direction currentDirection = Direction.Down;
    private int currentFrameCycle = 0;
    private float animationTimer = 0f;
    public float baseFrameDuration = 0.2f;   // 每帧时长

    // idle 帧索引
    private readonly int[] idleFrameIndices = { 1, 4, 7, 10 };

    private bool framesLoaded = false;

    /*────────────────── 关键改动从这里开始 ──────────────────*/
    private void Awake()
    {
        image = GetComponent<Image>();

        // 1️⃣ 取默认名
        if (string.IsNullOrEmpty(spriteSheetName))
        {
            spriteSheetName = gameObject.name.Replace("Agent_", "");
            Debug.LogWarning($"AgentAnimationController: spriteSheetName 未设置，使用 \"{spriteSheetName}\"");
        }

        // 2️⃣ 首次尝试
        TryLoadFrames(spriteSheetName);

        // 3️⃣ 若失败，再把空格 ↔ 下划线互换后再试一次
        if (!framesLoaded)
        {
            string alt = spriteSheetName.Contains("_")
                       ? spriteSheetName.Replace("_", " ")
                       : spriteSheetName.Replace(" ", "_");

            if (!alt.Equals(spriteSheetName))
                TryLoadFrames(alt);
        }
    }

    /// <summary>尝试加载并排序 12 帧行走图</summary>
    private void TryLoadFrames(string sheetName)
    {
        allFrames = Resources.LoadAll<Sprite>(sheetName);
        if (allFrames == null || allFrames.Length < 12)
        {
            Debug.LogError($"AgentAnimationController: 找不到或帧数不足 12 —— \"{sheetName}\"");
            framesLoaded = false;
            return;
        }

        // 以末尾数字排序：xxx_0 .. xxx_11
        allFrames = allFrames.OrderBy(s => ExtractFrameNumber(s.name)).ToArray();
        framesLoaded = true;
        spriteSheetName = sheetName;   // 记录实际成功名
    }
    /*────────────────── 其余代码保持原样 ───────────────────*/

    private int ExtractFrameNumber(string spriteName)
    {
        int idx = spriteName.LastIndexOf('_');
        if (idx >= 0 && idx < spriteName.Length - 1 &&
            int.TryParse(spriteName.Substring(idx + 1), out int num))
            return num;
        return 0;
    }

    public void UpdateAnimation(Direction direction, bool moving, float dt)
    {
        currentDirection = direction;
        if (!framesLoaded) return;

        if (!moving)
        {
            int idleIdx = idleFrameIndices[(int)currentDirection];
            if (allFrames.Length > idleIdx)
                image.sprite = allFrames[idleIdx];
            currentFrameCycle = 0;
            animationTimer = 0f;
            return;
        }

        animationTimer += dt;
        if (animationTimer >= baseFrameDuration)
        {
            animationTimer -= baseFrameDuration;
            currentFrameCycle = (currentFrameCycle + 1) % 3;
            int baseIdx = currentDirection switch
            {
                Direction.Down => 0,
                Direction.Left => 3,
                Direction.Right => 6,
                Direction.Up => 9,
                _ => 0
            };
            int frameIdx = baseIdx + currentFrameCycle;
            if (allFrames.Length > frameIdx)
                image.sprite = allFrames[frameIdx];
        }
    }
}