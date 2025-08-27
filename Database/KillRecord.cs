namespace NUTS.Database
{
    public class KillRecord
    {
        public int Id { get; set; }
        public int TargetDataId { get; set; }
        public TargetData TargetData { get; set; }

        public string TargetGuid { get; set; }
        public ulong KillerUserId { get; set; }
        public DateTime Timestamp { get; set; }
    }
}