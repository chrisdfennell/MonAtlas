namespace MonAtlas.Models
{
    public class CounterRow
    {
        public string AttackingType { get; set; } = "";
        public double Multiplier { get; set; }

        public string Label => $"{AttackingType.ToUpper()}  x{Multiplier:0.##}";
    }
}