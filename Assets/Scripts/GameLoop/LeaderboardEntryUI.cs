using System.Collections;
using CreatureParts;
using multiplayer;
using Player;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LeaderboardEntryUI : MonoBehaviour
{
    public TextMeshProUGUI usernameText;
    public TextMeshProUGUI scoreText;

    public float scoreDuration = 1.5f;
    public float moveDuration = 1.5f;

    public PlayerRegister.PlayerData playerData;

    public Image background;
    public Image iconBackground;

    private RectTransform rt;
    private Vector2 targetPosition;

    private void Awake()
    {
        rt = GetComponent<RectTransform>();
    }

    public void SetData(PlayerRegister.PlayerData data)
    {
        playerData = data;
        usernameText.text = data.name;

        foreach (Player.Player player in FindObjectsByType<Player.Player>(FindObjectsSortMode.None))
        {
            if (player.owner == data.playerID)
            {
                Color color = player.gameObject.GetComponent<WormRenderer>().wormMaterial.color;
                background.color = color;
                iconBackground.color = color;
            }
        }
        int startScore = ScoreManager.Instance.GetPreviousScore(data.name);

        StopAllCoroutines();
        StartCoroutine(CountUp(startScore, data.score));

        ScoreManager.Instance.SetScore(data.name, data.score);
    }

    public void SetTargetPosition(Vector2 pos)
    {
        targetPosition = pos;

        StopCoroutine("SlideToTarget");
        StartCoroutine(SlideToTarget());
    }

    private IEnumerator CountUp(int start, int end)
    {
        float time = 0f;

        while (time < scoreDuration)
        {
            time += Time.deltaTime;
            float t = Mathf.SmoothStep(0, 1, time / scoreDuration);

            int value = Mathf.RoundToInt(Mathf.Lerp(start, end, t));
            scoreText.text = value.ToString();

            yield return null;
        }

        scoreText.text = end.ToString();
    }

    private IEnumerator SlideToTarget()
    {
        float time = 0f;
        Vector2 initialPosition = rt.anchoredPosition;

        while (time < moveDuration)
        {
            time += Time.deltaTime;
            float t = Mathf.SmoothStep(0, 1, time / moveDuration);

            rt.anchoredPosition = Vector2.Lerp(initialPosition, targetPosition, t);

            yield return null;
        }

        rt.anchoredPosition = targetPosition;
    }
}