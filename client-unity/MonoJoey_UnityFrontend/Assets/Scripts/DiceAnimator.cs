using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public sealed class DiceAnimator : MonoBehaviour
{
    [Header("Dice Faces")]
    [SerializeField] private Image[] diceFaces;
    [SerializeField] private float rollDuration = 0.75f;
    [SerializeField] private AnimationCurve rollCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    private Coroutine activeCoroutine;
    private int[] lastValues = Array.Empty<int>();

    public bool IsAnimating => activeCoroutine != null;
    public int[] LastValues => lastValues;
    public int DiceFaceCount => diceFaces == null ? 0 : diceFaces.Length;
    public float RollDuration => rollDuration;

    public void AnimateRoll(int[] values)
    {
        StopAnimation();

        lastValues = NormalizeValues(values);
        activeCoroutine = StartCoroutine(AnimateRollRoutine(lastValues));
    }

    public void StopAnimation()
    {
        if (activeCoroutine != null)
        {
            StopCoroutine(activeCoroutine);
            activeCoroutine = null;
        }
    }

    private IEnumerator AnimateRollRoutine(int[] values)
    {
        float duration = Mathf.Max(0.01f, rollDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float curveT = rollCurve == null ? t : Mathf.Clamp01(rollCurve.Evaluate(t));
            int cycleOffset = Mathf.FloorToInt(curveT * 18f);

            for (int i = 0; i < DiceFaceCount; i++)
            {
                SetDiceVisual(i, ((cycleOffset + i) % 6) + 1, true, curveT);
            }

            yield return null;
        }

        for (int i = 0; i < DiceFaceCount; i++)
        {
            int value = i < values.Length ? values[i] : 1;
            SetDiceVisual(i, value, false, 1f);
        }

        activeCoroutine = null;
    }

    private void SetDiceVisual(int index, int value, bool rolling, float t)
    {
        if (diceFaces == null || index < 0 || index >= diceFaces.Length || diceFaces[index] == null)
        {
            return;
        }

        Image face = diceFaces[index];
        float pulse = rolling ? Mathf.Sin(t * Mathf.PI * 8f) * 0.08f : 0f;
        face.color = rolling
            ? Color.Lerp(new Color(0.90f, 0.94f, 0.98f, 1f), new Color(0.95f, 0.78f, 0.24f, 1f), Mathf.Abs(pulse) * 10f)
            : new Color(0.96f, 0.97f, 0.98f, 1f);
        face.rectTransform.localScale = Vector3.one * (1f + Mathf.Abs(pulse));
        face.rectTransform.localRotation = Quaternion.Euler(0f, 0f, rolling ? Mathf.Lerp(-8f, 8f, Mathf.PingPong(t * 8f, 1f)) : 0f);

        Text label = face.GetComponentInChildren<Text>();
        if (label != null)
        {
            label.text = Mathf.Clamp(value, 1, 6).ToString();
        }
    }

    private static int[] NormalizeValues(int[] values)
    {
        if (values == null || values.Length == 0)
        {
            return new[] { 1, 1 };
        }

        int[] normalized = new int[Mathf.Max(2, values.Length)];
        for (int i = 0; i < normalized.Length; i++)
        {
            int source = i < values.Length ? values[i] : 1;
            normalized[i] = Mathf.Clamp(source, 1, 6);
        }

        return normalized;
    }
}
