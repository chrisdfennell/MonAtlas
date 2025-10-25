namespace MonAtlas.Models
{
    public static class Types
    {
        public static readonly string[] All = new[] {
            "normal","fire","water","electric","grass","ice","fighting","poison","ground","flying",
            "psychic","bug","rock","ghost","dragon","dark","steel","fairy"
        };

        // Attacking -> Defending multiplier matrix (attacker rows, defender cols)
        // Source: standard type chart
        public static readonly double[,] Mult =
        {
            // nor, fir, wat, ele, gra, ice, fig, poi, gro, fly, psy, bug, roc, gho, dra, dar, ste, fai
            {1,   1,   1,   1,   1,   1,   1,  1,   1,  1,   1,  1,  0.5, 0,   1,   1,  0.5, 1}, // normal
            {1,  0.5, 0.5,  1,   2,   2,   1,  1,   1,  1,   1,  2,  0.5, 1,  0.5,  1,   2,  1}, // fire
            {1,   2,  0.5,  1,  0.5,  1,   1,  1,   2,  1,   1,  1,   2,  1,  0.5,  1,   1,  1}, // water
            {1,   1,   2,  0.5, 0.5,  1,   1,  1,   0,  2,   1,  1,   1,  1,  0.5,  1,   1,  1}, // electric
            {1,  0.5,  2,   1,  0.5,  1,   1,  0.5,  2,  0.5,  1,  0.5,  2,  1,  0.5,  1,  0.5, 1}, // grass
            {1,  0.5,  0.5,  1,   2,  0.5,  1,  1,   2,  2,   1,  1,   1,  1,   2,   1,  0.5, 1}, // ice
            {2,   1,   1,   1,   1,   2,   1,  0.5,  1,  0.5,  0.5, 0.5,  2,  0,   1,   2,   2,  0.5}, // fighting
            {1,   1,   1,   1,   2,   1,   1,  0.5,  0.5, 1,   1,  1,   0.5, 0.5, 1,   1,   0,   2}, // poison
            {1,   2,   1,   2,  0.5,  1,   1,   2,   1,  0,    1,  0.5,  2,  1,   1,   1,   2,  1}, // ground
            {1,   1,   1,  0.5,  2,   1,   2,   1,   1,  1,    1,  2,   0.5, 1,   1,   1,  0.5, 1}, // flying
            {1,   1,   1,   1,   1,   1,   2,   2,   1,  1,   0.5, 1,   1,  1,   1,   0,   0.5, 1}, // psychic
            {1,  0.5,  1,   1,   2,   1,  0.5, 0.5,  1,  0.5,  2,  1,   1,  0.5, 1,   2,   0.5, 0.5}, // bug
            {1,   2,   1,   1,   1,   2,   1,   1,   1,  1,    1,  1,   2,  1,   1,   1,   0.5, 1}, // rock
            {0,   1,   1,   1,   1,   1,   1,   1,   1,  1,    1,  1,   1,  2,   1,   1,   1,   1}, // ghost
            {1,   1,   1,   1,   1,   1,   1,   1,   1,  1,    1,  1,   1,  1,   2,   1,   0.5, 0}, // dragon
            {1,   1,   1,   1,   1,   1,   0.5, 1,   1,  1,    2,  1,   1,  2,   1,   0.5, 1,   0.5}, // dark
            {1,  0.5,  0.5,  0.5,  1,   2,   1,   1,   1,  1,   1,  1,   2,  1,   1,   1,   0.5, 2}, // steel
            {1,  0.5,  1,    1,   1,   1,   2,  0.5,  1,  1,   1,  1,   1,  1,   2,   2,   0.5, 1}  // fairy
        };

        public static int IndexOf(string type) => Array.IndexOf(All, type.ToLower());
    }
}