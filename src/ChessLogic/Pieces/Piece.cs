using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChessLogic
{
    //all pieces inherit from this class
    public abstract class Piece
    {
        public abstract PieceType Type { get; }
        public abstract Player Colour { get; }
        public bool HasMoved { get; set; } = false;
        public abstract Piece Copy();
    }
}
