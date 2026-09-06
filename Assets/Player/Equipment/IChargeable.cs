namespace Player.Equipment
{
    public interface IChargeable
    {
        int CurrentCharge { get; }
        int MaxCharge { get => 100; }
        bool IsFullyCharged => CurrentCharge >= MaxCharge;

        /// <summary>
        /// Change charge amount (ONLY by server)
        /// </summary>
        void ModifyCharge(int amount);
    }
}