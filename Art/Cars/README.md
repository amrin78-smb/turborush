# Car models (source art, not yet imported into Unity: that is M7)

| # | Car | Made by | Body / wheel / LOD1 tris | Size L×W×H (m) |
|---|-----|---------|--------------------------|----------------|
| 1 | Blaze | Kimi | 1024 / 200 / 600 | 4.26 × 1.98 × 1.23 |
| 2 | Zoom | Kimi | 832 / 200 / 528 | 4.00 × 1.98 × 1.49 |
| 3 | Sunny | Kimi | 1048 / 200 / 648 | 4.40 × 2.01 × 1.25 |
| 4 | Mint | Kimi | 868 / 156 / 556 | 4.15 × 1.96 × 1.37 |
| 5 | Grape | Claude (`Tools/Blender/car_grape.py`) | 2732 / 192 / 564 | 4.10 × 1.98 × 1.60 |
| 6 | Tango | Claude (`Tools/Blender/car_tango.py`) | 1448 / 168 / 552 | 4.11 × 1.93 × 1.43 |
| 7 | Frost | Claude (`Tools/Blender/car_frost.py`) | 864 / 168 / 592 | 4.35 × 1.92 × 1.18 |
| 8 | Bolt | Claude (`Tools/Blender/car_bolt.py`) | 1556 / 168 / 624 | 4.40 × 1.98 × 1.20 |

Every FBX passes `Tools/Blender/check_car_fbx.py` (hierarchy and names, triangle budget, the per-car size limits from
`Docs/ArtBriefs/KimiCarPrompts/`, wheel pivot and radius, ground contact, front at −Y, material names).

Rebuild a Claude-made car (Blender 5.2):
```
~/.local/bin/blender -b --factory-startup --python-exit-code 1 -P Tools/Blender/car_tango.py -- --out Art/Cars/tango
CAR_ALL_VIEWS=1 ...   # also render chase/front/side/top/LOD previews
~/.local/bin/blender -b --factory-startup -P Tools/Blender/check_car_fbx.py -- Art/Cars/*/Car_*.fbx
```
Kimi's preview colours look duller than the spec; the game sets colours per material slot from the car data, so this does not matter in-game.
