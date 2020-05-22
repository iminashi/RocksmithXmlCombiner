using System;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using RSXmlCombinerGUI.ViewModels;

namespace RSXmlCombinerGUI
{
    public class ViewLocator : IDataTemplate
    {
        public bool SupportsRecycling => false;

        public IControl Build(object data)
        {
            var name = data.GetType().FullName.Replace("ViewModel", "View");
            var type = Type.GetType(name);

            if (type != null && Activator.CreateInstance(type) is Control ctrl)
            {
                return ctrl;
            }
            else
            {
                return new TextBlock { Text = "Not Found: " + name };
            }
        }

        public bool Match(object data)
        {
            return data is ViewModelBase;
        }
    }
}