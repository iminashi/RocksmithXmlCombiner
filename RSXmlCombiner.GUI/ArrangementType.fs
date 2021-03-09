namespace RSXmlCombiner.FuncUI

type ArrangementType = 
    | Unknown =    0b0000000
    | Lead =       0b0000001
    | Rhythm =     0b0000010
    | Combo =      0b0000100
    | Bass =       0b0001000
    | Vocals =     0b0010000
    | JVocals =    0b0100000
    | ShowLights = 0b1000000

module ArrangementType =
    open Rocksmith2014.XML

    let [<Literal>] private InstrumentalArrangement = ArrangementType.Lead ||| ArrangementType.Rhythm ||| ArrangementType.Combo ||| ArrangementType.Bass
    let [<Literal>] private VocalsArrangement = ArrangementType.Vocals ||| ArrangementType.JVocals
    let [<Literal>] private OtherArrangement = VocalsArrangement ||| ArrangementType.ShowLights

    /// Tests if the arrangement type is lead, rhythm, bass or combo.
    let isInstrumental arrType = (arrType &&& InstrumentalArrangement) <> ArrangementType.Unknown
    /// Tests if the arrangement type is vocals or j-vocals.
    let isVocals arrType = (arrType &&& VocalsArrangement) <> ArrangementType.Unknown
    /// Tests if the arrangement type is vocals, j-vocals or show lights.
    let isOther arrType = (arrType &&& OtherArrangement) <> ArrangementType.Unknown

    /// Active pattern for matching an instrumental arrangement.
    let (|Instrumental|_|) (arrType: ArrangementType) = Option.create isInstrumental arrType
    /// Active pattern for matching a vocals arrangement.
    let (|Vocals|_|) (arrType: ArrangementType) = Option.create isVocals arrType

    /// Creates an ArrangementType from an instrumental arrangement.
    let fromArrangement (arr: InstrumentalArrangement) =
        let props = arr.MetaData.ArrangementProperties
        if props.PathLead then ArrangementType.Lead
        elif props.PathRhythm then ArrangementType.Rhythm
        elif props.PathBass then ArrangementType.Bass
        else ArrangementType.Unknown

    /// Returns a humanized string matching the arrangement type.
    let humanize = function
        | ArrangementType.ShowLights -> "Show Lights"
        | ArrangementType.JVocals -> "J-Vocals"
        | arrType -> string arrType
