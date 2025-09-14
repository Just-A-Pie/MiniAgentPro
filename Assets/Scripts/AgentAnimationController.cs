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

    // ★ 改：走路序列改为“乒乓”0-1-2-1，避免 2→0 的突变
    private static readonly int[] WalkSeq = { 0, 1, 2, 1 };
    private int walkPhase = 0;

    private float animationTimer = 0f;
    public float baseFrameDuration = 0.2f;   // 每帧时长

    // idle 帧索引（中间帧）
    private readonly int[] idleFrameIndices = { 1, 4, 7, 10 };

    // ★ 新增：idle 宽限时间（两段移动间隔 < 这个时间就不切 idle）
    [Tooltip("从移动变为静止后，等待这么久才切到 idle 帧（秒），避免段间闪一下。")]
    public float idleHoldDuration = 0.1f;
    private float idleHoldTimer = 0f;
    private bool wasMoving = false;

    private bool framesLoaded = false;

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

        int baseIdx = currentDirection switch
        {
            Direction.Down => 0,
            Direction.Left => 3,
            Direction.Right => 6,
            Direction.Up => 9,
            _ => 0
        };

        if (!moving)
        {
            // ★ idle 宽限：短暂停留先保持最后一帧走路图，不立刻切 idle
            idleHoldTimer += dt;
            if (wasMoving && idleHoldTimer < idleHoldDuration)
            {
                wasMoving = false; // 仍然算静止，但先不换图
                return;
            }

            int idleIdx = idleFrameIndices[(int)currentDirection];
            if (allFrames.Length > idleIdx)
                image.sprite = allFrames[idleIdx];

            // 重置
            walkPhase = 0;
            animationTimer = 0f;
            wasMoving = false;
            return;
        }

        // 从静止切到移动：立刻显示第一帧走路图（不等计时器）
        if (!wasMoving)
        {
            int firstIdx = baseIdx + WalkSeq[walkPhase]; // 当前相位的图，起始为 0
            if (allFrames.Length > firstIdx)
                image.sprite = allFrames[firstIdx];

            idleHoldTimer = 0f;
            animationTimer = 0f; // 从零开始计时下一帧
            wasMoving = true;
            // 注意：不 return，下面仍会累加 dt 并根据需要推进
        }

        // 累计时间，可能一次推进多帧（抗卡顿）
        animationTimer += dt;
        while (animationTimer >= baseFrameDuration)
        {
            animationTimer -= baseFrameDuration;
            walkPhase = (walkPhase + 1) % WalkSeq.Length;

            int frameIdx = baseIdx + WalkSeq[walkPhase];
            if (allFrames.Length > frameIdx)
                image.sprite = allFrames[frameIdx];
        }
    }
}
