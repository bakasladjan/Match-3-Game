using System.Collections;
using UnityEngine;

public class InputController : MonoBehaviour
{
    public float minSwipeDistance = 0.3f;  // minimum za swipe
    private BoardManager board;

    private Gem selectedGem = null;
    private Vector2 startPos;
    public bool inputLocked = false;
    void Start()
    {
        board = FindObjectOfType<BoardManager>();
    }

    void Update()
    {
        if (inputLocked)
            return;
        // 🚫 0) BLOKIRAJ INPUT AKO JE IGRA GOTOVA
        if (GameManager.Instance != null && GameManager.Instance.isGameOver)
            return;

        // ✔ 1) DETEKCIJA POČETKA PRITISKA
        if (Input.GetMouseButtonDown(0))
        {
            Vector3 worldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            Vector2 touchPos = new Vector2(worldPos.x, worldPos.y);

            selectedGem = GetGemAtPosition(touchPos);
            startPos = touchPos;
        }

        // ✔ 2) DETEKCIJA PUŠTANJA + IZRAČUNAVANJE SWIPE-a
        if (Input.GetMouseButtonUp(0) && selectedGem != null)
        {
            Vector3 worldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            Vector2 endPos = new Vector2(worldPos.x, worldPos.y);

            Vector2 swipe = endPos - startPos;

            if (swipe.magnitude < minSwipeDistance)
            {
                selectedGem = null;
                return;
            }

            swipe.Normalize();

            int dx = 0;
            int dy = 0;

            if (Mathf.Abs(swipe.x) > Mathf.Abs(swipe.y))
                dx = swipe.x > 0 ? 1 : -1;
            else
                dy = swipe.y > 0 ? 1 : -1;

            Gem other = board.GetGemAt(selectedGem.x + dx, selectedGem.y + dy);

            if (other != null)
            {
                StartCoroutine(board.TrySwap(selectedGem, other));
            }

            selectedGem = null;
        }
    }


    // 🔍 Raycast kroz 2D prostor da izabere gem
    Gem GetGemAtPosition(Vector2 pos)
    {
        Collider2D col = Physics2D.OverlapPoint(pos);
        if (col != null)
            return col.GetComponent<Gem>();

        return null;
    }
}
