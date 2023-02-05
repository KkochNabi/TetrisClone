using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Net.Mime;
using System.Reflection.Metadata;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Media;
using MonoGame.Extended;
using MonoGame.Extended.BitmapFonts;
using Color = Microsoft.Xna.Framework.Color;
using Rectangle = Microsoft.Xna.Framework.Rectangle;

namespace TetrisClone
{
    public class Game1 : Game
    {
        private GraphicsDeviceManager _graphics;
        private List<Texture2D> _blocks;
        private List<Texture2D> _blocksShadow;
        private Texture2D _playFieldBorder;
        private Texture2D _playFieldBorder2X;
        private Texture2D _playFieldTexture;
        private Texture2D _slotTexture;
        private Board _board;
        private SpriteBatch _spriteBatch;
        private KeyboardState _previousKeyState;
        private Stage _screenState;
        private MainMenuSelection _mainMenuSelection;
        private BitmapFont _font;
        private BitmapFont _font24Pt;
        private float _test;

        // Function to make sure a key is just recently pressed
        private bool JustPressed(Keys key) => Keyboard.GetState().IsKeyDown(key) && !_previousKeyState.IsKeyDown(key);

        // Function to check if a key is just recently pressed and the next menu item isn't undesirable
        private bool MenuCheck(Keys key, int indexChange, IConvertible selection) => JustPressed(key) && Enum.IsDefined(selection.GetType(), (int) selection + indexChange);

        
        private enum Stage
        {
            MainMenu,
            Cheese,
            Results,
        }

        private enum MainMenuSelection
        { // Ordered from top to bottom when rendered
            Cheese = 0,
            Exit = 1,
        }
        
        public Game1()
        {
            _graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            IsMouseVisible = true;
        }

        protected override void Initialize()
        {
            _screenState = Stage.MainMenu;
            _mainMenuSelection = 0;
            _graphics.PreferredBackBufferWidth = 1000;
            _graphics.PreferredBackBufferHeight = 800;
            _graphics.ApplyChanges();

            _test = 0;

            base.Initialize();
        }

        protected override void LoadContent()
        {
            _spriteBatch = new SpriteBatch(GraphicsDevice);

            // Fonts
            _font = Content.Load<BitmapFont>("arial");
            _font24Pt = Content.Load<BitmapFont>("arial24");

            // Textures
            // Normal & shadow block textures are from Tetris The Grand Master 3: Terror-Instinct
            _blocks = LoadSpriteSheet(Content.Load<Texture2D>("blocks"), 16, 16);
            _blocksShadow = LoadSpriteSheet(Content.Load<Texture2D>("blocksShadow"), 16, 16);
            _playFieldBorder = Content.Load<Texture2D>("matrixBorder");
            _playFieldBorder2X = Content.Load<Texture2D>("matrixBorder@2x");
            _playFieldTexture = new Texture2D(GraphicsDevice, 160, 320);
            _slotTexture = Content.Load<Texture2D>("slotBorder");

            // Texture Generation
            // Playfield texture is just black
            var playFieldTextureData = new Color[160 * 320];
            for (var i = 0; i < playFieldTextureData.Length; i++)
            {
                playFieldTextureData[i] = Color.Black;
            }
            _playFieldTexture.SetData(playFieldTextureData);
            
        }

        protected override void Update(GameTime gameTime)
        {
            switch (_screenState)
            {
                case Stage.MainMenu:
                    if (Keyboard.GetState().IsKeyDown(Keys.Escape))
                    {
                        Exit();
                    }
                    
                    // Cycle through the main menu options
                    if (MenuCheck(Keys.Up, -1, _mainMenuSelection))
                    {
                        _mainMenuSelection--;
                        break;
                    }
                    
                    if (MenuCheck(Keys.Down, 1, _mainMenuSelection))
                    {
                        _mainMenuSelection++;
                        break;
                    }

                    // Loop from top to bottom
                    if (JustPressed(Keys.Up) && _mainMenuSelection == default(MainMenuSelection))
                    {
                        _mainMenuSelection = Enum.GetValues(_mainMenuSelection.GetType()).Cast<MainMenuSelection>().Last();
                        break;
                    }
                    // Loop from bottom to top
                    if (JustPressed(Keys.Down) && _mainMenuSelection ==
                        Enum.GetValues(_mainMenuSelection.GetType()).Cast<MainMenuSelection>().Last())
                    {
                        _mainMenuSelection = default(MainMenuSelection);
                        break;
                    }

                    // Perform appropriate menu action
                    if (Keyboard.GetState().IsKeyDown(Keys.Enter))
                    {
                        switch (_mainMenuSelection)
                        {
                            case MainMenuSelection.Cheese:
                                _screenState = Stage.Cheese;
                                _board = new Board(Board.GameMode.Cheese);
                                break;
                            case MainMenuSelection.Exit:
                                Exit();
                                break;
                            default:
                                throw new ArgumentOutOfRangeException();
                        }
                        _mainMenuSelection = 0;
                    }

                    break;
                case Stage.Cheese:
                    // Win condition
                    if (_board.ToppedOut || _board.ObjectiveScore >= _board.ObjectiveTarget)
                    {
                        _screenState = Stage.Results;
                    }
                    
                    // TODO: Move input controls to another location when implementing other modes
                    // Piece rotation
                    if (JustPressed(Keys.Up))
                    { 
                        _board.Rotate(90);
                    }

                    if (JustPressed(Keys.LeftControl))
                    {
                        _board.Rotate(-90);
                    }

                    if (JustPressed(Keys.A))
                    {
                        _board.Rotate(180);
                    }

                    // Piece horizontal movement
                    if (JustPressed(Keys.Left))
                    {
                        _board.MoveLeft();
                    }
                    
                    // DAS movement
                    if (Keyboard.GetState().IsKeyDown(Keys.Left))
                    {
                        _board.LeftPressed(gameTime.GetElapsedSeconds());
                    }
                    else
                    {
                        _board.UnpressedKey(Keys.Left);
                    }

                    if (JustPressed(Keys.Right))
                    {
                        _board.MoveRight();
                    }
                    
                    // DAS movement
                    if (Keyboard.GetState().IsKeyDown(Keys.Right))
                    {
                        _board.RightPressed(gameTime.GetElapsedSeconds());
                    }
                    else
                    {
                        _board.UnpressedKey(Keys.Right);
                    }
                    
                    // Piece vertical movement
                    if (Keyboard.GetState().IsKeyDown(Keys.Down))
                    {
                        _board.SoftDrop(gameTime.GetElapsedSeconds());
                    }
                    else
                    {
                        _board.UnpressedKey(Keys.Down);
                    }
                    
                    if (JustPressed(Keys.Space))
                    {
                        _board.HardDrop();
                    }

                    // Hold swap
                    if (JustPressed(Keys.LeftShift))
                    {
                        _board.Hold();
                    }
                    
                    _board.GravitatePiece(gameTime.GetElapsedSeconds());
                    break;
                case Stage.Results:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }


            _previousKeyState = Keyboard.GetState();
            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.CornflowerBlue);
            _spriteBatch.Begin();
            
            // FPS Counter
            //_spriteBatch.DrawString(_font, $"FPS: {Math.Round(1/gameTime.ElapsedGameTime.TotalSeconds, 2)}", new Vector2(0, 0), Color.Lime);

            // Screen state specific rendering
            switch (_screenState)
            {
                case Stage.MainMenu:
                    // Function to output a different color if a menu option is the selected menu option
                    Color DynamicColor(MainMenuSelection repScreenState) =>
                        _mainMenuSelection == repScreenState ? Color.Firebrick : Color.White;

                    var menuOptions = new Dictionary<int, string>()
                    { 
                        // Lowest key is the first item, highest key is the last item, etc.
                        {0, "Play Cheese"},
                        {1, "Exit"}
                    };

                    // Render all the menu options, with the next option below the previous
                    foreach (var (i, text) in menuOptions)
                    {
                        _spriteBatch.DrawString(_font24Pt, text, new Vector2(4, 350 + (_font24Pt.LineHeight * i)), DynamicColor((MainMenuSelection)i));
                    }
                    break;
                case Stage.Cheese:
                    // Render playfield with its hold and queue slots
                    _spriteBatch.Draw(_playFieldBorder2X, new Vector2(250, 100), Color.White);
                    _board.RenderPlayField(_spriteBatch, new Vector2(258, 108), _blocks, _blocksShadow,
                        _playFieldTexture, 32);
                    _board.RenderQueues(_spriteBatch, new Vector2(250, 100), _blocks, _slotTexture, _slotTexture,
                        _playFieldBorder2X.Width, 20);

                    // Render shape of current block for ease of debugging
                    //var sb = new StringBuilder();
                    //for (var row = 0; row < 4; row++)
                    //{
                    //    for (var col = 0; col < 4; col++)
                    //    {
                    //        sb.Append((_board.CurrentPiece.Shape[row, col] == -1 ? "0" : "1") + " ");
                    //    }
                    //    
                    //    sb.Append("\n");
                    //}
                    //_spriteBatch.DrawString(_font, $"Shape:\n{sb}", new Vector2(750, 30), Color.Black);
                    
                    break;
                case Stage.Results:
                    // Render playfield with its hold and queue slots
                    _spriteBatch.Draw(_playFieldBorder2X, new Vector2(250, 100), Color.White);
                    _board.RenderPlayField(_spriteBatch, new Vector2(258, 108), _blocks, _blocksShadow,
                        _playFieldTexture, 32);
                    _board.RenderQueues(_spriteBatch, new Vector2(250, 100), _blocks, _slotTexture, _slotTexture,
                        _playFieldBorder2X.Width, 20);

                    // Notify the user that the game is over
                    const string winMsg = "You Win!";
                    _spriteBatch.DrawString(_font24Pt, winMsg,
                        new Vector2(418 - (_font24Pt.MeasureString(winMsg).Width / 2),
                            308 - (_font24Pt.MeasureString(winMsg).Height / 2)), Color.White);

                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            _spriteBatch.End();

            base.Draw(gameTime);
        }
        
        /// <summary>
        /// Generates a list of textures from a sprite sheet from left to right, top to bottom.
        /// </summary>
        /// <param name="spriteSheet">The Texture2D object of the sprite sheet</param>
        /// <param name="width">The width of each sprite</param>
        /// <param name="height">The height of each sprite</param>
        private List<Texture2D> LoadSpriteSheet(Texture2D spriteSheet, int width, int height)
        {
            var textures = new List<Texture2D>();
            // Amount of textures in the sprite sheet
            var textureCount = (spriteSheet.Width / width) * (spriteSheet.Height / height);
            var spritePixelCount = width * height;
            var spriteData = new Color[spritePixelCount];
            var spriteRegion = new Rectangle(0, 0, width, height);

            // Add every sprite region from the sprite sheet
            for (var i = 0; i < textureCount; i++)
            {
                var currentRegion = i;
                spriteRegion.X = 0;
                spriteRegion.Y = 0;
                
                // Find and set coordinates for sprite region
                while (true)
                {
                    if (currentRegion * width >= spriteSheet.Width)
                    { 
                        // Current sprite not on the current row, move the region to the left of the next row
                        currentRegion -= spriteSheet.Width / width;
                        spriteRegion.Y += height;
                    }
                    else
                    { 
                        // Current sprite on the current row, move the region to the right
                        spriteRegion.X += currentRegion * width;
                        break;
                    }
                }
                
                // Add current sprite to textures list
                spriteSheet.GetData(0, spriteRegion, spriteData, 0, spritePixelCount);
                textures.Add(new Texture2D(_graphics.GraphicsDevice, width, height));
                textures[i].SetData(0, new Rectangle(0, 0, width, height), spriteData, 0, spritePixelCount);
            }
            
            return textures;
        }
    }
}