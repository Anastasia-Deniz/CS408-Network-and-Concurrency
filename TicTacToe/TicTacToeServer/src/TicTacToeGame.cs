using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TicTacToeServer
{
    public class TicTacToeGame
    {
        private Dictionary<int, string> board;
        public string Turn { get; private set; }

        public TicTacToeGame()
        {
            board = new Dictionary<int, string>();
            for (int i = 1; i <= 9; i++)
            {
                board[i] = i.ToString();
            }
            Turn = "X";
        }

        public (bool valid, (string status, string result, string opponentResult)) MakeMove(int cell, string symbol)
        {
            if (cell < 1 || cell > 9 || !board[cell].All(char.IsDigit))
                return (false, ("continue", null, null));

            board[cell] = symbol;

            if (CheckWin(symbol))
                return (true, ("end", "win", "loss"));
            if (CheckDraw())
                return (true, ("end", "draw", "draw"));

            Turn = Turn == "X" ? "O" : "X";
            return (true, ("continue", null, null));
        }

        private bool CheckWin(string symbol)
        {
            int[][] winCombinations = new int[][]
            {
                new int[] {1, 2, 3}, new int[] {4, 5, 6}, new int[] {7, 8, 9},
                new int[] {1, 4, 7}, new int[] {2, 5, 8}, new int[] {3, 6, 9},
                new int[] {1, 5, 9}, new int[] {3, 5, 7}
            };

            foreach (var combination in winCombinations)
            {
                if (combination.All(cell => board[cell] == symbol))
                    return true;
            }
            return false;
        }

        private bool CheckDraw()
        {
            return board.Values.All(cell => !cell.All(char.IsDigit));
        }

        public string GetBoardRepresentation()
        {
            StringBuilder boardRepr = new StringBuilder();
            for (int i = 1; i <= 9; i++)
            {
                boardRepr.Append(board[i]);
                if (i % 3 != 0)
                    boardRepr.Append("|");
                else if (i != 9)
                    boardRepr.AppendLine();
            }
            return boardRepr.ToString();
        }
    }
}