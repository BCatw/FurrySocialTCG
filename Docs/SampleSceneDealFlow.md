# SampleScene 發牌與資源交換流程

進入 `Assets/Scenes/SampleScene.unity` 的 Play Mode 後，系統保持等待，不會自動發牌。

1. 點擊 `StartBtn`。
2. 牌庫載入並洗牌。
3. 依序發 8 張至 `bafuda/Layout`。
4. 依序發 8 張至 `tefuda`，手牌會自動水平置中排列。
5. 進入玩家回合抽牌階段，此時 `DrawBtn` 才能使用。
6. 點擊 `DrawBtn` 抽一張手牌，接著進入資源交換階段並停用按鈕。
7. 點擊一張手牌選取；再次點同一張會取消，改點另一張則改選新牌。不能吃的場牌會變暗。
8. 點擊場地位置打出選取牌：
   - 沒有可吃牌時，打出的牌留在場上。
   - 只有一張可吃牌時，自動吃該牌。
   - 有多張可吃牌時，吃掉最靠近點擊位置的牌。
9. 吃牌後，打出牌與被吃牌會移入 `Resource`，並自動補一張場牌。補牌若能吃牌就繼續補牌，直到補出的牌不能吃或牌庫用盡。
10. 再次點擊 `StartBtn`，所有已發出與移入資源區的牌會清除，牌庫重新洗牌並重發。

`tefuda` 與 `Resource` 都由 `PlayerTurnDealController` 以水平置中、固定間距的方式排列，不依賴 Layout Group。可分別在 Inspector 調整 `Hand Card Spacing` 與 `Resource Card Spacing`。

目前吃牌規則預設為「顏色與形狀皆相同」。這是前期暫定值，可在 `Card Game Flow > Resource Exchange Controller > Match Rule` 切換成同色、同形或同色／同形其一；規則定案後只需替換集中在控制器內的判定。

流程元件位於場景的 `Card Game Flow`。預設逐張發牌間隔為 0.12 秒，連續補牌間隔為 0.18 秒，皆可在 Inspector 修改。

## 卡牌移動演出

所有跨區域的卡牌移動都使用 DOTween：

- 初始發牌、玩家抽牌與吃牌後補牌，會先在 `Deck` 顯示抽到的正面卡牌。
- 卡牌預設在 `Deck` 停留 0.5 秒，再移動至場牌或手牌位置。
- 手牌打到場上，以及卡牌移入 `Resource`，也會播放移動動畫。
- 預設移動時間為 0.2 秒，Ease 使用 `InOutQuad`。
- 動畫期間會暫停資源交換互動，防止連點造成卡牌狀態交錯。

可在 `Card Game Flow > Player Turn Deal Controller` 調整：

- `Deck Reveal Seconds`：Deck 顯示時間。
- `Card Move Duration Seconds`：卡片移動時間。
- `Deal Interval Seconds`：初始連續發牌時，每張牌完成後的額外間隔。

若場景 UI 階層被重新命名或引用遺失，可使用 Unity 選單 `Tools > Furry Social Card > Setup Sample Scene Deal Flow` 重新接線。

