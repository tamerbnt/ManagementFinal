using System;
using System.Threading.Tasks;

namespace Management.Presentation.ViewModels.Base
{
    public interface IParameterReceiver
    {
        Task SetParameterAsync(object parameter);
    }
}
