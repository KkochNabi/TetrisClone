#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Mime;
using System.Net.NetworkInformation;
using System.Numerics;
using System.Reflection.Emit;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using MonoGame.Extended.Sprites;
using Vector2 = Microsoft.Xna.Framework.Vector2;

namespace TetrisClone
{
    public class Board
    {
        private static readonly string[] Pieces = {"i", "j", "l", "o", "s", "z", "t"};
        // Block types are as follows: -1 = empty space, 0 = placed block, 1 = current piece, 2 = shadow of current piece
        private (int blockColor, int blockType)[,] _matrix;
        private GameMode _gamemode;
        public int ObjectiveScore;
        public float ObjectiveTarget;
        private Piece _currentPiece;
        // Position tuples mark the bottom-leftmost place of the matrix as 0,0
        private Position _currentPosition;
        private List<Position> _nonPlacedPositions;
        private Piece _holdQueue;
        private Piece[] _nextQueue;
        private bool _justHeld;
        private Random _random;
        private List<string?> _pieceBag;
        private float _gravity;
        public bool ToppedOut;
        private float _leftCount;
        private float _rightCount;
        private float _gravityCount;
        private float _dropCount;
        private float _lockTimeCount;
        private int _lockMoveCount;
        private bool _locking;
        private float _delayedAutoShift;
        private float _autoRepeatRate;
        private float _softDropRate;
        private float _lockTimeThreshold;
        private int _lockMoveThreshold;
        private static Dictionary<int, Position[]> _generalOffsets;
        private static Dictionary<int, Position[]> _iOffsets;
        public Piece CurrentPiece => _currentPiece;

        // TODO: Implement settings to make this relevant
        public float Gravity
        {
            get => _gravity;
            set => _gravity = (1 / 60f) / (value);
        }
        
        public float DelayedAutoShift
        {
            get => _delayedAutoShift;
            set => _delayedAutoShift = value / 60f;
        }

        public float AutoRepeatRate
        {
            get => _autoRepeatRate;
            set => _autoRepeatRate = value / 60f;
        }

        public float SoftDropRate
        {
            get => _softDropRate;
            set => _softDropRate = value / 60f;
        }

        public enum GameMode
        {
            ScoreAttack,
            LineClear,
            Cheese
        }

        /// <summary>
        /// Basic container for position, using rows and columns
        /// </summary>
        private struct Position
        {
            public int Row;
            public int Column;
            public Position(int row, int column)
            {
                Row = row;
                Column = column;
            }

            public void Deconstruct(out int row, out int column)
            {
                row = Row;
                column = Column;
            }
        }

        /// <summary>
        /// Initialize a board
        /// </summary>
        /// <param name="gameMode">The gamemode of the board</param>
        /// <param name="secondsToComplete">The time constraint of the game (score attack mode only)</param>
        /// <param name="linesToClear">The goal amount of lines to clear (line clear mode only)</param>
        /// <param name="cheeseLines">The amount of cheese lines spawned (cheese mode only)</param>
        public Board(GameMode gameMode, int secondsToComplete = 0, int linesToClear = 0, int cheeseLines = 10)
        {
            _matrix = new (int, int)[40, 10]; // There are 20 rows above the play field as a buffer
            _gamemode = gameMode;
            ObjectiveScore = 0;
            _currentPiece = new Piece();
            _currentPosition = new Position();
            _holdQueue = new Piece();
            _nextQueue = new Piece[6];
            _justHeld = false;
            _random = new Random();
            _pieceBag = new List<string?>();
            _nonPlacedPositions = new List<Position>();
            _gravity = (1/60f)/(1/64f);
            _leftCount = 0;
            _rightCount = 0;
            _gravityCount = 0;
            _dropCount = 0;
            _lockTimeCount = 0;
            _lockMoveCount = 0;
            _locking = false;
            _delayedAutoShift = 8/60f;
            _autoRepeatRate = 1/60f;
            _softDropRate = 3/60f;
            _lockTimeThreshold = 0.5f;
            _lockMoveThreshold = 15;
            // The following offsets use the position structure, but the rows and columns are x and y, respectively
            // Based off the offset data used in SRS rotation in modern guideline Tetris games
            _generalOffsets = new Dictionary<int, Position[]>
            {
                {
                    0,
                    new[]
                    {
                        new Position(0, 0), new Position(0, 0), new Position(0, 0), new Position(0, 0),
                        new Position(0, 0)
                    }
                },
                {
                    90,
                    new[]
                    {
                        new Position(0, 0), new Position(1, 0), new Position(1, -1), new Position(0, 2),
                        new Position(1, 2)
                    }
                },
                {
                    180,
                    new[]
                    {
                        new Position(0, 0), new Position(0, 0), new Position(0, 0), new Position(0, 0),
                        new Position(0, 0)
                    }
                },
                {
                    -90,
                    new[]
                    {
                        new Position(0, 0), new Position(-1, 0), new Position(-1, -1), new Position(0, 2),
                        new Position(-1, 2)
                    }
                }
            };
            _iOffsets =
                new
                    Dictionary<int, Position[]>
                    {
                        {
                            0,
                            new[]
                            {
                                new Position(0, 0), new Position(-1, 0), new Position(2, 0), new Position(-1, 0),
                                new Position(2, 0)
                            }
                        },
                        {
                            90,
                            new[]
                            {
                                new Position(-1, 0), new Position(0, 0), new Position(0, 0), new Position(0, 1),
                                new Position(0, -2)
                            }
                        },
                        {
                            180,
                            new[]
                            {
                                new Position(-1, 1), new Position(1, 1), new Position(-2, 1), new Position(1, 0),
                                new Position(-2, 0)
                            }
                        },
                        {
                            -90,
                            new[]
                            {
                                new Position(0, 1), new Position(0, 1), new Position(0, 1), new Position(0, -1),
                                new Position(0, 2)
                            }
                        }
                    };

            // Make matrix completely blank space
            for (var row = 0; row < 40; row++)
            {
                for (var column = 0; column < 10; column++)
                {
                    _matrix[row, column] = (-1, -1);
                }
            }
            
            // Generate random pieces for queue
            for (var i = 0; i < _nextQueue.Length; i++)
            {
                _nextQueue[i] = new Piece(GeneratePiece());
            }
            
            // Set the current piece
            NextPiece();
            ResetPosition();
            
            // Gamemode specific setup
            switch (gameMode)
            {
                case GameMode.ScoreAttack:
                    // Gamemode not developed
                    ObjectiveTarget = secondsToComplete;
                    break;
                case GameMode.LineClear:
                    // Gamemode not developed
                    ObjectiveTarget = linesToClear;
                    break;
                case GameMode.Cheese:
                    ObjectiveTarget = cheeseLines;
                    // Initial board setup
                    for (var i = 0; i < cheeseLines; i++)
                    {
                        GenerateGarbage();
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(gameMode), gameMode, null);
            }
        }
        
        /// <summary>
        /// Allows the current user-controlled piece to render on the board, along with its' shadow
        /// </summary>
        private void RenderCurrentPiece()
        {
            // Clear current piece and its shadow
            foreach (var (row, column) in _nonPlacedPositions)
            {
                _matrix[row, column] = (-1, -1);
            }
            _nonPlacedPositions.Clear();

            // The row for the shadow of the current piece
            var shadowRow = 39 - LowestFreeSpace(_currentPiece, _currentPosition);
            
            for (var row = 0; row < 4; row++)
            {
                for (var column = 0; column < 4; column++)
                {
                    // Render spaces in the shape that aren't blank space
                    // By inserting them into the matrix
                    if (_currentPiece.Shape[row, column] != -1)
                    {
                        _nonPlacedPositions.Add(new Position(shadowRow + row - 3, _currentPosition.Column + column));
                        _nonPlacedPositions.Add(new Position(39 - _currentPosition.Row - 3 + row, _currentPosition.Column + column));
                        _matrix[shadowRow + row - 3, _currentPosition.Column + column] = (_currentPiece.Shape[row, column], 2);
                        _matrix[39 - _currentPosition.Row - 3 + row, _currentPosition.Column + column] =
                            (_currentPiece.Shape[row, column], 1);
                    }
                }
            }
        }
        
        /// <summary>
        /// Returns the row of the bottom-leftmost block of the piece
        /// </summary>
        /// <param name="piece">The piece object</param>
        /// <param name="position">The current position of the piece</param>
        private int LowestFreeSpace(Piece piece, Position position)
        {
            var prevRow = position.Row;
            for (var row = position.Row + piece.RowOffset; row >= -piece.RowOffset; row--)
            {
                // The piece collides with row, then we're done and can use the previous row
                if (!PieceCollisionCheck(piece, new Position(row, position.Column)))
                {
                    break;
                }

                prevRow = row;
            }

            return prevRow;
        }
        
        /// <summary>
        /// Resets the position of the current piece to the default position
        /// </summary>
        private void ResetPosition()
        {
            // Set current position to be above the playfield (21st row from bottom) and in the middle
            _currentPosition = new Position(20 - _currentPiece.RowOffset, (int) Math.Floor((10 - (double) _currentPiece.Width) / 2) - _currentPiece.ColOffset);
        }

        /// <summary>
        /// Generates a new piece, taking into account the 6 previous generated pieces
        /// </summary>
        private string GeneratePiece()
        {
            // Clear the bag if all pieces have been generated
            if (_pieceBag.Count == Pieces.Length)
            {
                _pieceBag.Clear();
            }

            // Keep generating potential pieces until it's a piece that is not in the current bag
            while (true)
            {
                var candidate = Pieces[_random.Next(Pieces.Length)];
                if (!_pieceBag.Contains(candidate))
                {
                    _pieceBag.Add(candidate);
                    return candidate;
                }
            }
        }
        
        /// <summary>
        /// Generates a row of garbage at the bottom of the matrix, taking into account the previous garbage's hole, if any are specified
        /// </summary>
        /// <param name="previousHoleColumn">The column of the previous hole of garbage</param>
        /// <param name="sameColumnRate">The rate chance out of 1 that the garbage row will have the same hole</param>
        private void GenerateGarbage(int previousHoleColumn = -1, double sameColumnRate = 0)
        {
            var holeGenerated = false;
            var normalPieceRate = new double();

            // Shift board up one row to make space for garbage
            for (var row = 1; row < 40; row++)
            {
                for (var column = 0; column < 10; column++)
                {
                    _matrix[row - 1, column] = _matrix[row, column];
                }
            }

            // Previous hole column not chosen; make each column for the hole to be equally likely
            if (previousHoleColumn == -1)
            {
                normalPieceRate = 0.1;
            } else if (_random.NextDouble() <= sameColumnRate)
            {
                // Previous hole column chosen and it meets the chance for generation
                _matrix[39, previousHoleColumn] = (-1, -1);
                holeGenerated = true;
            }
            else
            {
                // Previous hole column chosen, but it doesn't meet the chance for generation
                // Set the previous hole to be garbage and make all other columns equally likely
                _matrix[39, previousHoleColumn] = (0, 0);
                normalPieceRate = 1 / 9D;
            }
            
            // Populate each column with blank space or nothing except for the chosen previous hole column
            for (var i = 0; i < 9; i++)
            {
                if (i != previousHoleColumn)
                {
                    if (!holeGenerated && _random.NextDouble() <= normalPieceRate)
                    {
                        // Generate hole
                        _matrix[39, i] = (-1, -1);
                        holeGenerated = true;
                    }
                    else
                    {
                        // Generate garbage; no hole in this column
                        _matrix[39, i] = (0, 0);
                    }
                }
                
            }

            // Generate hole in last column if hole has not been generated
            // This may technically be the most likely to occur
            _matrix[39, 9] = holeGenerated ? (0, 0) : (-1, -1);
        }

        /// <summary>
        /// Clear all lines where every block in the row is placed and push the above board down
        /// </summary>
        private void ClearLines()
        {
            for (var row = 0; row < 40; row++)
            {
                var blockColors = new List<int>();
                for (var column = 0; column < 10; column++)
                {
                    // We don't want to clear lines that aren't entirely placed blocks (full rows)
                    if (_matrix[row, column].blockType != 0)
                    {
                        blockColors = new List<int>();
                        break;
                    }

                    blockColors.Add(_matrix[row, column].blockColor);
                    
                    // Affect scoring depending on the gamemode
                    if (column == 9)
                    {
                        switch (_gamemode)
                        {
                            case GameMode.ScoreAttack:
                                // Gamemode not implemented
                                throw new NotImplementedException();
                                break;
                            case GameMode.LineClear:
                                // Gamemode not implemented
                                ObjectiveScore++;
                                break;
                            case GameMode.Cheese:
                                if (blockColors.Count(i => i == 0) == 9)
                                {
                                    ObjectiveScore++;
                                }
                                break;
                            default:
                                throw new ArgumentOutOfRangeException();
                        }
                        
                        // Shift matrix above the cleared line down
                        for (var replaceRow = row; replaceRow > 0; replaceRow--)
                        {
                            for (var replaceCol = 0; replaceCol < 10; replaceCol++)
                            {
                                // Prevent moving the current piece down
                                if (_matrix[replaceRow, replaceCol].blockType == 0)
                                {
                                    _matrix[replaceRow, replaceCol] = _matrix[replaceRow - 1, replaceCol];    
                                }
                            }
                        }
                        
                        // Empty highest top row
                        for (var replaceCol = 0; replaceCol < 10; replaceCol++)
                        {
                            _matrix[0, replaceCol] = (-1, -1);
                        }
                    }
                }
            }
        }
        
        /// <summary>
        /// Checks whether or not the matrix is topped out
        /// </summary>
        private bool CheckTopOut()
        {
            // Return true if the next piece would spawn inside placed blocks
            for (var column = (10 - _currentPiece.Width)/2; column < ((10 + _currentPiece.Width)/2)-1; column++)
            {
                if (_matrix[18, column].blockType == 0)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Swaps the current user-controlled piece with the current held piece with its rotation reset. 
        /// </summary>
        public void Hold()
        {
            // Prevent hold abuse
            if (_justHeld)
            {
                return;
            }
            
            ResetPosition();
            
            // Swap the current piece and hold slot
            var temp = _currentPiece;
            // If the hold queue is empty, use the next queued piece
            if (_holdQueue.Width == 0)
            {
                _currentPiece = _nextQueue[0];
                NextPiece();
            }
            else
            {
                _currentPiece = _holdQueue;
            }
            _holdQueue = temp;
            _justHeld = true;
            _holdQueue.ResetRotation();
            _gravityCount = 0;
        }

        /// <summary>
        /// Makes the next piece in the queue the current piece
        /// </summary>
        private void NextPiece()
        {
            // Spawn the first item in the next queue
            _currentPiece = _nextQueue[0];
            ResetPosition();

            // Move all pieces in the piece queue one place down
            for (var i = 1; i < _nextQueue.Length; i++)
            {
                _nextQueue[i - 1] = _nextQueue[i];
            }

            // Populate the last place for the queue
            _nextQueue[^1] = new Piece(GeneratePiece());
        }

        /// <summary>
        /// Resets key based counters/timers when the key is unpressed 
        /// </summary>
        /// <param name="key">The key being unpressed</param>
        public void UnpressedKey(Keys key)
        {
            switch (key)
            {
                case Keys.Left:
                    _leftCount = 0;
                    break;
                case Keys.Right:
                    _rightCount = 0;
                    break;
                case Keys.Down:
                    _dropCount = 0;
                    break;
                default:
                    throw new Exception("Key unpressed must be either related to left or right movement or soft dropping");
            }
        }

        /// <summary>
        /// Moves the current piece left, considering the delayed auto shift and auto repeat rate
        /// </summary>
        /// <param name="elapsedTime">The elapsed time since the last left press</param>
        public void LeftPressed(float elapsedTime)
        {
            _leftCount += elapsedTime;

            // Do not move if the DAS timer is unfinished
            if (_leftCount < _delayedAutoShift)
            {
                return;
            }
            
            // Move the piece to the very left of the board if the ARR is instant
            if (_autoRepeatRate == 0)
            {
                _currentPosition.Column = 0;
                return;
            }
            
            // Move left for every ARR interval after DAS timer
            for (var i = 0; i < (_leftCount - _delayedAutoShift) / _autoRepeatRate; i++)
            {
                MoveLeft();
                _leftCount -= _autoRepeatRate;
            }
        }

        /// <summary>
        /// Moves the current piece left, considering the delayed auto shift and auto repeat rate
        /// </summary>
        /// <param name="elapsedTime">The elapsed time since the last right press</param>
        public void RightPressed(float elapsedTime)
        {
            _rightCount += elapsedTime;
            
            // Do not move if the DAS timer is unfinished
            if (_rightCount < _delayedAutoShift)
            {
                return;
            }
            
            // Move the piece to the very right of the board if the ARR is instant
            if (_autoRepeatRate == 0)
            {
                _currentPosition.Column = 10 - _currentPiece.Width;
                return;
            }
            
            // Move right for every ARR interval after DAS timer
            for (var i = 0; i < (_rightCount - _delayedAutoShift) / _autoRepeatRate; i++)
            {
                MoveRight();
                _rightCount -= _autoRepeatRate;
            }
        }
        
        /// <summary>
        /// Moves the current piece left one unit, if possible
        /// </summary>
        public void MoveLeft()
        {
            // Prevent the piece from going left out of the matrix
            if (_currentPosition.Column + _currentPiece.ColOffset == 0)
            {
                return;
            }
            
            // Collision check
            for (var row = 3 - _currentPiece.RowOffset; row >= 3 - _currentPiece.RowOffset - _currentPiece.Height + 1; row--)
            {
                if (_matrix[39 - _currentPosition.Row - 3 + row, _currentPosition.Column + _currentPiece.RowWidth(row).Offset - 1].blockType == 0)
                {
                    return;
                }
            }

            // Locking mechanism
            if (_locking)
            {
                _lockMoveCount++;
            }

            _lockTimeCount = 0;
            _currentPosition.Column--;
        }

        /// <summary>
        /// Moves the current piece right one unit, if possible
        /// </summary>
        public void MoveRight()
        {
            // Prevent the piece from going right out of the matrix
            if (_currentPosition.Column + _currentPiece.Width + _currentPiece.ColOffset == 10)
            {
                return;
            }

            // Prevent collision with placed blocks
            for (var row = 3 - _currentPiece.RowOffset; row >= 3 - _currentPiece.RowOffset - _currentPiece.Height + 1; row--)
            {
                if (_matrix[39 - _currentPosition.Row - 3 + row, _currentPosition.Column + _currentPiece.RowWidth(row).Width + _currentPiece.RowWidth(row).Offset].blockType == 0)
                {
                    return;
                }
            }
            
            // Locking mechanism
            if (_locking)
            {
                _lockMoveCount++;
            }
            
            _lockTimeCount = 0;
            _currentPosition.Column++;
        }

        /// <summary>
        /// Moves the current piece down, considering the soft drop delay
        /// </summary>
        /// <param name="elapsedTime">The elapsed time since the last soft drop press</param>
        public void SoftDrop(float elapsedTime)
        {
            _dropCount += elapsedTime;

            // Instant soft drop in effect; drop the piece to the lowest it can go
            if (_softDropRate == 0)
            {
                _currentPosition.Row = 39 - LowestFreeSpace(_currentPiece, _currentPosition);
                return;
            }
            
            // Soft drop for every SDR interval
            for (var i = 0; i < _dropCount / _softDropRate; i++)
            {
                SoftDrop();
                _dropCount -= _softDropRate;
            }
 
        }

        /// <summary>
        /// Moves the current piece down one row if possible
        /// </summary>
        private void SoftDrop()
        {
            // Prevent the piece from going into blocks or out of the matrix
            if (_currentPosition.Row == LowestFreeSpace(_currentPiece, _currentPosition))
            {
                return;
            }

            _currentPosition.Row--;
        }

        /// <summary>
        /// Moves the current piece to the lowest point without collisions
        /// </summary>
        public void HardDrop()
        {
            // Reset states/counters of piece
            _locking = false;
            _lockTimeCount = 0;
            _lockMoveCount = 0;
            _gravityCount = 0;
            _justHeld = false;
            
            var dropRow = 39 - LowestFreeSpace(_currentPiece, _currentPosition);

            // Shift the piece down to the lowest it can go
            for (var row = 0; row < 4; row++)
            {
                for (var column = 0; column < 4; column++)
                {
                    if (_currentPiece.Shape[row, column] != -1)
                    {
                        // Avoid deleting the placed piece due
                        _nonPlacedPositions.RemoveAll(i =>
                            i.Row == dropRow + row - 3 && i.Column == _currentPosition.Column + column);
                        // Set blocks of piece
                        _matrix[dropRow + row - 3, _currentPosition.Column + column] = (_currentPiece.Shape[row, column], 0);
                    }
                }
            }

            NextPiece();
            ToppedOut = CheckTopOut();
        }
        
        /// <summary>
        /// Moves a piece down in specific intervals, depending on the gravity
        /// </summary>
        /// <param name="elapsedTime">The elapsed time since the last call</param>
        public void GravitatePiece(float elapsedTime)
        {
            _gravityCount += elapsedTime;

            // Move piece down after the counter has exceeded a specific amount
            while (_gravityCount > _gravity)
            {
                SoftDrop();
                _gravityCount -= _gravity;
            }
            
            // Piece locking mechanism
            if (_currentPosition.Row == 39 - LowestFreeSpace(_currentPiece, _currentPosition))
            {
                _locking = true;
                _lockTimeCount += elapsedTime;
            }

            // Hard drop if lock timer or movement counter exceeds threshold
            if (_lockTimeCount >= _lockTimeThreshold || _lockMoveCount >= _lockMoveThreshold)
            {
                HardDrop();
            }
        }

        /// <summary>
        /// Rotates the current piece by 90, -90, or 180 degrees clockwise
        /// </summary>
        /// <param name="deg">The amount of degrees to rotate the piece clockwise by</param>
        public void Rotate(int deg)
        {
            // Inappropriate rotation
            if (!new[] {90, -90, 180}.Contains(deg))
            {
                throw new Exception("Degrees must be 90 (clockwise), -90 (anticlockwise), or 180");
            }

            // Test rotation collision and SRS kick possibility (if the rotation collision check fails)
            var rotatedPiece = _currentPiece.Clone();
            rotatedPiece.Rotate(deg);
            
            // Find SRS offset
            // Get the right offsets
            var offsetTable = _currentPiece.BlockType != "i" ? _generalOffsets : _iOffsets;
            var beforeRotOffset = offsetTable[_currentPiece.Rotation];
            var afterRotOffset = offsetTable[rotatedPiece.Rotation];
            var offsetPosition = new Position();

            // For using basic rotation if SRS rotations also fails
            var possibleOffsets = new List<int>();
                
            // Iterate through SRS rotations to see which ones are possible
            for (var i = 0; i < 5; i++)
            {
                var colOffset = beforeRotOffset[i].Row - afterRotOffset[i].Row;
                var rowOffset = beforeRotOffset[i].Column - afterRotOffset[i].Column;

                var potOffsetPosition = new Position(_currentPosition.Row + rowOffset,
                    _currentPosition.Column + colOffset);
                
                if (PieceCollisionCheck(rotatedPiece, potOffsetPosition))
                {
                    possibleOffsets.Add(i);
                }
            }
            // Use the first rotation possible
            var lastOffset = possibleOffsets[0];
            offsetPosition = new Position(beforeRotOffset[lastOffset].Column - afterRotOffset[lastOffset].Column,
                beforeRotOffset[lastOffset].Row - afterRotOffset[lastOffset].Row);

            // All rotations fail; do not rotate the piece
            if (!PieceCollisionCheck(rotatedPiece, _currentPosition) && offsetPosition.Row == 0 && offsetPosition.Column == 0)
            {
                return;
            }

            _currentPiece.Rotate(deg);
            _currentPosition = new Position(_currentPosition.Row + offsetPosition.Row, _currentPosition.Column + offsetPosition.Column);

            // Locking mechanism
            if (_locking)
            {
                _lockMoveCount++;
            }
        }

        /// <summary>
        /// Checks whether or not the piece is able to fit in the position without colliding with placed blocks
        /// </summary>
        /// <param name="piece">The piece in the state to be tested</param>
        /// <param name="position">The position of the piece</param>
        private bool PieceCollisionCheck(Piece piece, Position position)
        {
            for (var row = 0; row < 4; row++)
            {
                for (var col = 0; col < 4; col++)
                {
                    try
                    {
                        // Return false of shape of piece conflicts with already placed blocks in the matrix
                        if (piece.Shape[row, col] != -1 &&
                            _matrix[39 - position.Row - 3 + row, position.Column + col].blockType == 0)
                        {
                            return false;
                        }
                    }
                    catch
                    {
                        // Piece would be out of the matrix; collides with the edges of the matrix
                        return false;
                    }
                    
                }
            }
            
            // All the blocks in the piece do not conflict with a placed block
            return true;
        }

        /// <summary>
        /// Renders the playfield, the visible portion of the matrix
        /// </summary>
        /// <param name="spriteBatch">The spriteBatch used to render the playfield</param>
        /// <param name="position">The top-left position of the playfield</param>
        /// <param name="blockTextures">The list of textures for the blocks</param>
        /// <param name="shadowTextures">The list of textures for the block shadows</param>
        /// <param name="playFieldTexture">The texture of the playfield</param>
        /// <param name="width">The width of the blocks; the playfield and the blocks will be scaled accordingly (default=16)</param>
        public void RenderPlayField(SpriteBatch spriteBatch, Vector2 position, List<Texture2D> blockTextures,
            List<Texture2D> shadowTextures, Texture2D playFieldTexture, int width = 16)
        {
            var scale = (float) width / 16;
            // Render everything as tinted gray if topped out
            var dynamicColor = ToppedOut ? Color.DimGray : Color.White; 

            // Background sprite
            spriteBatch.Draw(playFieldTexture, position, null, dynamicColor, 0f, Vector2.Zero, new Vector2(scale, scale),
                SpriteEffects.None, 0.5f);
            
            // Update the matrix
            ClearLines();
            RenderCurrentPiece();

            // Render each individual block in the bottommost 21 rows
            for (var row = 20; row < 40; row++) // TODO: Make function to draw selected blocks; row should start from 19
            {
                for (var column = 0; column < 10; column++)
                {
                    if (_matrix[row, column].blockColor == -1) // No need to render empty spaces
                    {
                        continue;
                    }

                    spriteBatch.Draw(
                        _matrix[row, column].blockType == 2
                            ? shadowTextures[_matrix[row, column].blockColor]
                            : blockTextures[_matrix[row, column].blockColor],
                        new Vector2(position.X + (column * width), position.Y + ((row - 20) * width)), null,
                        dynamicColor, 0f, Vector2.Zero, new Vector2(scale, scale), SpriteEffects.None, 0f);
                }
            }
        }

        /// <summary>
        /// Renders the hold and next queues and their slots
        /// </summary>
        /// <param name="spriteBatch">The spriteBatch used for rendering</param>
        /// <param name="position">The top-left position of the playfield border</param>
        /// <param name="blockTextures">The list of textures for the blocks</param>
        /// <param name="holdSlotTexture">The texture for the hold slot</param>
        /// <param name="nextSlotTexture">The texture for an individual next slot</param>
        /// <param name="playFieldBorderWidth">The width of the playfield, including its border</param>
        /// <param name="width">The width of each block (default=16)</param>
        /// <param name="padding">The padding between each slot (default=4)</param>
        public void RenderQueues(SpriteBatch spriteBatch, Vector2 position, List<Texture2D> blockTextures,
            Texture2D holdSlotTexture, Texture2D nextSlotTexture, int playFieldBorderWidth, int width = 16,
            int padding = 4)
        {
            // Hold slot
            RenderPieceSlot(spriteBatch, new Vector2(position.X - holdSlotTexture.Width - padding, position.Y),
                _holdQueue, blockTextures, holdSlotTexture, width);
            
            // Next queue slots
            for (var i = 0; i < _nextQueue.Length; i++)
            {
                RenderPieceSlot(spriteBatch,
                    new Vector2(position.X + playFieldBorderWidth + padding,
                        position.Y + ((holdSlotTexture.Height + padding) * i)), _nextQueue[i], blockTextures,
                    nextSlotTexture, width);
            }
        }

        /// <summary>
        /// Draws a slot and its containing piece
        /// </summary>
        /// <param name="spriteBatch">The SpriteBatch to draw the slot and its piece with</param>
        /// <param name="position">The top-left position of the hold texture</param>
        /// <param name="piece">The piece being contained within the slot</param>
        /// <param name="blockTextures">The list of block textures</param>
        /// <param name="slotTexture">The texture of the slot</param>
        /// <param name="width">The width of each block (default=16)</param>
        private void RenderPieceSlot(SpriteBatch spriteBatch, Vector2 position, Piece piece, List<Texture2D> blockTextures,
            Texture2D slotTexture, int width = 16)
        {
            var scale = (float) width / 16;
            // Find top left position for blocks for it to be centered within the slot texture
            var offsetX = (slotTexture.Width - (piece.Width * width)) / 2;
            var offsetY = (slotTexture.Height - (piece.Height * width)) / 2;
            var offsetRow = piece.RowOffset;
            var offsetCol = piece.ColOffset;
            
            spriteBatch.Draw(slotTexture, position, Color.White);

            // Render piece inside slot
            for (var row = 0; row < piece.Height ; row++)
            {
                var shapeRow = 4 - piece.Height + row;
                for (var column = 0; column < piece.Width; column++)
                {
                    if (piece.Shape[shapeRow - offsetRow, column + offsetCol] != -1)
                    {
                        spriteBatch.Draw(blockTextures[piece.Shape[shapeRow - offsetRow, column + offsetCol]],
                            new Vector2(position.X + offsetX + (column * width), position.Y + offsetY + (row * width)),
                            null,
                            ToppedOut ? Color.DimGray : Color.White, 0f, Vector2.Zero, new Vector2(scale, scale), SpriteEffects.None, 1f);
                    }
                }
            }
        }
    }
}