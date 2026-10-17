using UnityEngine;

namespace CraftMine
{
    [RequireComponent(typeof(UnityEngine.UI.Text))]
    public sealed class CounterFeedback : MonoBehaviour
    {
        private UnityEngine.UI.Text label;
        private string previous;
        private float remaining;

        private void Awake() { label = GetComponent<UnityEngine.UI.Text>(); }
        private void Update()
        {
            if (label.text != previous)
            {
                if (!string.IsNullOrEmpty(previous)) remaining = .2f;
                previous = label.text;
            }
            remaining = Mathf.Max(0,remaining-Time.unscaledDeltaTime);
            float progress = 1-remaining/.2f;
            transform.localScale = Vector3.one*(1+Mathf.Sin(progress*Mathf.PI)*.045f);
        }
        private void OnDisable() { remaining = 0; transform.localScale = Vector3.one; }
    }
}
