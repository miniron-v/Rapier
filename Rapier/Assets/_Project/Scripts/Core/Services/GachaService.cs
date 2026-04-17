using System;
using System.Collections.Generic;
using UnityEngine;
using Game.Core;
using Game.Data.Equipment;
using Game.Data.Gacha;
using Game.Data.Save;

namespace Game.Core.Services
{
    /// <summary>
    /// 가챠 뽑기 핵심 서비스.
    /// CurrencyService, EquipmentManager, SaveManager에 의존한다.
    /// Pull/CalcCost 공개 API 제공. 천장·10연차 Rare 보장 포함.
    /// </summary>
    public class GachaService
    {
        private CurrencyService  _currencyService;
        private EquipmentManager _equipmentManager;
        private SaveManager      _saveManager;

        /// <summary>뽑기 완료 시 발화. GachaResult에 결과 정보 포함.</summary>
        public event Action<GachaResult> OnGachaCompleted;

        /// <summary>의존성 주입 및 ServiceLocator 등록.</summary>
        public void Init(CurrencyService currencyService, EquipmentManager equipmentManager, SaveManager saveManager)
        {
            _currencyService  = currencyService;
            _equipmentManager = equipmentManager;
            _saveManager      = saveManager;
            ServiceLocator.Register(this);
        }

        /// <summary>
        /// 배너에서 count회 뽑기를 수행한다.
        /// count는 1 또는 10만 유효. 티켓 우선 소모, 부족분 Crystal 보충.
        /// 10연차 Rare 보장 및 천장(Epic 40회, Unique 90회) 적용.
        /// </summary>
        public GachaResult Pull(GachaBannerData banner, int count)
        {
            if (banner == null)
                return GachaResult.Fail("배너 데이터 없음");

            if (count != 1 && count != 10)
                return GachaResult.Fail("count는 1 또는 10만 허용");

            // ── 비용 계산 ─────────────────────────────────────────────────────
            var (ticketCost, crystalCost) = CalcCost(banner, count);

            // 잔고 체크
            if (_currencyService.Crystal < crystalCost)
                return GachaResult.Fail("재화 부족");

            // ── 소비 ──────────────────────────────────────────────────────────
            if (ticketCost > 0)
                ConsumeTicket(banner.TicketType, ticketCost);
            if (crystalCost > 0)
            {
                if (!_currencyService.TryConsumeCrystal(crystalCost))
                    return GachaResult.Fail("재화 부족");
            }

            // ── 롤 ────────────────────────────────────────────────────────────
            var results = new List<EquipmentInstance>(count);
            var saveData = _saveManager.Current;

            for (int i = 0; i < count; i++)
            {
                var instance = RollOne(banner, saveData);
                if (instance != null)
                    results.Add(instance);
            }

            // ── 10연차 Rare 보장 ──────────────────────────────────────────────
            if (count == 10 && results.Count == 10)
            {
                bool hasRareOrAbove = false;
                foreach (var item in results)
                {
                    if (item.Grade >= EquipmentGrade.Rare)
                    {
                        hasRareOrAbove = true;
                        break;
                    }
                }

                if (!hasRareOrAbove)
                {
                    // 마지막 아이템을 Rare 풀에서 재롤
                    var rareEntry = FindGradeEntry(banner, EquipmentGrade.Rare);
                    if (rareEntry != null && rareEntry.Pool != null && rareEntry.Pool.Length > 0)
                    {
                        var rareItem = rareEntry.Pool[UnityEngine.Random.Range(0, rareEntry.Pool.Length)];
                        if (rareItem != null)
                            results[results.Count - 1] = new EquipmentInstance(rareItem);
                    }
                }
            }

            // ── 인벤토리 추가 ─────────────────────────────────────────────────
            foreach (var item in results)
                _equipmentManager.AddEquipmentToInventory(item);

            // ── 저장 ──────────────────────────────────────────────────────────
            _saveManager.Save();

            var gachaResult = new GachaResult(true, results, ticketCost, crystalCost, null);
            OnGachaCompleted?.Invoke(gachaResult);
            return gachaResult;
        }

        /// <summary>
        /// count회 뽑기에 소모될 (티켓 수, Crystal 수)를 계산한다. UI 표시용.
        /// </summary>
        public (int tickets, int crystals) CalcCost(GachaBannerData banner, int count)
        {
            if (banner == null) return (0, 0);

            int currentTicket = GetCurrentTicket(banner.TicketType);
            int usableTickets = Mathf.Min(currentTicket, count);
            int deficit       = count - usableTickets;
            int crystalCost   = deficit * banner.CrystalCostPerPull;

            return (usableTickets, crystalCost);
        }

        // ── 내부 헬퍼 ─────────────────────────────────────────────────────────

        private EquipmentInstance RollOne(GachaBannerData banner, SaveData saveData)
        {
            // 천장 체크
            EquipmentGrade? forcedGrade = null;
            if (saveData.uniquePityCounter >= 90)
                forcedGrade = EquipmentGrade.Unique;
            else if (saveData.epicPityCounter >= 40)
                forcedGrade = EquipmentGrade.Epic;

            EquipmentGrade rolledGrade;
            if (forcedGrade.HasValue)
            {
                rolledGrade = forcedGrade.Value;
            }
            else
            {
                rolledGrade = RollGrade(banner);
            }

            // 카운터 갱신
            if (rolledGrade == EquipmentGrade.Unique)
            {
                saveData.uniquePityCounter = 0;
                saveData.epicPityCounter   = 0;
            }
            else if (rolledGrade == EquipmentGrade.Epic)
            {
                saveData.epicPityCounter = 0;
                saveData.uniquePityCounter++;
            }
            else
            {
                saveData.epicPityCounter++;
                saveData.uniquePityCounter++;
            }

            // 해당 등급 풀에서 아이템 선택
            var gradeEntry = FindGradeEntry(banner, rolledGrade);
            if (gradeEntry == null || gradeEntry.Pool == null || gradeEntry.Pool.Length == 0)
            {
                Debug.LogWarning($"[GachaService] 등급 {rolledGrade}에 대한 풀 없음. 폴백.");
                return null;
            }

            var selectedItem = gradeEntry.Pool[UnityEngine.Random.Range(0, gradeEntry.Pool.Length)];
            if (selectedItem == null)
            {
                Debug.LogWarning($"[GachaService] 선택된 아이템이 null.");
                return null;
            }

            return new EquipmentInstance(selectedItem);
        }

        private EquipmentGrade RollGrade(GachaBannerData banner)
        {
            // 가중치 정규화
            float totalWeight = 0f;
            foreach (var entry in banner.GradeEntries)
                totalWeight += entry.Weight;

            if (totalWeight <= 0f)
                return EquipmentGrade.Normal;

            float roll = UnityEngine.Random.value * totalWeight;
            float cumulative = 0f;

            foreach (var entry in banner.GradeEntries)
            {
                cumulative += entry.Weight;
                if (roll <= cumulative)
                    return entry.Grade;
            }

            // 부동소수점 오차 대비 마지막 등급 반환
            return banner.GradeEntries[banner.GradeEntries.Count - 1].Grade;
        }

        private GachaGradeEntry FindGradeEntry(GachaBannerData banner, EquipmentGrade grade)
        {
            foreach (var entry in banner.GradeEntries)
            {
                if (entry.Grade == grade)
                    return entry;
            }
            return null;
        }

        private int GetCurrentTicket(GachaTicketType ticketType)
        {
            return ticketType == GachaTicketType.Equipment
                ? _currencyService.GachaTicket
                : _currencyService.RuneGachaTicket;
        }

        private void ConsumeTicket(GachaTicketType ticketType, int amount)
        {
            if (ticketType == GachaTicketType.Equipment)
                _currencyService.TryConsumeGachaTicket(amount);
            else
                _currencyService.TryConsumeRuneGachaTicket(amount);
        }
    }
}
