using System;

namespace RSXmlCombinerGUI.Models
{
    [Flags]
    public enum ArrangementType
    {
        Unknown =     0b000000000,

        Lead =        0b000000001,
        Rhythm =      0b000000010,
        Combo =       0b000000100,
        Bass =        0b000001000,
        Instrumental = Lead | Rhythm | Combo | Bass,

        Alternative = 0b000010000,
        Bonus       = 0b000100000,

        Vocals =      0b001000000,
        JVocals =     0b010000000,
        VocalsArrangement = Vocals | JVocals,

        ShowLights =  0b100000000,
    }
}
