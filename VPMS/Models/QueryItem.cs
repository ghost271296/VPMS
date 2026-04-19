namespace VPMS.Models;

public enum QueryInputType { Boolean, Text, Numeric, Date, MultiChoice }

public class QueryItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Question { get; set; } = string.Empty;
    public string Rationale { get; set; } = string.Empty;
    public QueryInputType InputType { get; set; }
    public List<string> Choices { get; set; } = [];
    public string Unit { get; set; } = string.Empty;
    public string? Answer { get; set; }
    public bool IsAnswered => !string.IsNullOrWhiteSpace(Answer);
    public double InformationGain { get; set; }     // Higher = more diagnostic value
}
