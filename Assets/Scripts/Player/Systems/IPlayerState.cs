namespace Mimeto.PlayerSystems
{
    /// <summary>
    /// Interface cho kiến trúc State Machine.
    /// Bất kỳ trạng thái nào (Đi bộ, Chạy, Nhảy) đều phải tuân thủ form này.
    /// </summary>
    public interface IPlayerState
    {
        void Enter();
        void UpdateState();
        void Exit();
    }
}
