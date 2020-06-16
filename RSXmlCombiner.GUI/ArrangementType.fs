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
    open Rocksmith2014Xml

    [<Literal>]
    let private instrumentalArrangement = ArrangementType.Lead ||| ArrangementType.Rhythm ||| ArrangementType.Combo ||| ArrangementType.Bass
    [<Literal>]
    let private vocalsArrangement = ArrangementType.Vocals ||| ArrangementType.JVocals
    [<Literal>]
    let private otherArrangement = vocalsArrangement ||| ArrangementType.ShowLights

    /// Tests if the arrangement type is lead, rhythm, bass or combo.
    let isInstrumental arrType = (arrType &&& instrumentalArrangement) <> ArrangementType.Unknown
    /// Tests if the arrangement type is vocals or j-vocals.
    let isVocals arrType = (arrType &&& vocalsArrangement) <> ArrangementType.Unknown
    /// Tests if the arrangement type is vocals, j-vocals or show lights.
    let isOther arrType = (arrType &&& otherArrangement) <> ArrangementType.Unknown

    /// Active pattern for matching an instrumental arrangement.
    let (|Instrumental|_|) (arrType : ArrangementType) = Option.create isInstrumental arrType
    /// Active pattern for matching a vocals arrangement.
    let (|Vocals|_|) (arrType : ArrangementType) = Option.create isVocals arrType

    /// Creates an ArrangementType from arrangement properties.
    let fromArrProperties (props : ArrangementProperties) =
        if props.PathLead = 1uy then ArrangementType.Lead
        elif props.PathRhythm = 1uy then ArrangementType.Rhythm
        elif props.PathBass = 1uy then ArrangementType.Bass
        else ArrangementType.Unknown

    let humanize arrType =
        match arrType with
        | ArrangementType.ShowLights -> "Show Lights"
        | ArrangementType.JVocals -> "J-Vocals"
        | _ -> string arrType