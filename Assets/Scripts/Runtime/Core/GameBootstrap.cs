using UnityEngine;
using UnityEngine.SceneManagement;

namespace TurboRush.Core
{
    /// <summary>Boot scene entry (state Initialising): create services, load save, apply quality, go to the menu.</summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        private void Start()
        {
            Services s = Services.Ensure();
            s.States.Force(GameState.Initialising);
            SceneManager.LoadScene(SceneNames.Menu);
        }
    }
}
