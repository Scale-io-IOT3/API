namespace Core.Interface.Foods;

public interface IMacroSource
{
    int Calories { get; }
    double Carbohydrates { get; }
    double Fat { get; }
    double Proteins { get; }
}