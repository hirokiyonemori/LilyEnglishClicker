using System;

namespace Idle
{
    /// <summary>本編1ステージ分のHP。永続セーブには含めず、開始・再挑戦で回復する。</summary>
    public sealed class MainBattleHealth
    {
        public int MaxHp { get; private set; }
        public int Hp { get; private set; }
        public bool Active { get; private set; }
        public bool IsDefeated => MaxHp > 0 && Hp == 0;
        private float elapsed;

        public void Begin(int maxHp) { MaxHp = Math.Max(1, maxHp); Hp = MaxHp; Active = true; elapsed = 0; }
        public void Stop() { Active = false; elapsed = 0; }
        public void ResetAttackTimer() { elapsed = 0; }
        public bool Tick(float delta, bool canAttack, float interval, int damage)
        {
            if (!Active || !canAttack) { elapsed = 0; return false; }
            elapsed += Math.Max(0, delta);
            if (elapsed < Math.Max(.1f, interval)) return false;
            elapsed = 0; // 復帰や低フレームレート時にまとめて攻撃しない。
            Hp = Math.Max(0, Hp - Math.Max(1, damage));
            if (Hp == 0) Active = false;
            return true;
        }
    }
}
