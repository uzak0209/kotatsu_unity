using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class SketchCharacterLook : MonoBehaviour
{
    private enum CharacterVariant
    {
        Cat1P,
        Frog2P,
        Blob3P,
        Bat4P
    }

    [Header("Sketch Colors")]
    [SerializeField] private Color onePColor = new Color(0.95f, 0.48f, 0.45f, 1f);
    [SerializeField] private Color twoPColor = new Color(0.62f, 0.88f, 0.44f, 1f);
    [SerializeField] private Color threePColor = new Color(0.52f, 0.88f, 0.92f, 1f);
    [SerializeField] private Color fourPColor = new Color(0.67f, 0.52f, 0.95f, 1f);
    [SerializeField] private Color lineColor = new Color(0.20f, 0.20f, 0.23f, 1f);
    [SerializeField] private Color eyeColor = new Color(0.12f, 0.12f, 0.14f, 1f);

    private static Sprite catSprite;
    private static Sprite frogSprite;
    private static Sprite blobSprite;
    private static Sprite batSprite;
    private static Material sharedSpriteMaterial;

    private void Awake()
    {
        ApplyLook();
    }

    private void ApplyLook()
    {
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr == null) return;

        CharacterVariant variant = ResolveVariant(gameObject.name);
        sr.sprite = GetOrCreateSprite(variant);
        sr.sharedMaterial = GetSharedSpriteMaterial();
        sr.color = Color.white;
        sr.flipX = false;
        sr.flipY = false;
    }

    private static Material GetSharedSpriteMaterial()
    {
        if (sharedSpriteMaterial != null) return sharedSpriteMaterial;

        Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Unlit/Transparent");

        sharedSpriteMaterial = shader != null ? new Material(shader) : null;
        if (sharedSpriteMaterial != null)
        {
            sharedSpriteMaterial.hideFlags = HideFlags.DontSave;
        }
        return sharedSpriteMaterial;
    }

    private CharacterVariant ResolveVariant(string objectName)
    {
        if (objectName.Contains("4")) return CharacterVariant.Bat4P;
        if (objectName.Contains("3")) return CharacterVariant.Blob3P;
        if (objectName.Contains("2")) return CharacterVariant.Frog2P;
        return CharacterVariant.Cat1P;
    }

    private Sprite GetOrCreateSprite(CharacterVariant variant)
    {
        switch (variant)
        {
            case CharacterVariant.Frog2P:
                if (frogSprite == null) frogSprite = BuildSprite(FrogMask(), twoPColor);
                return frogSprite;
            case CharacterVariant.Blob3P:
                if (blobSprite == null) blobSprite = BuildSprite(BlobMask(), threePColor);
                return blobSprite;
            case CharacterVariant.Bat4P:
                if (batSprite == null) batSprite = BuildSprite(BatMask(), fourPColor);
                return batSprite;
            default:
                if (catSprite == null) catSprite = BuildSprite(CatMask(), onePColor, addSoftOuterEdge: true);
                return catSprite;
        }
    }

    private Sprite BuildSprite(string[] mask, Color fillColor, bool addSoftOuterEdge = false)
    {
        int width = mask[0].Length;
        int height = mask.Length;

        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };

        Color clear = new Color(0f, 0f, 0f, 0f);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                tex.SetPixel(x, y, clear);
            }
        }

        for (int row = 0; row < height; row++)
        {
            string line = mask[row];
            int y = (height - 1) - row;
            for (int x = 0; x < width; x++)
            {
                char c = line[x];
                if (c == '#') tex.SetPixel(x, y, fillColor);
                if (c == 'e') tex.SetPixel(x, y, eyeColor);
                if (c == 'm') tex.SetPixel(x, y, lineColor);
            }
        }

        if (addSoftOuterEdge)
        {
            OutlineAlpha(tex, new Color(1f, 0.93f, 0.93f, 1f));
        }
        tex.Apply();

        return Sprite.Create(
            tex,
            new Rect(0f, 0f, width, height),
            new Vector2(0.5f, 0.06f),
            16f,
            0,
            SpriteMeshType.FullRect
        );
    }

    private static void OutlineAlpha(Texture2D tex, Color outline)
    {
        int w = tex.width;
        int h = tex.height;
        Color[] src = tex.GetPixels();
        Color[] dst = (Color[])src.Clone();

        for (int y = 1; y < h - 1; y++)
        {
            for (int x = 1; x < w - 1; x++)
            {
                int i = y * w + x;
                if (src[i].a > 0.01f) continue;

                bool nearSolid =
                    src[i - 1].a > 0.01f || src[i + 1].a > 0.01f ||
                    src[i - w].a > 0.01f || src[i + w].a > 0.01f ||
                    src[i - w - 1].a > 0.01f || src[i - w + 1].a > 0.01f ||
                    src[i + w - 1].a > 0.01f || src[i + w + 1].a > 0.01f;

                if (nearSolid) dst[i] = outline;
            }
        }

        tex.SetPixels(dst);
    }

    private static string[] CatMask()
    {
        return new[]
        {
            "................",
            "......#..#......",
            ".....######.....",
            "....########....",
            "...##########...",
            "...##########...",
            "..###e####e##...",
            "..###########...",
            "..#####mm####...",
            "..###########...",
            ".###..####..##..",
            ".##...####...##.",
            ".##....##....##.",
            "..##...##.....#.",
            "..###.........#.",
            "...###.......##.",
            "................"
        };
    }

    private static string[] FrogMask()
    {
        return new[]
        {
            "................",
            "....##....##....",
            "...####..####...",
            "..############..",
            "..############..",
            "..###e####e###..",
            "..############..",
            "..####mmmm####..",
            "..############..",
            "..############..",
            "..############..",
            "...##########...",
            "...###....###...",
            "...##......##...",
            "................",
            "................"
        };
    }

    private static string[] BlobMask()
    {
        return new[]
        {
            "................",
            "................",
            "....########....",
            "...##########...",
            "..############..",
            "..############..",
            "..###e####e###..",
            "..############..",
            "..#####mm#####..",
            "..############..",
            "...##########...",
            "...##########...",
            "....##....##....",
            "................",
            "................",
            "................"
        };
    }

    private static string[] BatMask()
    {
        return new[]
        {
            "................",
            "....#......#....",
            "...###....###...",
            "..#####..#####..",
            ".##############.",
            "################",
            "..###e####e###..",
            "...##########...",
            "...####mm####...",
            "...##########...",
            "..###......###..",
            "..##........##..",
            ".##..........##.",
            "................",
            "................",
            "................"
        };
    }
}
