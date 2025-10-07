using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GradoCerrado.Application.DTOs
{
    public class TtsRequest
    {
        public string Text { get; set; } = string.Empty;
        public string? Voice { get; set; }
        public string Rate { get; set; } = "medium";
        public string Pitch { get; set; } = "medium";
        public bool UseSSML { get; set; } = false;
    }
}
