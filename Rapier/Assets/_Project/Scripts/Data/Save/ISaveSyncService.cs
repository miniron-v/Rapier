using System.Threading.Tasks;

namespace Game.Data.Save
{
    /// <summary>
    /// 저장 동기화 상태 코드.
    /// </summary>
    public enum SaveSyncStatus
    {
        /// <summary>동기화 비활성(로컬 전용 모드).</summary>
        Disabled,
        /// <summary>동기화 성공.</summary>
        Success,
        /// <summary>서버와 로컬 데이터 충돌.</summary>
        Conflict,
        /// <summary>네트워크 오류 등 실패.</summary>
        Failure
    }

    /// <summary>
    /// 저장 동기화 결과값 구조체.
    /// </summary>
    public readonly struct SaveSyncResult
    {
        /// <summary>동기화 상태 코드.</summary>
        public readonly SaveSyncStatus Status;
        /// <summary>부가 메시지 (오류 원인, 빈 문자열 허용).</summary>
        public readonly string Message;

        public SaveSyncResult(SaveSyncStatus status, string message = "")
        {
            Status  = status;
            Message = message ?? "";
        }

        /// <summary>Disabled 결과를 생성한다.</summary>
        public static SaveSyncResult Disabled() => new SaveSyncResult(SaveSyncStatus.Disabled);
    }

    /// <summary>
    /// 로컬/서버 저장 동기화 추상 인터페이스.
    /// 현재 구현체: <see cref="LocalOnlySaveSyncService"/> (항상 Disabled).
    /// 서버 연동 시 이 인터페이스를 구현하는 새 클래스를 추가하고 SaveManager 에 주입한다.
    /// </summary>
    public interface ISaveSyncService
    {
        /// <summary>동기화가 활성화된 경우 true.</summary>
        bool IsEnabled { get; }

        /// <summary>
        /// 서버에서 최신 SaveData 를 가져온다.
        /// </summary>
        /// <param name="userId">계정 식별자. 미연동 시 빈 문자열.</param>
        Task<SaveSyncResult> PullAsync(string userId);

        /// <summary>
        /// 로컬 SaveData 를 서버로 업로드한다.
        /// </summary>
        /// <param name="userId">계정 식별자. 미연동 시 빈 문자열.</param>
        /// <param name="data">업로드할 데이터.</param>
        Task<SaveSyncResult> PushAsync(string userId, SaveData data);
    }
}
