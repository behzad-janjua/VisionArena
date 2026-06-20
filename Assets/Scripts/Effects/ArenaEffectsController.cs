using System.Collections;
using UnityEngine;

namespace KiForge.Effects
{
    public sealed class ArenaEffectsController : MonoBehaviour
    {
        private Transform playerRoot;
        private GameObject aura;
        private GameObject shield;
        private Camera mainCamera;

        public void Initialize(Transform player)
        {
            playerRoot = player;
            mainCamera = Camera.main;
            aura = CreateDisc("Charge Aura", new Color(0.2f, 0.85f, 1f, 0.35f), 1.2f);
            shield = CreateDisc("Shield Arc", new Color(0.4f, 1f, 0.8f, 0.42f), 1.8f);
            aura.SetActive(false);
            shield.SetActive(false);
        }

        private void LateUpdate()
        {
            if (playerRoot == null)
            {
                return;
            }

            if (aura != null)
            {
                aura.transform.position = playerRoot.position + Vector3.up * 0.2f;
            }

            if (shield != null)
            {
                shield.transform.position = playerRoot.position + new Vector3(0.7f, 0.25f, -0.1f);
            }
        }

        public void ShowAura(bool visible)
        {
            if (aura != null)
            {
                aura.SetActive(visible);
            }
        }

        public void SetAuraPower(float charge)
        {
            if (aura != null)
            {
                var scale = Mathf.Lerp(1.1f, 2.2f, Mathf.Clamp01(charge));
                aura.transform.localScale = Vector3.one * scale;
            }
        }

        public void ShowShield(bool visible)
        {
            if (shield != null)
            {
                shield.SetActive(visible);
            }
        }

        public void FireBeam(Vector2 start, Vector2 end, int level)
        {
            var beam = new GameObject("Energy Beam");
            var line = beam.AddComponent<LineRenderer>();
            line.positionCount = 2;
            line.SetPosition(0, start);
            line.SetPosition(1, end);
            line.startWidth = Mathf.Lerp(0.12f, 0.45f, Mathf.Clamp01(level / 4f));
            line.endWidth = line.startWidth;
            line.material = new Material(Shader.Find("Sprites/Default"));
            line.startColor = new Color(0.1f, 0.85f, 1f, 1f);
            line.endColor = new Color(1f, 0.95f, 0.35f, 0.7f);
            StartCoroutine(FadeAndDestroy(beam, 0.22f));
            ScreenShake();
        }

        public void ShowSlash(Vector2 origin, Vector2 direction)
        {
            var slash = new GameObject("Slash Trail");
            var line = slash.AddComponent<LineRenderer>();
            line.positionCount = 3;
            var normal = new Vector2(-direction.y, direction.x);
            line.SetPosition(0, origin - normal * 1.1f);
            line.SetPosition(1, origin + direction.normalized * 1.1f);
            line.SetPosition(2, origin + normal * 1.1f);
            line.startWidth = 0.18f;
            line.endWidth = 0.02f;
            line.material = new Material(Shader.Find("Sprites/Default"));
            line.startColor = new Color(1f, 0.25f, 0.75f, 1f);
            line.endColor = new Color(0.2f, 0.9f, 1f, 0.2f);
            StartCoroutine(FadeAndDestroy(slash, 0.28f));
        }

                public void ShowPainBurst(Vector2 origin)
        {
            var burst = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            burst.name = "Pain Burst";
            burst.transform.position = new Vector3(origin.x, origin.y, -0.2f);
            burst.transform.localScale = Vector3.one * 0.18f;
            burst.GetComponent<Renderer>().material = new Material(Shader.Find("Sprites/Default"))
            {
                color = new Color(1f, 0.08f, 0.08f, 0.85f)
            };
            StartCoroutine(PopAndDestroy(burst, 0.22f));
        }

public void ScreenShake()
        {
            if (mainCamera != null)
            {
                StartCoroutine(ShakeCamera());
            }
        }

        private IEnumerator ShakeCamera()
        {
            var start = mainCamera.transform.position;
            for (var i = 0; i < 8; i++)
            {
                mainCamera.transform.position = start + (Vector3)Random.insideUnitCircle * 0.06f;
                yield return null;
            }

            mainCamera.transform.position = start;
        }

                private IEnumerator PopAndDestroy(GameObject target, float duration)
        {
            var timer = 0f;
            var startScale = target.transform.localScale;
            var endScale = startScale * 3.2f;

            while (timer < duration)
            {
                timer += Time.deltaTime;
                target.transform.localScale = Vector3.Lerp(startScale, endScale, timer / duration);
                yield return null;
            }

            Destroy(target);
        }

private IEnumerator FadeAndDestroy(GameObject target, float duration)
        {
            yield return new WaitForSeconds(duration);
            Destroy(target);
        }

        private static GameObject CreateDisc(string name, Color color, float scale)
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            obj.name = name;
            obj.transform.localScale = new Vector3(scale, scale, 0.08f);
            obj.GetComponent<Renderer>().material = new Material(Shader.Find("Sprites/Default")) { color = color };
            return obj;
        }
    }
}
