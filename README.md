# First Person Shooter Game

Unity 6 (6000.6.0f1, URP, new Input System) wave-survival shooter. You hold a fixed position at the arena
entrance and shoot enemies coming from the front. Aim with a hardware gun (ESP32 + sensor) or the mouse.

## Run
1. Open the project in Unity 6000.6.0f1.
2. **FPS > Build All Scenes** (regenerates MainMenu and Arena).
3. Press Play. Play always starts from the MainMenu scene (toggle: **FPS > Play From Main Menu**).

## Hardware gun
Receive only, over USB serial (default COM3 @ 115200) or UDP (default port 8888). Messages, one per line:

| Message | Meaning |
|---|---|
| `1(x,y)` | Aim position, x and y 0-100 (50,50 = centre). Moves the crosshair over the whole screen |
| `P1` / `R1` | Trigger pressed / released |
| `S1` | Switch to the next weapon |
| `B1` | Reload |

Keys: **F1** gun/mouse, **F2** tuning panel (connection, smoothing, invert), **F3** debug overlay.
Only one program can open a COM port or UDP port: close Protokol Bridge / Hercules while playing.

## Editor tools
- **FPS > Hardware Input > Run Self Test**: parser, aim and receiver tests.
- **FPS > Hardware Input > Send Test Sweep To Game Port**: fake gun traffic.
