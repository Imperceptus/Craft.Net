using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Craft.Net.Data.Blocks
{
    public class LockedChestBlock : Block
    {
        public override ushort Id
        {
            get { return 95; }
        }

        public override double Hardness
        {
            get { return -1; }
        }
    }
}
