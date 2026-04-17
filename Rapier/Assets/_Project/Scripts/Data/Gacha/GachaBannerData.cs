using System.Collections.Generic;
using UnityEngine;

namespace Game.Data.Gacha
{
    /// <summary>
    /// 가챠 배너 정의 ScriptableObject.
    /// 등급별 가중치, 아이템 풀, 비용 정보를 보유한다.
    /// SO 값은 런타임 불변. 외부에는 읽기 전용 프로퍼티로만 노출.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Data/Gacha/GachaBannerData")]
    public class GachaBannerData : ScriptableObject
    {
        [SerializeField] private string _bannerId = "standard_equipment";
        [SerializeField] private string _bannerName = "표준 장비 배너";
        [SerializeField] [TextArea] private string _description = "";
        [SerializeField] private Sprite _bannerArt;
        [SerializeField] private int _ticketCostPerPull = 1;
        [SerializeField] private int _crystalCostPerPull = 300;
        [SerializeField] [Range(0f, 1f)] private float _tenPullDiscount = 0.9f;
        [SerializeField] private GachaTicketType _ticketType;
        [SerializeField] private List<GachaGradeEntry> _gradeEntries = new();

        /// <summary>배너 고유 키 (예: "standard_equipment")</summary>
        public string BannerId          => _bannerId;
        /// <summary>배너 표시 이름</summary>
        public string BannerName        => _bannerName;
        /// <summary>배너 설명 텍스트</summary>
        public string Description       => _description;
        /// <summary>배너 이미지 (nullable)</summary>
        public Sprite BannerArt         => _bannerArt;
        /// <summary>1회 뽑기 티켓 비용</summary>
        public int TicketCostPerPull    => _ticketCostPerPull;
        /// <summary>1회 뽑기 Crystal 비용 (티켓 없을 때)</summary>
        public int CrystalCostPerPull   => _crystalCostPerPull;
        /// <summary>10회 할인율 (0~1. 예: 0.9 = 10% 할인)</summary>
        public float TenPullDiscount    => _tenPullDiscount;
        /// <summary>사용 티켓 종류</summary>
        public GachaTicketType TicketType => _ticketType;
        /// <summary>등급별 풀+가중치 목록</summary>
        public IReadOnlyList<GachaGradeEntry> GradeEntries => _gradeEntries;
    }
}
