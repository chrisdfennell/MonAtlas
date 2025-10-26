namespace MonAtlas.Models
{
    public class TeamSlot
    {
        public string Species { get; set; } = "";
        public string Nickname { get; set; } = "";
        public string Item { get; set; } = "";
        public string Ability { get; set; } = "";
        public string Nature { get; set; } = "Adamant";
        public int Level { get; set; } = 50;
        public bool Shiny { get; set; } = false;

        public int EV_HP, EV_Atk, EV_Def, EV_SpA, EV_SpD, EV_Spe;
        public int IV_HP = 31, IV_Atk = 31, IV_Def = 31, IV_SpA = 31, IV_SpD = 31, IV_Spe = 31;

        public string Move1 { get; set; } = "";
        public string Move2 { get; set; } = "";
        public string Move3 { get; set; } = "";
        public string Move4 { get; set; } = "";
    }
}