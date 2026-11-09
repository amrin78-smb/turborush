using System;

namespace TurboRush.Core
{
    public enum GameState { Initialising, MainMenu, VehicleSelect, TrackSelect, Countdown, Racing, Paused, Finishing, Results }

    /// <summary>
    /// Explicit game state machine with a whitelist of legal transitions. Pure C# so it is unit-testable and
    /// cannot be duplicated by scene loads (one instance lives in <see cref="Services"/>).
    /// </summary>
    public sealed class GameStateMachine
    {
        public GameState State { get; private set; } = GameState.Initialising;
        /// <summary>State before the current one (used to resume from Paused to Countdown or Racing).</summary>
        public GameState Previous { get; private set; } = GameState.Initialising;

        public event Action<GameState, GameState> Changed; // from, to

        public static bool IsAllowed(GameState from, GameState to)
        {
            if (from == to) return false;
            switch (from)
            {
                case GameState.Initialising: return to == GameState.MainMenu || to == GameState.Countdown;
                case GameState.MainMenu: return to == GameState.VehicleSelect || to == GameState.TrackSelect || to == GameState.Countdown;
                case GameState.VehicleSelect: return to == GameState.MainMenu || to == GameState.TrackSelect || to == GameState.Countdown;
                case GameState.TrackSelect: return to == GameState.MainMenu || to == GameState.VehicleSelect || to == GameState.Countdown;
                case GameState.Countdown: return to == GameState.Racing || to == GameState.Paused || to == GameState.MainMenu;
                case GameState.Racing: return to == GameState.Paused || to == GameState.Finishing || to == GameState.Countdown;
                case GameState.Paused: return to == GameState.Racing || to == GameState.Countdown || to == GameState.MainMenu || to == GameState.Finishing;
                case GameState.Finishing: return to == GameState.Results || to == GameState.Countdown;
                case GameState.Results: return to == GameState.Countdown || to == GameState.MainMenu || to == GameState.TrackSelect || to == GameState.VehicleSelect;
                default: return false;
            }
        }

        public bool TryTransition(GameState to)
        {
            if (!IsAllowed(State, to)) return false;
            GameState from = State;
            Previous = from;
            State = to;
            Changed?.Invoke(from, to);
            return true;
        }

        /// <summary>Unconditional jump used when a scene is entered directly (e.g. opening Race.unity in the Editor).</summary>
        public void Force(GameState to)
        {
            if (State == to) return;
            GameState from = State;
            Previous = from;
            State = to;
            Changed?.Invoke(from, to);
        }

        /// <summary>Only Racing accepts steering and advances race progress (Finishing keeps the sim running on autopilot).</summary>
        public static bool AcceptsSteering(GameState s) => s == GameState.Racing;
        public static bool SimulationRuns(GameState s) => s == GameState.Racing || s == GameState.Finishing;
    }
}
