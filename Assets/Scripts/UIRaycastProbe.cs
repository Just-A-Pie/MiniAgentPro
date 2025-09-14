using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UIRaycastProbe : MonoBehaviour
{
    public bool logEveryClick = false; // 置 true 则每次点击都打
    private PointerEventData _ped;
    private List<RaycastResult> _results = new();

    void Update()
    {
        if (EventSystem.current == null) return;

        if (logEveryClick || (Input.GetMouseButtonDown(0) && (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt))))
        {
            _results.Clear();
            if (_ped == null) _ped = new PointerEventData(EventSystem.current);
            _ped.position = Input.mousePosition;
            EventSystem.current.RaycastAll(_ped, _results);

            Debug.Log($"[UIProbe] hits={_results.Count} at {Input.mousePosition}");
            int n = Mathf.Min(10, _results.Count);
            for (int i = 0; i < n; i++)
            {
                var r = _results[i];
                var raycastTarget = (r.gameObject.TryGetComponent<Graphic>(out var g) ? g.raycastTarget : (bool?)null);
                Debug.Log($"  {i + 1}. {r.gameObject.name}  (raycastTarget={raycastTarget})  sortingOrder={r.sortingOrder}");
            }
        }
    }
}
