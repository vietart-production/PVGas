using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DetailPanelController : MonoBehaviour
{
    [SerializeField] private Image headerImage;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text subtitleText;
    [SerializeField] private Transform statsContainer;
    [SerializeField] private Transform cardsContainer;
    [SerializeField] private GameObject statPrefab;
    [SerializeField] private GameObject cardPrefab;

    public void Configure(Image header, TMP_Text title, TMP_Text subtitle, Transform stats, Transform cards, GameObject statItemPrefab, GameObject cardItemPrefab)
    {
        headerImage = header;
        titleText = title;
        subtitleText = subtitle;
        statsContainer = stats;
        cardsContainer = cards;
        statPrefab = statItemPrefab;
        cardPrefab = cardItemPrefab;
    }

    public void Bind(TopicData topic)
    {
        titleText.text = topic.displayName;
        subtitleText.text = topic.subtitle;
        if (topic.headerImage != null)
            headerImage.sprite = topic.headerImage;

        ClearChildren(statsContainer);
        if (topic.stats != null)
        {
            foreach (var stat in topic.stats)
            {
                var go = Instantiate(statPrefab, statsContainer);
                var text = go.GetComponentInChildren<TMP_Text>();
                if (text != null) text.text = stat;
            }
        }

        ClearChildren(cardsContainer);
        if (topic.cards != null)
        {
            foreach (var card in topic.cards)
            {
                var go = Instantiate(cardPrefab, cardsContainer);
                var texts = go.GetComponentsInChildren<TMP_Text>();
                if (texts.Length > 0) texts[0].text = card.title;
                if (texts.Length > 1) texts[1].text = card.body;
                var img = go.GetComponentInChildren<Image>();
                if (img != null && card.image != null) img.sprite = card.image;
            }
        }
    }

    private void ClearChildren(Transform container)
    {
        if (container == null) return;
        for (int i = container.childCount - 1; i >= 0; i--)
            Destroy(container.GetChild(i).gameObject);
    }
}
