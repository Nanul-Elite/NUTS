namespace NUTS.Database
{
    public class TargetData
    {
        public int Id { get; set; }

        public ulong MessageId { get; set; }
        public ulong ThreadId { get; set; }

        public string Guid { get; set; }
        public string CmdrName { get; set; }
        public string? ThumbUrl { get; set; }

        public int Status { get; set; }

        public string Reason { get; set; }
        public string? Sentence { get; set; }
        public string? Reward { get; set; }

        public string? Squad { get; set; }
        public string? Affiliation { get; set; }
        public string? Intel { get; set; }
        public string? InaraUrl { get; set; }
        public string? GankersUrl { get; set; }

        public List<ulong> KilledBy { get; set; }
    }
}
