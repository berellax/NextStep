using System;
using System.Collections.Generic;
using System.Text;

namespace ProviderSearch.Models
{
    internal class ResidentialProfile
    {
        public Dictionary<string, bool> ProfileOptions { get; set; }
        public BaseAmount BaseAmount { get; set; }
    }

    internal enum BaseAmount
    {
        Under1K = 298680000,
        Between1KAnd2K = 298680001,
        Between2KAnd3K = 298680002,
        Between3KAnd4K = 298680003,
        Between4KAnd5K = 298680004,
        Over5K = 298680005
    }
}
