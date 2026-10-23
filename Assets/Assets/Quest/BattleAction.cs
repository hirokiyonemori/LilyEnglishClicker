using System;
using System.Collections.Generic;

/// <summary>行動の効果だけを定義する。ターン・HP・入力ロックはBattlePanelが共通処理する。</summary>
public abstract class BattleAction
{
    public abstract int MpCost { get; }
    public abstract BattleActionResult Resolve(int normalAttackPower);
}

public readonly struct BattleActionResult
{
    public readonly long Damage;
    public readonly int MpRecovery;
    public readonly bool Guard;
    public readonly string Message;
    public BattleActionResult(long damage, int mpRecovery, bool guard, string message)
    { Damage = damage; MpRecovery = mpRecovery; Guard = guard; Message = message; }
}

public abstract class BattleSpell : BattleAction
{
    public string Id { get; }
    public string Name { get; }
    public override int MpCost { get; }
    protected BattleSpell(string id, string name, int mpCost)
    { Id = id; Name = name; MpCost = Math.Max(0, mpCost); }
}

/// <summary>攻撃魔法。別種の効果はBattleSpellの実装を追加できる。</summary>
public sealed class DamageBattleSpell : BattleSpell
{
    public int Multiplier { get; }
    public DamageBattleSpell(string id, string name, int mpCost, int multiplier) : base(id, name, mpCost)
    { Multiplier = Math.Max(1, multiplier); }
    public override BattleActionResult Resolve(int power)
        => new BattleActionResult((long)power * Multiplier, 0, false, Name + "！");
}

public static class BattleActions
{
    public static readonly BattleAction Attack = new AttackAction();
    public static readonly BattleAction Defend = new DefendAction();
    public static readonly BattleSpell Fire = new DamageBattleSpell("fire", "ファイア", 2, 2);
    public static readonly IReadOnlyList<BattleSpell> Spells = Array.AsReadOnly(new[] { Fire });
    private sealed class AttackAction : BattleAction
    {
        public override int MpCost => 0;
        public override BattleActionResult Resolve(int power) => new BattleActionResult(power, 0, false, "リリィの攻撃！");
    }
    private sealed class DefendAction : BattleAction
    {
        public override int MpCost => 0;
        public override BattleActionResult Resolve(int power) => new BattleActionResult(0, 1, true, "リリィは身を守っている！");
    }
}
