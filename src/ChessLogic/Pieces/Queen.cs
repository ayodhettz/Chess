using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChessLogic
{
    public class Queen : Piece
    {
        public override PieceType Type => PieceType.Queen;
        public override Player Colour { get; }
        public Queen(Player colour)
        {
            Colour = colour;
        }
        public override Piece Copy()
        {
            return new Queen(Colour) { HasMoved = this.HasMoved };
        }
    }
}
