namespace RSXmlCombinerGUI.Models
{
    public sealed class ShowLightsArrangement : Arrangement
    {
        public ShowLightsArrangement()
        {
            ArrangementType = ArrangementType.ShowLights;
        }

        public ShowLightsArrangement(string fileName) : this()
        {
            FileName = fileName;
        }
    }
}
