using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WuwaModModifier.Model
{
    public class WuwaMods
    {
        public string CharacterName { get; set; } = "";
        public string Folder { get; set; } = "";
        public List<WuwaMod> Mods { get; set; } = new List<WuwaMod>();
    }

    public class WuwaMod
    {
        public string CharacterName { get; set; } = "";
        public string Id { get; set; } = "";
        public string FullPath { get; set; } = "";
        public string ModName { get; set; } = "";
    }
}
