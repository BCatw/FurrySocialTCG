# SampleScene 初始發牌流程

進入 `Assets/Scenes/SampleScene.unity` 的 Play Mode 後，系統保持等待，不會自動發牌。

1. 點擊 `StartBtn`。
2. 牌庫載入並洗牌。
3. 依序發 8 張至 `bafuda/Layout`。
4. 依序發 8 張至 `tefuda`，手牌會自動水平置中排列。
5. 進入玩家回合的抽牌階段，此時 `DrawBtn` 才能使用。
6. 每次點擊 `DrawBtn`，再抽一張至玩家手牌。
7. 再次點擊 `StartBtn`，目前發出的牌會收回，牌庫重新洗牌並重發 8 張場牌與 8 張手牌。

流程由 `PlayerTurnDealController` 管理，場景內的元件位於 `Card Game Flow`。預設逐張發牌間隔為 0.12 秒，可在 Inspector 修改 `Deal Interval Seconds`。

若場景 UI 階層被重新命名或引用遺失，可使用 Unity 選單 `Tools > Furry Social Card > Setup Sample Scene Deal Flow` 重新接線。
