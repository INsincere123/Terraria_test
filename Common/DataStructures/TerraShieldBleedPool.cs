using System;

namespace TestMod.Common.DataStructures
{
    /// <summary>未释放的流血与已释放但不足一点的扣血余量分开维护，避免尾数遗失。</summary>
    public sealed class TerraShieldBleedPool
    {
        public double Buffer { get; private set; }
        public double Partial { get; private set; }
        public double Remaining => Buffer + Partial;

        public void Restore(double buffer, double partial)
        {
            Buffer = double.IsFinite(buffer) && buffer >= 0 ? Math.Min(buffer, int.MaxValue) : 0;
            Partial = double.IsFinite(partial) && partial >= 0 && partial < 1 ? partial : 0;
        }

        public void Add(int damage) => Buffer = Math.Min(int.MaxValue, Buffer + Math.Max(0, damage));

        public void Clear(double amount)
        {
            if (!double.IsFinite(amount) || amount <= 0) return;
            double fromBuffer = Math.Min(Buffer, amount);
            Buffer -= fromBuffer;
            Partial = Math.Max(0, Partial - (amount - fromBuffer));
        }

        public int Tick()
        {
            if (Buffer <= 5) return Flush();
            double released = Buffer / 120d;
            Buffer -= released;
            Partial += released;
            int damage = (int)Partial;
            Partial -= damage;
            return damage;
        }

        public int Flush()
        {
            // 每次伤害只有一个最终向上取整，不能丢弃已释放的小数。
            int damage = (int)Math.Min(int.MaxValue, Math.Ceiling(Remaining - 1e-9));
            Reset();
            return damage;
        }

        public void Reset() => Buffer = Partial = 0;
    }
}
