using System.Threading.Tasks;

namespace Game.Data.Save
{
    /// <summary>
    /// 로컬 전용 SaveSyncService 구현체.
    /// 서버 연동이 없는 현재 단계에서 기본값으로 사용된다.
    /// PullAsync / PushAsync 는 항상 Disabled 를 반환한다.
    /// </summary>
    public sealed class LocalOnlySaveSyncService : ISaveSyncService
    {
        /// <inheritdoc/>
        public bool IsEnabled => false;

        /// <inheritdoc/>
        public Task<SaveSyncResult> PullAsync(string userId)
            => Task.FromResult(SaveSyncResult.Disabled());

        /// <inheritdoc/>
        public Task<SaveSyncResult> PushAsync(string userId, SaveData data)
            => Task.FromResult(SaveSyncResult.Disabled());
    }
}
