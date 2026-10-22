using OpenTK.Windowing.Common;
using VelmoraCraft;

var width = 1280;
var height = 720;

var mode = VSyncMode.On;
var state = WindowState.Normal;
var border = WindowBorder.Resizable;

var title = "VelmoraCraft";

var game = new Game(width, height, title, mode, state, border);
game.Run();