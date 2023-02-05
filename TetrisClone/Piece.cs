using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.Serialization;
using Microsoft.Xna.Framework.Graphics;

namespace TetrisClone
{
    public class Piece
    {
        public readonly int[,] Shape = new int[4,4];
        public readonly string BlockType;
        public int Rotation;

        /// <summary>
        /// Width of the piece (excluding blank space)
        /// </summary>
        public int Width
        {
            get
            {
                var count = 4;
                // Subtract from count if a column is completely -1 (blank space) to get columns with blocks
                for (var column = 0; column < 4; column++)
                {
                    if (Enumerable.Range(0, Shape.GetLength(0)).Select(i => Shape[i, column]).ToArray()
                        .Count(i => i == -1) == 4)
                    {
                        count--;
                    }
                }

                return count;
            }
        }

        /// <summary>
        /// Height of the shape (excluding blank space)
        /// </summary>
        public int Height
        {
            get
            {
                var count = 4;
                // Subtract from count if a row is completely -1 (blank space) to get rows of blocks
                for (var row = 0; row < 4; row++)
                {
                    if (Enumerable.Range(0, Shape.GetLength(1)).Select(i => Shape[row, i]).ToArray()
                        .Count(i => i == -1) == 4)
                    {
                        count--;
                    }
                }

                return count;
            }
        }

        /// <summary>
        /// Row offset, where rows below are blank space
        /// </summary>
        public int RowOffset
        {
            get
            {
                var result = 0;
                for (var row = 3; row >= 0; row--)
                {
                    if (Enumerable.Range(0, Shape.GetLength(1)).Select(i => Shape[row, i]).ToArray()
                        .Count(i => i == -1) < 4)
                    {
                        result = 3 - row;
                        break;
                    }
                }

                return result;
            }
        }

        /// <summary>
        /// Column offset, where columns to the left are blank space
        /// </summary>
        public int ColOffset
        {
            get
            {
                var result = 0;
                for (var col = 0; col < 4; col++)
                {
                    if (Enumerable.Range(0, Shape.GetLength(0)).Select(i => Shape[i, col]).ToArray()
                        .Count(i => i == -1) < 4)
                    {
                        result = col;
                        break;
                    }
                }

                return result;
            }
        }

        /// <summary>
        /// Returns the width and offset of a specific row
        /// </summary>
        /// <param name="row">The row from top of the piece to bottom</param>
        public (int Width, int Offset) RowWidth(int row)
        {
            var count = 0;
            var offset = 4;
            
            // Subtract from count if cell in the row is -1 (blank space)
            for (var column = 0; column < 4; column++)
            {
                if (Shape[row, column] != -1)
                {
                    count++;
                    offset = column < offset ? column : offset;
                }
            }

            return (count, offset != 4 ? offset : 0);
        }

        /// <summary>
        /// Initialize a blank piece
        /// </summary>
        public Piece()
        {
            BlockType = "";
            Rotation = 0;
            // Set shape of piece to be completely blank space
            for (var row = 0; row < 4; row++)
            {
                for (var column = 0; column < 4; column++)
                {
                    Shape[row, column] = -1;
                }
            }
        }
        
        /// <summary>
        /// Initialize a piece 
        /// </summary>
        /// <param name="blockType"></param>
        public Piece(string blockType)
        {
            BlockType = blockType;
            Rotation = 0;

            Reset();
        }

        /// <summary>
        /// Sets the state of the class to as if you just initiated it
        /// </summary>
        private void Reset()
        {
            var blockTypes = new Dictionary<string, (bool[,] shape, string color)>()
            {  
                // Format of shapeName:(shape in a 4x4 grid, color), from bottom to top
                {"i", (new[,]
                {
                    {false, false, false, false}, 
                    {true, true, true, true},
                    {false, false, false, false}, 
                    {false, false, false, false}
                    
                }, "lightBlue")},
                {"j", (new[,]
                {
                    {false, false, false, false},
                    {true, false, false, false},
                    {true, true, true, false},
                    {false, false, false, false}
                    
                }, "blue")},
                {"l", (new[,]
                {
                    {false, false, false, false},
                    {false, false, true, false},
                    {true, true, true, false},
                    {false, false, false, false}
    
                }, "orange")},
                {"o", (new[,]
                {
                    {false, false, false, false},
                    {false, true, true, false},
                    {false, true, true, false}, 
                    {false, false, false, false} 

                }, "yellow")},
                {"s", (new[,]
                {
                    {false, false, false, false}, 
                    {false, true, true, false}, 
                    {true, true, false, false},
                    {false, false, false, false}

                }, "green")},
                {"z", (new[,]
                {
                    {false, false, false, false}, 
                    {true, true, false, false}, 
                    {false, true, true, false},
                    {false, false, false, false}
                }, "red")},
                {"t", (new[,]
                {
                    {false, false, false, false},
                    {false, true, false, false}, 
                    {true, true, true, false},
                    {false, false, false, false}
                }, "magenta")}
                
            };

            // Set shape and color, iterating to each space in the shape
            for (var row = 0; row < 4; row++)
            {
                for (var column = 0; column < 4; column++)
                {
                    // Set space to correct color
                    if (blockTypes[BlockType].shape[row, column])
                    {
                        Shape[row, column] = Block.Colors[blockTypes[BlockType].color];
                    }
                    else
                    {
                        // Set space to be blank space
                        Shape[row, column] = -1;
                    }
                }
            }
        }

        /// <summary>
        /// Rotates the current piece by 90, -90, or 180 degrees clockwise, based on SRS rules
        /// </summary>
        /// <param name="deg">The amount of degrees to rotate the piece clockwise by</param>
        public void Rotate(int deg)
        {
            // Change the rotation
            Rotation += deg;
            
            // Convert the rotation so it's between -90 and 180
            if (Rotation < -90)
            {
                Rotation += 360;
            }

            if (Rotation > 180)
            {
                Rotation -= 360;
            }

            // "o" block does not need to be rotated; it is a square
            switch (BlockType)
            {
                case "o":
                    return;
            }

            // Bool on whether or not the shape can fit in a 3x3 matrix
            var doFullRotation = Height == 4 || Width == 4;
            
            // Transfer shape into a temp var and make the original shape var blank
            var oldShape = (int[,]) Shape.Clone();
            for (var row = 0; row < 4; row++)
            {
                for (var col = 0; col < 4; col++)
                {
                    Shape[row, col] = -1;
                }
            }
            
            // Rotate the piece differently depending on bool doFullRotation
            if (doFullRotation)
            {
                Rotate4X4(deg, oldShape);
            }
            else
            {
                Rotate3X3(deg, oldShape);
            }
        }

        /// <summary>
        /// Rotates the piece's shape using the full area of the shape 
        /// </summary>
        /// <param name="deg">The degree of rotation</param>
        /// <param name="shape">The original shape</param>
        private void Rotate4X4(int deg, int[,] shape)
        {
            // Local function to get coordinates relative to the center of the matrix
            static int ToRelative(int i) => i >= 2 ? i - 1 : i - 2;
            // Local function to get the index of the matrix from relative coordinates
            static int ToTrue(int i) => (i > 0 ? 1 : 2) + i;
            // Repeat entire procedure for every x and y coordinate in the 4x4 matrix
            for (var x = 0; x < 4; x++)
            {
                for (var y = 0; y < 4; y++)
                {
                    // Get coordinates relative to the center of the 4x4 matrix
                    var tempX = ToRelative(x);
                    var tempY = ToRelative(y);

                    // Rotate the piece's shape depending on the inputted degrees
                    // By manipulating the coordinates
                    // And converting the relative coordinates back to their indices
                    switch (deg)
                    {
                        case 90:
                            // (-y, x)
                            Shape[ToTrue(tempX), ToTrue(-tempY)] = shape[y, x];
                            break;
                        case -90:
                            // (y, -x)
                            Shape[ToTrue(-tempX), ToTrue(tempY)] = shape[y, x];
                            break;
                        case 180:
                            // (-x, -y)
                            Shape[ToTrue(-tempY), ToTrue(-tempX)] = shape[y, x];
                            break;
                    }
                }
            }
        }

        /// <summary>
        /// Rotates the piece's shape using a 3x3 area in the bottom left
        /// </summary>
        /// <param name="deg">The degree of rotation</param>
        /// <param name="shape">The original shape</param>
        private void Rotate3X3(int deg, int[,] shape)
        {
            // Indices for the pivoting point
            const int pivotX = 1;
            const int pivotY = 2;
            // Repeat rest of procedure for every x and y coordinate in the 3x3 region
            for (var x = 0; x < 3; x++)
            {
                for (var y = 1; y < 4; y++)
                {
                    // Get distance to pivot for relative coordinates
                    var tempX = x - pivotX;
                    var tempY = y - pivotY;

                    // Rotate the piece's shape depending on the inputted degrees
                    // By manipulating the coordinates
                    // And converting the coordinates back to their indices
                    switch (deg)
                    {
                        case 90:
                            // (y, -x)
                            Shape[pivotY + tempX, pivotX - tempY] = shape[y, x];
                            break;
                        case -90:
                            // (-y, x)
                            Shape[pivotY - tempX, pivotX + tempY] = shape[y, x];
                            break;
                        case 180:
                            // (-x, -y)
                            Shape[pivotY - tempY, pivotX - tempX] = shape[y, x];
                            break;
                    }
                }
            }
        }

        /// <summary>
        /// Reverts the piece back to its original, unrotated form
        /// </summary>
        public void ResetRotation()
        {
            Reset();
        }

        /// <summary>
        /// Returns a clone of this object
        /// </summary>
        public Piece Clone()
        {
            var clone = new Piece(BlockType);
            clone.Rotate(Rotation);

            return clone;
        }
        
    }
}