using System;
using Game.Core;
using Game.Data.Save;

namespace Game.Core.Services
{
    /// <summary>
    /// 재화 중앙 관리 서비스.
    /// Add/TryConsume 호출 시 SaveManager.Save() 후 이벤트 발화.
    /// </summary>
    public class CurrencyService
    {
        private SaveManager _saveManager;

        public event Action<int> OnGoldChanged;
        public event Action<int> OnGachaTicketChanged;
        public event Action<int> OnRuneGachaTicketChanged;
        public event Action<int> OnCrystalChanged;

        /// <summary>현재 Gold 보유량.</summary>
        public int Gold            => _saveManager.Current.gold;
        /// <summary>현재 장비 가챠 티켓 보유량.</summary>
        public int GachaTicket     => _saveManager.Current.gachaTicket;
        /// <summary>현재 룬 가챠 티켓 보유량.</summary>
        public int RuneGachaTicket => _saveManager.Current.runeGachaTicket;
        /// <summary>현재 Crystal(프리미엄 재화) 보유량.</summary>
        public int Crystal         => _saveManager.Current.crystal;

        /// <summary>SaveManager 주입 및 ServiceLocator 등록.</summary>
        public void Init(SaveManager saveManager)
        {
            _saveManager = saveManager;
            ServiceLocator.Register(this);
        }

        // ── Gold ──────────────────────────────────────────────────────────

        /// <summary>Gold를 amount만큼 추가하고 저장한다.</summary>
        public void AddGold(int amount)
        {
            _saveManager.Current.gold += amount;
            _saveManager.Save();
            OnGoldChanged?.Invoke(_saveManager.Current.gold);
        }

        /// <summary>Gold를 amount만큼 소모한다. 부족 시 false 반환.</summary>
        public bool TryConsumeGold(int amount)
        {
            if (_saveManager.Current.gold < amount) return false;
            _saveManager.Current.gold -= amount;
            _saveManager.Save();
            OnGoldChanged?.Invoke(_saveManager.Current.gold);
            return true;
        }

        // ── GachaTicket ───────────────────────────────────────────────────

        /// <summary>장비 가챠 티켓을 amount만큼 추가하고 저장한다.</summary>
        public void AddGachaTicket(int amount)
        {
            _saveManager.Current.gachaTicket += amount;
            _saveManager.Save();
            OnGachaTicketChanged?.Invoke(_saveManager.Current.gachaTicket);
        }

        /// <summary>장비 가챠 티켓을 amount만큼 소모한다. 부족 시 false 반환.</summary>
        public bool TryConsumeGachaTicket(int amount)
        {
            if (amount <= 0) return true;
            if (_saveManager.Current.gachaTicket < amount) return false;
            _saveManager.Current.gachaTicket -= amount;
            _saveManager.Save();
            OnGachaTicketChanged?.Invoke(_saveManager.Current.gachaTicket);
            return true;
        }

        // ── RuneGachaTicket ───────────────────────────────────────────────

        /// <summary>룬 가챠 티켓을 amount만큼 추가하고 저장한다.</summary>
        public void AddRuneGachaTicket(int amount)
        {
            _saveManager.Current.runeGachaTicket += amount;
            _saveManager.Save();
            OnRuneGachaTicketChanged?.Invoke(_saveManager.Current.runeGachaTicket);
        }

        /// <summary>룬 가챠 티켓을 amount만큼 소모한다. 부족 시 false 반환.</summary>
        public bool TryConsumeRuneGachaTicket(int amount)
        {
            if (amount <= 0) return true;
            if (_saveManager.Current.runeGachaTicket < amount) return false;
            _saveManager.Current.runeGachaTicket -= amount;
            _saveManager.Save();
            OnRuneGachaTicketChanged?.Invoke(_saveManager.Current.runeGachaTicket);
            return true;
        }

        // ── Crystal ───────────────────────────────────────────────────────

        /// <summary>Crystal을 amount만큼 추가하고 저장한다.</summary>
        public void AddCrystal(int amount)
        {
            _saveManager.Current.crystal += amount;
            _saveManager.Save();
            OnCrystalChanged?.Invoke(_saveManager.Current.crystal);
        }

        /// <summary>Crystal을 amount만큼 소모한다. 부족 시 false 반환.</summary>
        public bool TryConsumeCrystal(int amount)
        {
            if (amount <= 0) return true;
            if (_saveManager.Current.crystal < amount) return false;
            _saveManager.Current.crystal -= amount;
            _saveManager.Save();
            OnCrystalChanged?.Invoke(_saveManager.Current.crystal);
            return true;
        }
    }
}
