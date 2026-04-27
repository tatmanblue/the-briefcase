namespace Briefcase.Search;

public class SearchCacheEntry
{
    public Guid FileId { get; set; }
    public DateTime LastModifiedUtc { get; set; }
    public List<string> Words { get; set; } = [];
}
