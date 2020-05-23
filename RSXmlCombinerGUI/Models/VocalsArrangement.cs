namespace RSXmlCombinerGUI.Models
{
    public sealed class VocalsArrangement : Arrangement
    {
        public VocalsArrangement() { }

        public VocalsArrangement(string fileName, ArrangementType arrangementType)
        {
            FileName = fileName;
            ArrangementType = arrangementType;
        }

        public VocalsArrangement(ArrangementType arrangementType)
        {
            ArrangementType = arrangementType;
        }
    }
}
