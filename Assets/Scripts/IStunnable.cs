// 被弾の原因。UI側で「何に当たったか」を表示するために使う。
public enum HitCause
{
    Shell,
    HomingShell,
    Banana,
    Lightning,
    Crash,  // スター/キラーで無敵中の相手に衝突された場合など
    FellOff // レインボーロード等、コース外に落下した場合
}

// シェルなどの攻撃アイテムを受けたときに一定時間操作不能にできることを示すインターフェース。
// PlayerScript / AICarController の両方が実装する。
public interface IStunnable
{
    void Stun(float duration, HitCause cause);
}
