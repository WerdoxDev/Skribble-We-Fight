using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AudioController : MonoBehaviour {
    public static AudioController Instance;

    [SerializeField] private AudioSource menuTheme;
    [SerializeField] private AudioSource gameTheme;
    [SerializeField] private float fadeDuration;

    private void Awake() {
        if (Instance != null && Instance != this) Destroy(Instance.gameObject);
        Instance = this;

        DontDestroyOnLoad(this);
    }

    public void PlayMenuTheme() {
        menuTheme.Play();
        gameTheme.Stop();
    }

    public void PlayGameTheme() {
        StartCoroutine(FadeToGameThemeEnumerator(fadeDuration));
    }

    private IEnumerator FadeToGameThemeEnumerator(float duration) {
        float timeElapsed = 0;
        float startValue = menuTheme.volume;

        while (timeElapsed < duration) {
            menuTheme.volume = Mathf.Lerp(startValue, 0, timeElapsed / duration);
            timeElapsed += Time.deltaTime;
            yield return null;
        }

        menuTheme.Stop();
        gameTheme.Play();
    }
}
