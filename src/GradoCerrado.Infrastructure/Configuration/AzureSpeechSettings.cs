using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GradoCerrado.Infrastructure.Configuration
{
    public class AzureSpeechSettings
    {
        public const string SectionName = "AzureSpeech";

        public string ApiKey { get; set; } = string.Empty;
        public string Region { get; set; } = "brazilsouth";
        public string DefaultVoice { get; set; } = "es-ES-ElviraNeural";
        public string AudioFormat { get; set; } = "mp3";
        public string SpeechRate { get; set; } = "medium";
        public string Pitch { get; set; } = "medium";
    }
}
