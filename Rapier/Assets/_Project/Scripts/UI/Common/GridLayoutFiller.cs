using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// GridLayoutGroup 의 spacing.x 를 부모 Rect 폭에 맞춰 자동 계산한다.
    /// cellSize 는 변경하지 않아 비율을 유지한다.
    /// ILayoutSelfController 를 구현하여 Unity 레이아웃 패스 시 호출된다.
    /// </summary>
    [RequireComponent(typeof(GridLayoutGroup))]
    public class GridLayoutFiller : MonoBehaviour, ILayoutSelfController
    {
        private GridLayoutGroup _grid;
        private RectTransform _rect;

        private void Awake()
        {
            _grid = GetComponent<GridLayoutGroup>();
            _rect = GetComponent<RectTransform>();
        }

        /// <summary>수평 레이아웃 패스: spacing.x 를 열 수에 맞게 계산한다.</summary>
        public void SetLayoutHorizontal()
        {
            if (_grid == null || _rect == null) return;
            if (_grid.constraint != GridLayoutGroup.Constraint.FixedColumnCount) return;

            int cols = _grid.constraintCount;
            if (cols <= 1) return;

            float available = _rect.rect.width - _grid.padding.left - _grid.padding.right;
            float totalCellWidth = _grid.cellSize.x * cols;
            float spacingX = (available - totalCellWidth) / (cols - 1);
            if (spacingX >= 0f)
                _grid.spacing = new Vector2(spacingX, spacingX);
        }

        /// <summary>수직 레이아웃 패스: 처리 없음.</summary>
        public void SetLayoutVertical() { }
    }
}
