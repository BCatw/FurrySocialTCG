using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace FurrySocialCard.CardData
{
    public sealed class CardCatalogLoader : MonoBehaviour
    {
        [SerializeField] private TextAsset bundledCatalog;
        [SerializeField] private bool downloadRemoteCatalog = true;
        [SerializeField] private string remoteCatalogUrl;
        [SerializeField, Min(1)] private int requestTimeoutSeconds = 10;

        public CardCatalog Current { get; private set; }
        public string CurrentSource { get; private set; }

        private string CachePath => Path.Combine(Application.persistentDataPath, "CardData", "fsc_cards.json");

        public IEnumerator Load(Action<CardCatalog> onLoaded = null, Action<string> onFailed = null)
        {
            string remoteError = null;
            if (downloadRemoteCatalog && Uri.TryCreate(remoteCatalogUrl, UriKind.Absolute, out _))
            {
                using (UnityWebRequest request = UnityWebRequest.Get(remoteCatalogUrl))
                {
                    request.timeout = requestTimeoutSeconds;
                    yield return request.SendWebRequest();

                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        string json = request.downloadHandler.text;
                        if (TryUse(json, remoteCatalogUrl, out remoteError))
                        {
                            TryWriteCache(json);
                            onLoaded?.Invoke(Current);
                            yield break;
                        }
                    }
                    else
                    {
                        remoteError = request.error;
                    }
                }
            }

            if (TryReadCache(out string cachedJson) && TryUse(cachedJson, CachePath, out _))
            {
                onLoaded?.Invoke(Current);
                yield break;
            }

            string bundledError = null;
            TextAsset fallbackCatalog = bundledCatalog != null
                ? bundledCatalog
                : Resources.Load<TextAsset>("CardData/fsc_cards");
            if (fallbackCatalog != null && TryUse(fallbackCatalog.text, fallbackCatalog.name, out bundledError))
            {
                onLoaded?.Invoke(Current);
                yield break;
            }

            string error = $"No valid card catalog is available. Remote: {remoteError ?? "not configured"}; bundled: {bundledError ?? "missing"}.";
            Debug.LogError(error, this);
            onFailed?.Invoke(error);
        }

        private bool TryUse(string json, string source, out string error)
        {
            if (!CardCatalog.TryParse(json, out CardCatalog catalog, out error))
            {
                Debug.LogWarning($"Rejected card catalog from {source}: {error}", this);
                return false;
            }

            Current = catalog;
            CurrentSource = source;
            return true;
        }

        private bool TryReadCache(out string json)
        {
            try
            {
                json = File.Exists(CachePath) ? File.ReadAllText(CachePath) : null;
                return json != null;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Could not read card catalog cache: {exception.Message}", this);
                json = null;
                return false;
            }
        }

        private void TryWriteCache(string json)
        {
            try
            {
                string directory = Path.GetDirectoryName(CachePath);
                Directory.CreateDirectory(directory);
                string temporaryPath = CachePath + ".tmp";
                File.WriteAllText(temporaryPath, json);
                File.Copy(temporaryPath, CachePath, true);
                File.Delete(temporaryPath);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Could not cache card catalog: {exception.Message}", this);
            }
        }
    }
}
