using System.Collections.Generic;
using System.Net.Mime;
using Microsoft.Xna.Framework;

namespace TetrisClone
{
    public static class Block
    {
        public static readonly Dictionary<string, int> Colors = new Dictionary<string, int>()
        { 
            // Format of color:sprite number starting at index 0
            // -1 is just nothing/placeholder
            {"none", -1},
            {"gray", 0},
            {"red", 1},
            {"orange", 2},
            {"yellow", 3},
            {"green", 4},
            {"lightBlue", 5},
            {"blue", 6},
            {"magenta", 7},
        };

    }
}