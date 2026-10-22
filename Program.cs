using VelmoraCraft;
using OpenTK.Windowing.Common;

int width = 1280;
int height = 720;

VSyncMode mode = VSyncMode.On;
WindowState state = WindowState.Normal;
WindowBorder border = WindowBorder.Resizable;

string title = "VelmoraCraft";

Game game = new Game(width, height, title, mode, state, border);
game.Run();
