using UnityEngine;
using UnityEngine.EventSystems;

public sealed class DraggableSnapPanel : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IInitializePotentialDragHandler
{
    [SerializeField] private RectTransform dragTarget;
    [SerializeField, Min(0f)] private float edgePadding = 12f;
    [SerializeField, Min(0.01f)] private float snapDuration = 0.2f;

    private RectTransform parentRect;
    private Vector3 dragOffset;
    private bool isDragging;
    private Coroutine snapRoutine;

    private void Awake()
    {
        if (dragTarget == null)
        {
            dragTarget = GetComponent<RectTransform>();
        }

        parentRect = dragTarget.parent as RectTransform;

        if (parentRect == null)
        {
            Debug.LogWarning($"{nameof(DraggableSnapPanel)} on {name} requires a RectTransform parent.", this);
        }
    }

    public void OnInitializePotentialDrag(PointerEventData eventData)
    {
        eventData.useDragThreshold = false;
    }

    private void OnDisable()
    {
        if (snapRoutine != null)
        {
            StopCoroutine(snapRoutine);
            snapRoutine = null;
        }
        isDragging = false;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (snapRoutine != null)
        {
            StopCoroutine(snapRoutine);
            snapRoutine = null;
        }

        if (!TryGetParentRect(out RectTransform parent))
        {
            return;
        }

        if (!TryGetPointerWorldPoint(parent, eventData, out Vector3 worldPoint))
        {
            return;
        }

        isDragging = true;
        dragOffset = dragTarget.position - worldPoint;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging)
        {
            return;
        }

        if (!TryGetParentRect(out RectTransform parent))
        {
            return;
        }

        if (!TryGetPointerWorldPoint(parent, eventData, out Vector3 worldPoint))
        {
            return;
        }

        dragTarget.position = worldPoint + dragOffset;
        ClampInsideParent();
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!isDragging)
        {
            return;
        }

        isDragging = false;
        SnapToNearestEdge();
    }

    private void SnapToNearestEdge()
    {
        if (!TryGetParentRect(out RectTransform parent))
        {
            return;
        }

        Vector3[] parentCorners = new Vector3[4];
        Vector3[] panelCorners = new Vector3[4];
        parent.GetWorldCorners(parentCorners);
        dragTarget.GetWorldCorners(panelCorners);

        float parentMinX = parentCorners[0].x;
        float parentMaxX = parentCorners[2].x;
        float parentMinY = parentCorners[0].y;
        float parentMaxY = parentCorners[2].y;

        float panelMinX = panelCorners[0].x;
        float panelMaxX = panelCorners[2].x;
        float panelMinY = panelCorners[0].y;
        float panelMaxY = panelCorners[2].y;

        float leftDistance = Mathf.Abs(panelMinX - parentMinX);
        float rightDistance = Mathf.Abs(parentMaxX - panelMaxX);
        float bottomDistance = Mathf.Abs(panelMinY - parentMinY);
        float topDistance = Mathf.Abs(parentMaxY - panelMaxY);

        float width = panelMaxX - panelMinX;
        float height = panelMaxY - panelMinY;
        Vector2 pivot = dragTarget.pivot;

        Vector3 targetPos = dragTarget.position;

        if (leftDistance <= rightDistance && leftDistance <= topDistance && leftDistance <= bottomDistance)
        {
            float clampedMinY = ClampToBounds(panelMinY, parentMinY + edgePadding, parentMaxY - height - edgePadding);
            targetPos.x = parentMinX + edgePadding + pivot.x * width;
            targetPos.y = clampedMinY + pivot.y * height;
        }
        else if (rightDistance <= topDistance && rightDistance <= bottomDistance)
        {
            float clampedMinY = ClampToBounds(panelMinY, parentMinY + edgePadding, parentMaxY - height - edgePadding);
            targetPos.x = parentMaxX - edgePadding - (1f - pivot.x) * width;
            targetPos.y = clampedMinY + pivot.y * height;
        }
        else if (topDistance <= bottomDistance)
        {
            float clampedMinX = ClampToBounds(panelMinX, parentMinX + edgePadding, parentMaxX - width - edgePadding);
            targetPos.x = clampedMinX + pivot.x * width;
            targetPos.y = parentMaxY - edgePadding - (1f - pivot.y) * height;
        }
        else
        {
            float clampedMinX = ClampToBounds(panelMinX, parentMinX + edgePadding, parentMaxX - width - edgePadding);
            targetPos.x = clampedMinX + pivot.x * width;
            targetPos.y = parentMinY + edgePadding + pivot.y * height;
        }

        StartSnapAnimation(targetPos);
    }

    private void ClampInsideParent()
    {
        if (!TryGetParentRect(out RectTransform parent))
        {
            return;
        }

        Vector3[] parentCorners = new Vector3[4];
        Vector3[] panelCorners = new Vector3[4];
        parent.GetWorldCorners(parentCorners);
        dragTarget.GetWorldCorners(panelCorners);

        float parentMinX = parentCorners[0].x + edgePadding;
        float parentMaxX = parentCorners[2].x - edgePadding;
        float parentMinY = parentCorners[0].y + edgePadding;
        float parentMaxY = parentCorners[2].y - edgePadding;

        float width = panelCorners[2].x - panelCorners[0].x;
        float height = panelCorners[2].y - panelCorners[0].y;
        Vector2 pivot = dragTarget.pivot;

        float minX = ClampToBounds(panelCorners[0].x, parentMinX, parentMaxX - width);
        float minY = ClampToBounds(panelCorners[0].y, parentMinY, parentMaxY - height);

        SetWorldPosition(minX + pivot.x * width, minY + pivot.y * height);
    }

    private void SetWorldPosition(float worldX, float worldY)
    {
        Vector3 position = dragTarget.position;
        position.x = worldX;
        position.y = worldY;
        dragTarget.position = position;
    }

    private static float ClampToBounds(float value, float min, float max)
    {
        if (max < min)
        {
            return (min + max) * 0.5f;
        }

        return Mathf.Clamp(value, min, max);
    }

    private bool TryGetParentRect(out RectTransform parent)
    {
        parent = parentRect;
        return parent != null && dragTarget != null;
    }

    private static bool TryGetPointerWorldPoint(RectTransform target, PointerEventData eventData, out Vector3 worldPoint)
    {
        return RectTransformUtility.ScreenPointToWorldPointInRectangle(target, eventData.position, eventData.pressEventCamera, out worldPoint);
    }

    private void StartSnapAnimation(Vector3 targetPosition)
    {
        if (snapRoutine != null)
        {
            StopCoroutine(snapRoutine);
        }

        if (gameObject.activeInHierarchy)
        {
            snapRoutine = StartCoroutine(IESnapTo(targetPosition));
        }
        else
        {
            dragTarget.position = targetPosition;
        }
    }

    private System.Collections.IEnumerator IESnapTo(Vector3 targetPosition)
    {
        Vector3 startPosition = dragTarget.position;
        float elapsed = 0f;

        while (elapsed < snapDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / snapDuration;
            t = Mathf.SmoothStep(0f, 1f, t);

            dragTarget.position = Vector3.Lerp(startPosition, targetPosition, t);
            yield return null;
        }

        dragTarget.position = targetPosition;
        snapRoutine = null;
    }
}
