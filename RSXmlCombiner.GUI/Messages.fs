[<AutoOpen>]
module RSXmlCombiner.FuncUI.Messages

type Msg =
    | ToneReplacementClosed
    | SetReplacementTone of trackIndex : int * arrIndex : int * toneName : string * replacementIndex : int
    | ProjectViewActiveChanged of bool
    | CombineAudioProgressChanged of float
    | CombineArrangementsProgressChanged of int
    | ErrorOccured of exn

    // Top controls
    | SelectAddTrackFiles
    | AddTrack of arrangementFiles : string[] option
    | SelectOpenProjectFile
    | OpenProject of projectPath : string option
    | ProjectOpened of project : Project.Dto * projectPath : string
    | SelectImportProject
    | ImportProject of projectPath : string option
    | ImportProjectLoaded of arrangements : Map<ArrangementType, string> * string * string
    | NewProject
    | SaveProject of fileName : string option
    | ProjectSaved of fileName : string
    | SelectSaveProjectFile
    | AddTemplate of arrType : ArrangementType * ordering : ArrangementOrdering option

    // Bottom controls
    | SelectCombinationTargetFolder
    | CombineAudioFiles of targetFile : string option
    | CombineArrangements of targetFolder :string option
    | UpdateCombinationTitle of newTitle : string
    | CoercePhrasesChanged of bool
    | OnePhrasePerTrackChanged of bool
    | AddTrackNamesChanged of bool
    | CombineAudioCompleted of message : string
    | CombineArrangementsCompleted of unit
    | CreatePreview of targetFile : string option
    | SelectTargetAudioFile of defaultFileName : string option * cmd : (string option -> Msg)

    // Track list
    | RemoveTrack of trackIndex : int
    | ChangeAudioFile of trackIndex : int
    | ChangeAudioFileResult of trackIndex : int * newFile : string option
    | SelectArrangementFile of trackIndex : int * arrIndex : int
    | ChangeArrangementFile of trackIndex : int * arrIndex : int * newFile : string option
    | ArrangementBaseToneChanged of trackIndex : int * arrIndex : int * toneIndex : int
    | RemoveArrangementFile of trackIndex : int * arrIndex : int
    | ShowReplacementToneEditor of trackIndex : int * arrIndex : int
    | TrimAmountChanged of trackIndex : int * trimAmount : double
    | RemoveTemplate of name : string

    // Common tone editor
    | UpdateToneName of arrName:string * index:int * newName:string
    | SelectedToneFromFileChanged of arrName:string * selectedTone:string
    | AddSelectedToneFromFile of arrName:string
