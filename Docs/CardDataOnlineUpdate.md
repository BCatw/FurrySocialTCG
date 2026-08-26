# 卡牌資料與線上更新

## 現行資料

`Assets/Resources/CardData/fsc_cards.json` 是遊戲隨附的離線基準資料。目前依 `FSC_牌庫表.xlsx` 的 `FSC` 分頁建立，共 48 張牌：4 種顏色、3 種形狀、4 種點數的所有組合。

這只是目前的遊戲資料模型，沒有套用花牌月份、牌名或役等規則。

## JSON 格式

遠端檔案必須是 UTF-8 JSON，根物件格式如下：

```json
{
  "schemaVersion": 1,
  "dataVersion": "2026.08.27.1",
  "cards": [
    {
      "id": "FSC-001",
      "serialNumber": 1,
      "color": "紅",
      "shape": "三角形",
      "points": 1
    }
  ],
  "decks": [
    {
      "id": "FSC-INITIAL",
      "displayName": "FSC 初始牌組",
      "cardIds": ["FSC-001"]
    }
  ]
}
```

- `schemaVersion`：程式資料格式版本；目前只能是 `1`。欄位結構改變時才遞增。
- `dataVersion`：每次上線資料都要更新的版本字串，建議使用日期加流水號。
- `id`：永久且唯一的卡牌識別碼。卡名或數值修改時不要變更；只有另一張新卡才使用新 ID。
- `serialNumber`：正整數且不可重複，供企劃辨識與排序。
- `color`、`shape`：目前保留為字串，允許後續增加類型，不受寫死的列舉限制。
- `points`：目前必須是正整數。
- `decks`：定義實際牌組；每個 `cardIds` 都必須引用存在的卡牌，而且同一牌組內不可重複。

## Unity 場景設定

1. 在啟動場景建立一個 GameObject，加入 `CardCatalogLoader`。
2. `Bundled Catalog` 可留空，程式會自動讀取 `Assets/Resources/CardData/fsc_cards.json`；也可以手動指定另一份基準檔。
3. 把公開的 HTTPS JSON 網址填入 `Remote Catalog Url`。
4. 遊戲啟動時呼叫 `StartCoroutine(loader.Load(...))`，成功後由 `loader.Current` 取得資料。

載入順序是遠端資料、上次成功快取、隨遊戲附帶資料。遠端資料只有通過完整驗證後才會寫入快取。

## 上線前準備

1. 準備可直接下載原始檔的 HTTPS 空間，例如自有 CDN、Cloudflare R2、AWS S3 或 GitHub Pages。
2. 伺服器回應應為 `application/json; charset=utf-8`，不要回傳需要登入的 HTML 頁面或分享預覽頁。
3. 網址最好固定，例如 `https://data.example.com/fsc/cards.json`；換版時覆蓋內容即可。
4. 若發布 WebGL，伺服器必須允許遊戲網域的 CORS；Android/iOS/桌面版通常不需要瀏覽器 CORS。
5. 正式環境應設短時間 CDN cache，或以 query version 控制刷新；更新後先用測試環境驗證再推正式網址。
6. 保留上一版 JSON，發生資料問題時可以立即回滾。

## 交付新資料給 Codex

提供新版 Excel 或線上試算表的可讀連結，並說明：

- 要讀取的分頁名稱。
- 欄位新增、刪除或改名的定義。
- 哪些欄位可以修改，哪些 ID 必須保持不變。
- 正式 JSON 的目標網址，或希望輸出的 JSON 檔案位置。
- 若需直接讀取私人 Google Sheet／Drive，請先安裝並連線相應的 Google Drive 外掛；公開 CSV／JSON 下載網址則不需要。

在資料尚未定案期間，建議每次更新都先讓 Codex產生差異摘要與驗證結果，再部署到正式網址。
