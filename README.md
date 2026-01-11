# Match-3 Game (Unity)

A casual **Match-3** game: classic swap gameplay, cascades, special gems (Rocket, Bomb, Disco Ball, Paper Plane), chain reactions, and level goals.

> Built in **Unity** using a 2D grid board (default 8x8).  
> The core gameplay logic lives in `BoardManager.cs`.

---

## 🎮 Gameplay

- Swap two adjacent gems.
- Match **3+** gems of the same color to destroy them and trigger **cascades**.
- Special gems are created from specific patterns:
  - **4 in a line** → Rocket (Horizontal / Vertical)
  - **5+ in a line** → Disco Ball
  - **L / T shape** → Bomb (3x3 explosion)
  - **2x2 square** → Paper Plane
- Specials can be activated by:
  - Swapping with another gem/special
  - Tapping the special directly (single special activation)
- Special swap combinations:
  - Disco + Disco → Clears the entire board
  - Disco + Rocket → All gems of that color become rockets and activate
  - Disco + Bomb → All gems of that color become bombs and activate
  - Disco + Plane → Multiple planes launch toward unique targets (with cap)
  - Rocket + Rocket → 3 rows + 3 columns
  - Bomb + Bomb → 5x5 explosion
  - Bomb + Rocket → “Thick plus” (3 rows + 3 columns)

---

## ✨ Key Features

- ✅ Stable **cascade loop** (destroy → collapse → spawn → repeat)
- ✅ **Protected host** logic (specials are created from matches but their host gem is not destroyed)
- ✅ Chain reactions using a queued activation system
- ✅ Paper Plane behavior:
  - “+” shaped destruction on launch
  - No double-launch in the same move (`planesLaunchedThisAction`)
  - Unique targets per move (`reservedPlaneTargets`)
- ✅ Disco Ball safety:
  - Disco activated by swap will not randomly activate again (`discosActivatedBySwap`)
- ✅ Cap for Disco + Plane combo (`discoPlaneMaxFlights`) to prevent excessive spawns

---

## 🧱 Project Structure

Key scripts:

- `BoardManager.cs`  
  Grid logic, swapping, match detection, special creation, chain reactions, collapse & spawn.
- `Gem.cs`  
  Gem data (`type`, `x`, `y`, `specialType`) and animations.
- `ScoreManager.cs`  
  Score and combo handling.
- `LevelGoalManager.cs`  
  Level goals (e.g. target colors and counters).
- `GameManager.cs`  
  Global game state (`isGameOver`, etc.).

---

## ⚙️ Configuration (Inspector)

In `BoardManager` you can tweak:

- `width`, `height` – board dimensions
- **Speeds**
  - `swapDuration`
  - `dropBaseDuration`
  - `planeFlightDuration`
- **Plane**
  - `discoPlaneMaxFlights` – max number of planes in Disco + Plane combo
- **Prefabs / Sprites**
  - `gemPrefab`
  - `gemSprites[]`
  - Special sprites (bomb / rocket / disco / plane)
- **Particles**
  - FX prefabs for specials and special creation

---

## 🚀 How to Run

1. Clone the repository:
   ```bash
   git clone https://github.com/bakasladjan/Match-3.git
---

## 📸 Media
https://youtu.be/8Jt4x4Xm5BI
