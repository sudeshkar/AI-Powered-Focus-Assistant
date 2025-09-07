using FocusAssistant.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FocusAssistant.Services.Interfaces
{
    public interface IMLDataProcessor
    {
        Task<List<MLTrainingData>> PrepareMLDataAsync(IEnumerable<WorkSession> sessions);
    }
}
