namespace BensEngineeringMetrics;

public interface IFieldMapping
{
    string Alias { get; set; }
    string Field { get; init; }
    string FlattenField { get; set; }
}
