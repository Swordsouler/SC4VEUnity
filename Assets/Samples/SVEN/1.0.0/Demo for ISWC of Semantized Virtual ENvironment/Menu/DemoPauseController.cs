using NaughtyAttributes;
using UnityEngine;

namespace Sven.Demo
{
    public class DemoPauseController : MonoBehaviour
    {
        private bool isPaused = false;
        [BoxGroup("View")]
        public GameObject pauseMenu;

        public void Awake()
        {
            isPaused = pauseMenu.activeSelf;
            RefreshState();
        }

        public virtual void TogglePause()
        {
            isPaused = !isPaused;
            RefreshState();
        }

        public void RefreshState()
        {
            pauseMenu.SetActive(isPaused);
            Time.timeScale = isPaused ? 0f : 1f;
        }

        public void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape)) TogglePause();
        }
    }
}