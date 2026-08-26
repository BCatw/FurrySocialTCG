# UI 牌庫測試

開啟 `Assets/Scenes/DeckTestScene.unity` 並進入 Play Mode。牌組載入完成後，點擊「發一張牌」會從洗好的 `FSC-INITIAL` 牌組抽一張牌，並顯示在中央指定的 `RectTransform`。

## 元件職責

- `CardDeckController`：讀取牌組、洗牌、重置及抽牌，不處理 UI。
- `CardObject`：掛在 `CardGroup.prefab`，將單張 `CardDefinition` 顯示成顏色、形狀與點數。
- `DeckTester`：接收按鈕事件，將抽到的牌 prefab 實例化到 `Card Display Position`。

`DeckTester` 預設會用新牌取代上一張。若關閉 `Replace Displayed Card`，每次點擊都會保留已發的牌；此時建議把顯示位置換成含 `HorizontalLayoutGroup` 或自訂手牌排版元件的容器。

若需要重新產生測試場景，可使用 Unity 選單 `Tools > Furry Social Card > Build Deck Test Scene`。
