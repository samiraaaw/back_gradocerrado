using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GradoCerrado.Application.Interfaces
{
    public interface ISpeechService
    {
        //Text-to-Speech
        Task<byte[]> TextToSpeechAsync(string text, string? voice = null);
        Task<byte[]> SsmlToSpeechAsync(string ssml);

        //Speech-to-Text
        Task<string> SpeechToTextAsync(byte[] audioData);

        //Utilidades
        Task<bool> TestConnectionAsync();
    }
}
