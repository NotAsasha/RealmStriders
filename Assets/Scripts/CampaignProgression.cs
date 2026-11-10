namespace Game
{
    public static class CampaignProgression
    {
        public const int FinalMissionRating = 15;
        public const string FinalMissionName = "End";

        public static bool RequiresFinalMission(int rating, bool completed)
        {
            return rating == FinalMissionRating && !completed;
        }

        public static bool IsMissionSuccessful(int targetCount, int capturedCount, bool returnedToBase)
        {
            return targetCount > 0 && capturedCount == targetCount && returnedToBase;
        }

        public static int CalculateRating(int current, bool successful, bool teamWiped)
        {
            if (teamWiped) return System.Math.Max(0, current - 1);
            return successful ? current + 1 : current;
        }
    }
}
