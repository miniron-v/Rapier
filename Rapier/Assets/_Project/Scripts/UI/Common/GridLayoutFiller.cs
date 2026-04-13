using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// GridLayoutGroup 의 cellSize.x 를 부모 Rect 폭에 맞춰 자동 계산한다.
    /// ILayoutSelfController 를 구현하여 Unity 레이아웃 패스 시 호출된다.
    /// HorizontalLayoutGroup.childControlWidth 와 동일한 역할.
    /// </summary>
    [RequireComponent(typeof(GridLayoutGroup))]
    public class GridLayoutFiller : UIBehaviour, ILayoutSelfController
    {
        private GridLayoutGroup _grid;
        private RectTransform _rect;

        protected override void Awake()
        {
            base.Awake();
            _grid = GetComponent<GridLayoutGroup>();
            _rect = GetComponent<RectTransform>();
        }

        /// <summary>수평 레이아웃 패스: cellSize.x 를 열 수에 맞게 계산한다.</summary>
        public void SetLayoutHorizontal()
        {
            if (_grid == null || _rect == null) return;
            if (_grid.constraint != GridLayoutGroup.Constraint.FixedColumnCount) return;

            int cols = _grid.constraintCount;
            if (cols <= 0) return;

            float availableWidth = _rect.rect.width
                                   - _grid.padding.left
                                   - _grid.padding.right;
            float cellWidth = (availableWidth - _grid.spacing.x * (cols - 1)) / cols;
            if (cellWidth > 0f)
                _grid.cellSize = new Vector2(cellWidth, _grid.cellSize.y);
        }

        /// <summary>수직 레이아웃 패스: 처리 없음.</summary>
        public void SetLayoutVertical() { }
    }
}
