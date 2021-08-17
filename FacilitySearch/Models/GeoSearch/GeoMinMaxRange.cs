using System;
using System.Collections.Generic;
using System.Text;

namespace ProviderSearch.Models
{
    class GeoRange
    {
        public double LatitudeMin { get; set; }
        public double LatitudeMax { get; set; }
        public double LongitudeMax { get; set; }
        public double LongitudeMin { get; set; }
    }
}
