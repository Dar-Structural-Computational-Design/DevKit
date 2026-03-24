using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DevKit.Models
{
    public class LlmResponse
    {
        public string Text { get; set; }
        public int InputTokens { get; set; }
        public int OutputTokens { get; set; }
    }
}
