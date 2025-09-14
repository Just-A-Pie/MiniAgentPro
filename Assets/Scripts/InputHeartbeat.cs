using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System;

public class InputHeartbeat : MonoBehaviour
{
    float _timer;
    GraphicRaycaster _anyRaycaster;

    void Start()
    {
        _anyRaycaster = FindObjectOfType<GraphicRaycaster>(true);
        Debug.Log("[HB] start");
    }

    void Update()
    {
        _timer += Time.unscaledDeltaTime;
        if (_timer >= 1f)
        {
            _timer = 0f;
            var es = EventSystem.current;
            var sel = es ? es.currentSelectedGameObject : null;
            var ray = _anyRaycaster ? _anyRaycaster.enabled : (bool?)null;
            Debug.Log($"[HB] timeScale={Time.timeScale}, ES={(es ? "OK" : "NULL")}, Selected={(sel ? sel.name : "none")}, " +
                      $"Raycaster={(ray.HasValue ? ray.Value.ToString() : "none")}, CursorLock={Cursor.lockState}, CursorVisible={Cursor.visible}, " +
                      $"Screen={Screen.width}x{Screen.height}");
        }
    }

    void OnApplicationFocus(bool focus)
    {
        Debug.Log($"[HB] OnApplicationFocus: {focus}");
    }
    void OnApplicationPause(bool pause)
    {
        Debug.Log($"[HB] OnApplicationPause: {pause}");
    }
}
