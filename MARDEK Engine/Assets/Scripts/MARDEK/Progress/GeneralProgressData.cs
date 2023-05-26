using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

using MARDEK.Core;
using MARDEK.CharacterSystem;
using MARDEK.Save;

namespace MARDEK.Progress
{

    public class GeneralProgressData : AddressableMonoBehaviour
    {
        [SerializeField] public string currentScene = default;
        [SerializeField] string _gameName = string.Empty;
        [SerializeField] public string sceneName { get; private set; } = string.Empty;
        [SerializeField] public DateTime savedTime { get; private set; } = new DateTime();
        [SerializeField] public List<CharacterProfile> profiles { get; private set; } = new();
        
        public string GameName
        {
            get
            {
                return _gameName;
            }
            set
            {
                if (string.IsNullOrEmpty(_gameName))
                    _gameName = value;
                return;
            }
        }

        public override void Save()
        {
            Scene scene = SceneManager.GetActiveScene();
            currentScene = scene.path;

            sceneName = SceneInfo.CurrentSceneInfoDisplayName;
            savedTime = DateTime.Now;

            if (Party.Instance != null)
            {
                profiles = new();
                foreach (Character c in Party.Instance.Characters)
                {
                    profiles.Add(c.Profile);
                }
            }

            base.Save();
        }

        public void LoadScene()
        {
            if (string.IsNullOrEmpty(currentScene))
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex + 1);
            else
                SceneManager.LoadScene(currentScene);
        }
    }
}
