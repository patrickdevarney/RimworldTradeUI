using Verse;
using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using RimWorld.Planet;
using System.Reflection;

/*
 * TODO list for release
 * 
 * 
 * TODO for future
 * Change colors of buttons at bottom (Accept green, Cancel red, Reset, orange/default)
 * 
 * Fix unintuitive behavior that if I sell/buy all of an item, it is strange to reverse this move
 * * Make transfer numbers always postive
 * * If user types a positive number on our side, interpret it as negative
 */

namespace TradeUI
{
    [StaticConstructorOnStartup]
    static class TradeUIPatches
    {
        static TradeUIPatches()
        {
            //Harmony.DEBUG = true;
            new Harmony("rimworld.hobtook.tradeui").PatchAll();
        }
    }

    [HarmonyPatch(typeof(RimWorld.Dialog_Trade), "PostOpen")]
    static class Harmony_DialogTrade_PostOpen
    {
        static void Prefix()
        {
            TradeUIParameters.Singleton.Reset();
        }
    }

    [HarmonyPatch]
    static class PatchTradingWindowWidth
    {
        static void Postfix(ref Vector2 __result)
        {
            __result.x = Mathf.Min(UI.screenWidth, __result.x + 360);
        }

        public static bool Prepare()
        {
            //return ModLister.HasActiveModWithName("Multiplayer");
            return LoadedModManager.RunningModsListForReading.Any(
                m => m.PackageId == "rwmt.Multiplayer".ToLowerInvariant()
                );
        }

        public static MethodInfo TargetMethod()
        {
            System.Type multiplayerTradeUIType = System.Type.GetType("Multiplayer.Client.TradingWindow, Multiplayer");
            var methodInfo = AccessTools.Property(multiplayerTradeUIType, "InitialSize").GetGetMethod();
            if (methodInfo == null)
                Log.Error("[TradeUI] failed to find multiplayer method info");
            return methodInfo;
        }
    }

    [HarmonyPatch(typeof(RimWorld.Dialog_Trade), "InitialSize", MethodType.Getter)]
    static class Harmony_DialogTrade_InitialSize
    {
        static void Postfix(ref Vector2 __result)
        {
            // Make screen wider (without being bigger than the user's screen)
            __result.x = Mathf.Min(UI.screenWidth, __result.x + 360);
        }
    }

    [HarmonyPatch(typeof(RimWorld.Dialog_Trade), "DoWindowContents")]
    static class Harmony_DialogTrade_DoWindowContents
    {
        // This one sets up a GUI groups, fonts, labels for player faction, negotiator, kind of trader,
        // if in gift mode (something with Royalty DLC?),
        // Then call TradeUI.DrawTradeableRow to draw the currency (silver)
        // Then call FillMainRect() and pass it a sub-rect of our full UI rect
        // Then draw the accept buttons and react to user pressing buttons
        static bool Prefix(ref UnityEngine.Rect inRect, ref Dialog_Trade __instance)
        {
            var myThis = __instance;
            //Log.Message($"[TradeUI] Dialog_Trade.DoWindowContents prefix");
            if (__instance.playerIsCaravan)
            {
                CaravanUIUtility.DrawCaravanInfo(new CaravanUIUtility.CaravanInfo(__instance.MassUsage, __instance.MassCapacity, __instance.cachedMassCapacityExplanation, __instance.TilesPerDay, __instance.cachedTilesPerDayExplanation, __instance.DaysWorthOfFood, __instance.ForagedFoodPerDay, __instance.cachedForagedFoodPerDayExplanation, __instance.Visibility, __instance.cachedVisibilityExplanation, -1f, -1f, null), null, __instance.Tile, null, -9999f, new Rect(12f, 0f, inRect.width - 24f, 40f), true, null, false);
                inRect.yMin += 52f;
            }
            TradeSession.deal.UpdateCurrencyCount();
            GUI.BeginGroup(inRect);
            inRect = inRect.AtZero();
            //Log.Message($"[TradeUI] Harmony_DialogTrade_DoWindowContents inRect width {inRect.width})");
            TransferableUIUtility.DoTransferableSorters(__instance.sorter1, __instance.sorter2, delegate (TransferableSorterDef x)
            {
                myThis.sorter1 = x;
                myThis.CacheTradeables();
            }, delegate (TransferableSorterDef x)
            {
                myThis.sorter2 = x;
                myThis.CacheTradeables();
            });
            
            // Calculate space for left/right rects
            const float FOOTER_HEIGHT = 150;
            const float BUTTON_HEIGHT = 55;
            Rect twoColumnRect = new Rect(0f, inRect.yMin + TransferableUIUtility.SortersHeight, inRect.width, inRect.height - FOOTER_HEIGHT - TransferableUIUtility.SortersHeight);

            // DRAW THE LEFT/RIGHT AREAS
            __instance.FillMainRect(twoColumnRect);

            Rect footerSilverRect = new Rect(0f, inRect.height - FOOTER_HEIGHT + 3, inRect.width, FOOTER_HEIGHT - BUTTON_HEIGHT - 13);//FOOTER_HEIGHT - 55);
            if (__instance.cachedCurrencyTradeable != null)
            {
                GUI.color = Color.gray;
                Widgets.DrawLineHorizontal(0f, footerSilverRect.yMin, inRect.width);
                GUI.color = Color.white;
                Harmony_DialogTrade_FillMainRect.DrawCurrencyTradableRow(new Rect(0f, footerSilverRect.yMin + 5, footerSilverRect.width, footerSilverRect.height), __instance.cachedCurrencyTradeable, true);
            }

            // Draw bottom buttons
            Text.Font = GameFont.Small;
            Rect buttonsRect = new Rect(inRect.width / 2f - Dialog_Trade.AcceptButtonSize.x / 2f,
                inRect.height - BUTTON_HEIGHT,
                Dialog_Trade.AcceptButtonSize.x,
                Dialog_Trade.AcceptButtonSize.y);
            if (Widgets.ButtonText(buttonsRect, TradeSession.giftMode ? ("OfferGifts".Translate() + " (" + FactionGiftUtility.GetGoodwillChange(TradeSession.deal.AllTradeables, TradeSession.trader.Faction).ToStringWithSign() + ")") : "AcceptButton".Translate(), true, true, true))
            {
                System.Action action = delegate ()
                {
                    bool flag;
                    if (TradeSession.deal.TryExecute(out flag))
                    {
                        if (flag)
                        {
                            Verse.Sound.SoundStarter.PlayOneShotOnCamera(SoundDefOf.ExecuteTrade, null);
                            //SoundDefOf.ExecuteTrade.PlayOneShotOnCamera(null);
                            Caravan caravan = TradeSession.playerNegotiator.GetCaravan();
                            if (caravan != null)
                            {
                                caravan.RecacheImmobilizedNow();
                            }
                            myThis.Close(false);
                            return;
                        }
                        myThis.Close(true);
                    }
                };
                if (TradeSession.deal.DoesTraderHaveEnoughSilver())
                {
                    action();
                }
                else
                {
                    __instance.FlashSilver();
                    //SoundDefOf.ClickReject.PlayOneShotOnCamera(null);
                    Verse.Sound.SoundStarter.PlayOneShotOnCamera(SoundDefOf.ClickReject, null);
                    Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("ConfirmTraderShortFunds".Translate(), action, false, null, WindowLayer.Dialog));
                }
                Event.current.Use();
            }
            if (Widgets.ButtonText(new Rect(buttonsRect.x - 10f - Dialog_Trade.OtherBottomButtonSize.x, buttonsRect.y, Dialog_Trade.OtherBottomButtonSize.x, Dialog_Trade.OtherBottomButtonSize.y), "ResetButton".Translate(), true, true, true))
            {
                //SoundDefOf.Tick_Low.PlayOneShotOnCamera(null);
                Verse.Sound.SoundStarter.PlayOneShotOnCamera(SoundDefOf.Tick_Low, null);
                TradeSession.deal.Reset();
                __instance.CacheTradeables();
                __instance.CountToTransferChanged();
            }
            if (Widgets.ButtonText(new Rect(buttonsRect.xMax + 10f, buttonsRect.y, Dialog_Trade.OtherBottomButtonSize.x, Dialog_Trade.OtherBottomButtonSize.y), "CancelButton".Translate(), true, true, true))
            {
                __instance.Close(true);
                Event.current.Use();
            }
            float y = Dialog_Trade.OtherBottomButtonSize.y;
            Rect rect5 = new Rect(inRect.width - y, buttonsRect.y, y, y);
            if (Widgets.ButtonImageWithBG(rect5, Dialog_Trade.ShowSellableItemsIcon, new Vector2?(new Vector2(32f, 32f))))
            {
                Find.WindowStack.Add(new Dialog_SellableItems(TradeSession.trader));
            }
            TooltipHandler.TipRegionByKey(rect5, "CommandShowSellableItemsDesc");
            Faction faction = TradeSession.trader.Faction;
            if (faction != null && !__instance.giftsOnly && !faction.def.permanentEnemy)
            {
                Rect rect6 = new Rect(rect5.x - y - 4f, buttonsRect.y, y, y);
                if (TradeSession.giftMode)
                {
                    if (Widgets.ButtonImageWithBG(rect6, Dialog_Trade.TradeModeIcon, new Vector2?(new Vector2(32f, 32f))))
                    {
                        TradeSession.giftMode = false;
                        TradeSession.deal.Reset();
                        __instance.CacheTradeables();
                        __instance.CountToTransferChanged();
                        //SoundDefOf.Tick_High.PlayOneShotOnCamera(null);
                        Verse.Sound.SoundStarter.PlayOneShotOnCamera(SoundDefOf.Tick_High, null);
                    }
                    TooltipHandler.TipRegionByKey(rect6, "TradeModeTip");
                }
                else
                {
                    if (Widgets.ButtonImageWithBG(rect6, Dialog_Trade.GiftModeIcon, new Vector2?(new Vector2(32f, 32f))))
                    {
                        TradeSession.giftMode = true;
                        TradeSession.deal.Reset();
                        __instance.CacheTradeables();
                        __instance.CountToTransferChanged();
                        //SoundDefOf.Tick_High.PlayOneShotOnCamera(null);
                        Verse.Sound.SoundStarter.PlayOneShotOnCamera(SoundDefOf.Tick_High, null);
                    }
                    TooltipHandler.TipRegionByKey(rect6, "GiftModeTip", faction.Name);
                }
            }
            GUI.EndGroup();

            return false;
        }
    }

    [HarmonyPatch(typeof(RimWorld.Dialog_Trade), "FillMainRect")]
    public static class Harmony_DialogTrade_FillMainRect
    {
        // We may need to override this
        static bool Prefix(ref UnityEngine.Rect mainRect, ref List<Tradeable> ___cachedTradeables, ref Dialog_Trade __instance)
        {
            //Log.Message($"[TradeUI] Dialog_Trade.FillMainRect prefix");
            //Log.Message($"[TradeUI] FillMainRect ({mainRect.x}, {mainRect.y},{mainRect.width},{mainRect.height})");

            // Draw headers
            float halfWidth = mainRect.width / 2f;
            // Draw left header
            Rect leftHeaderRect = new Rect(0, mainRect.y, halfWidth, 85);
            //Log.Message($"[TradeUI] leftHeaderRect ({leftHeaderRect.x}, {leftHeaderRect.y},{leftHeaderRect.width},{leftHeaderRect.height})");
            // Draw colony name
            GUI.BeginGroup(leftHeaderRect);
            var colonyNameRect = new Rect(0, 0, leftHeaderRect.width, leftHeaderRect.height);
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.UpperCenter;
            Widgets.Label(colonyNameRect, Faction.OfPlayer.Name.Truncate(colonyNameRect.width, null));
            // Draw negotiator name
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperCenter;
            Widgets.Label(new Rect(0, 30f, leftHeaderRect.width, leftHeaderRect.height - 30),
                "NegotiatorTradeDialogInfo".Translate(TradeSession.playerNegotiator.Name.ToStringFull,
                TradeSession.playerNegotiator.GetStatValue(StatDefOf.TradePriceImprovement,
                true).ToStringPercent()));

            // Draw money
            if (false && __instance.cachedCurrencyTradeable != null)
            {
                MyDrawTradableRow(new Rect(0f, 58f, leftHeaderRect.width, 30f), __instance.cachedCurrencyTradeable, -1, true);
            }
            else
            {
                leftHeaderRect.height -= 30;
            }

            // TODO: fix one pixel missing between left/right horizontal lines (but drawing it all in one go caused scroll bar to render on top of white line)
            GUI.color = Color.gray;
            Widgets.DrawLineHorizontal(0f, leftHeaderRect.height - 1, leftHeaderRect.width);
            GUI.color = Color.white;

            GUI.EndGroup();

            // Draw right header
            Rect rightHeaderRect = new Rect(halfWidth, mainRect.y, halfWidth, 85);
            //Log.Message($"[TradeUI] rightHeaderRect ({rightHeaderRect.x}, {rightHeaderRect.y},{rightHeaderRect.width},{rightHeaderRect.height})");
            // Draw trader name
            GUI.BeginGroup(rightHeaderRect);
            var traderNameRect = new Rect(0, 0, rightHeaderRect.width, rightHeaderRect.height);
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.UpperCenter;
            string text = TradeSession.trader.TraderName;
            if (Text.CalcSize(text).x > traderNameRect.width)
            {
                Text.Font = GameFont.Small;
                text = text.Truncate(traderNameRect.width, null);
            }
            Widgets.Label(traderNameRect, text);
            // Draw type of trader
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperCenter;
            Widgets.Label(new Rect(0, 30f, rightHeaderRect.width, rightHeaderRect.height - 30), TradeSession.trader.TraderKind.LabelCap);

            // Draw money
            if (false && __instance.cachedCurrencyTradeable != null)
            {
                MyDrawTradableRow(new Rect(0f, 58f, leftHeaderRect.width, 30f), __instance.cachedCurrencyTradeable, -1, false);
            }
            else
            {
                rightHeaderRect.height -= 30;
            }

            GUI.color = Color.gray;
            Widgets.DrawLineHorizontal(0f, rightHeaderRect.height - 1, rightHeaderRect.width);
            GUI.color = Color.white;

            GUI.EndGroup();

            // Draw vertical divider
            /*GUI.BeginGroup(mainRect);
            Widgets.DrawLineVertical(halfWidth - 1, leftHeaderRect.height, mainRect.height - leftHeaderRect.height);
            GUI.EndGroup();*/

            // Calculate scroll height
            float leftHeight = 6f;
            float rightHeight = 6f;
            foreach(var entry in ___cachedTradeables)
            {
                if (entry.thingsColony != null && entry.thingsColony.Count > 0)
                    leftHeight += 30f;
                if (entry.thingsTrader != null && entry.thingsTrader.Count > 0)
                    rightHeight += 30f;
            }

            // Draw left view
            Text.Font = GameFont.Small;
            // Start scroll rect down a bit vertically
            Rect leftScrollRect = new Rect(0, mainRect.y + leftHeaderRect.height, halfWidth, mainRect.height - leftHeaderRect.height);
            Rect leftInsideScrollRect = new Rect(0, 0, (leftScrollRect.width - 16f), leftHeight);
            BeginScrollViewForceDraw(leftScrollRect, ref TradeUIParameters.Singleton.scrollPositionLeft, leftInsideScrollRect, true);
            float num = 6f;
            float num2 = TradeUIParameters.Singleton.scrollPositionLeft.y - 30f;
            float num3 = TradeUIParameters.Singleton.scrollPositionLeft.y + leftScrollRect.height;
            int num4 = 0;
            for (int i = 0; i < ___cachedTradeables.Count; i++)
            {
                // Only draw stuff we have
                if (___cachedTradeables[i].thingsColony == null || ___cachedTradeables[i].thingsColony.Count == 0)
                    continue;

                if (num > num2 && num < num3)
                {
                    Rect rect = new Rect(0, num, leftInsideScrollRect.width, 30f);
                    int countToTransfer = ___cachedTradeables[i].CountToTransfer;
                    //RimWorld.TradeUI.DrawTradeableRow(rect, ___cachedTradeables[i], num4);
                    MyDrawTradableRow(rect, ___cachedTradeables[i], num4, true);
                    if (countToTransfer != ___cachedTradeables[i].CountToTransfer)
                    {
                        __instance.CountToTransferChanged();
                    }
                }
                num += 30f;
                num4++;
            }
            Widgets.EndScrollView();

            // Draw right view
            Rect rightScrollRect = new Rect(halfWidth, mainRect.y + rightHeaderRect.height, halfWidth, mainRect.height - rightHeaderRect.height);
            Rect rightInnerRect = new Rect(0, 0, (rightScrollRect.width - 16f), rightHeight);
            BeginScrollViewForceDraw(rightScrollRect, ref TradeUIParameters.Singleton.scrollPositionRight, rightInnerRect, true);
            num = 6f;
            num2 = TradeUIParameters.Singleton.scrollPositionRight.y - 30f;
            num3 = TradeUIParameters.Singleton.scrollPositionRight.y + rightScrollRect.height;
            num4 = 0;
            for (int i = 0; i < ___cachedTradeables.Count; i++)
            {
                // Only draw stuff they have
                if (___cachedTradeables[i].thingsTrader == null || ___cachedTradeables[i].thingsTrader.Count == 0)
                    continue;

                if (num > num2 && num < num3)
                {
                    Rect rect = new Rect(0, num, rightInnerRect.width, 30f);
                    int countToTransfer = ___cachedTradeables[i].CountToTransfer;
                    MyDrawTradableRow(rect, ___cachedTradeables[i], num4, false);
                    //RimWorld.TradeUI.DrawTradeableRow(rect, ___cachedTradeables[i], num4);
                    if (countToTransfer != ___cachedTradeables[i].CountToTransfer)
                    {
                        __instance.CountToTransferChanged();
                    }

                }
                num += 30f;
                num4++;
            }
            Widgets.EndScrollView();

            // Skip vanilla behavior
            return false;
        }

        static void BeginScrollViewForceDraw(Rect outRect, ref Vector2 scrollPosition, Rect viewRect, bool showScrollbars = true)
        {
            if (Widgets.mouseOverScrollViewStack.Count > 0)
            {
                Widgets.mouseOverScrollViewStack.Push(Widgets.mouseOverScrollViewStack.Peek() && outRect.Contains(Event.current.mousePosition));
            }
            else
            {
                Widgets.mouseOverScrollViewStack.Push(outRect.Contains(Event.current.mousePosition));
            }
            if (showScrollbars)
            {
                scrollPosition = GUI.BeginScrollView(outRect, scrollPosition, viewRect, false, true);
                return;
            }
            scrollPosition = GUI.BeginScrollView(outRect, scrollPosition, viewRect, GUIStyle.none, GUIStyle.none);
        }

        public static void MyDrawTradableRow(Rect mainRect, Tradeable trad, int index, bool isOurs)
        {
            if (Mathf.Abs(index) % 2 == 1)
            {
                Widgets.DrawLightHighlight(mainRect);
            }

            // Hack to prevent formatting for currency
            if (index < 0)
                mainRect.width -= 16;

            Text.Font = GameFont.Small;
            GUI.BeginGroup(mainRect);
            float xPosition = mainRect.width;

            // Vanilla draws this right-left for some reason
            // our side, should read this
            // LEFT  ---------- RIGHT
            //Icon, info button, name, animal bond/ridability, owned amount, sell price, amount selling, arrows point right

            // their side should read
            // LEFT --------- RIGHT
            // Icon, info button, name, animal bond/ridability, owned amount, buy price, amount buying, arrows point left

            // TDOO: handle somewhere in the trade what happens when I select (sell 10 steel + buy 5 steel)

            const float COST_WIDTH = 90f;
            const float TRANSFER_WIDTH = 160f;
            const float OWNED_AMOUNT_WIDTH = 75f;

            if (!trad.TraderWillTrade)
            {
                // Since no price will be shown, we will occupy more space
                xPosition -= (TRANSFER_WIDTH + COST_WIDTH);
                Rect rect5 = new Rect(xPosition, 0f, TRANSFER_WIDTH + COST_WIDTH, mainRect.height);
                // But don't actually consume this space since the price will be "drawn" as empty
                xPosition += COST_WIDTH;

                RimWorld.TradeUI.DrawWillNotTradeText(rect5, "TraderWillNotTrade".Translate());
            }
            else if (ModsConfig.IdeologyActive && TransferableUIUtility.TradeIsPlayerSellingToSlavery(trad, TradeSession.trader.Faction) && !new HistoryEvent(HistoryEventDefOf.SoldSlave, TradeSession.playerNegotiator.Named(HistoryEventArgsNames.Doer)).DoerWillingToDo())
            {
                // Since no price will be shown, will we occupy more space
                xPosition -= (TRANSFER_WIDTH + COST_WIDTH);
                Rect rect5 = new Rect(xPosition, 0f, TRANSFER_WIDTH + COST_WIDTH, mainRect.height);
                // But don't actually consume this space since the price will be "drawn" as empty
                xPosition += COST_WIDTH;

                RimWorld.TradeUI.DrawWillNotTradeText(rect5, "NegotiatorWillNotTradeSlaves".Translate(TradeSession.playerNegotiator));
                if (Mouse.IsOver(rect5))
                {
                    Widgets.DrawHighlight(rect5);
                    TooltipHandler.TipRegion(rect5, "NegotiatorWillNotTradeSlavesTip".Translate(TradeSession.playerNegotiator, TradeSession.playerNegotiator.Ideo.name));
                }
            }
            else
            {
                xPosition -= TRANSFER_WIDTH;
                Rect rect5 = new Rect(xPosition, 0f, TRANSFER_WIDTH, mainRect.height);
                // Drawing left/right arrows and transfer amount
                bool flash = Time.time - Dialog_Trade.lastCurrencyFlashTime < 1f && trad.IsCurrency;
                // Prevent drawing both left/right arrows
                if (isOurs)
                {
                    TradeUIParameters.Singleton.isDrawingColonyItems = true;
                }
                else
                {
                    TradeUIParameters.Singleton.isDrawingColonyItems = false;
                }
                TransferableUIUtility.DoCountAdjustInterface(rect5, trad, index, trad.GetMinimumToTransfer(), trad.GetMaximumToTransfer(), flash, null, false);
            }

            int ownedAmount = trad.CountHeldBy(isOurs ? Transactor.Colony : Transactor.Trader);
            if ((isOurs && ownedAmount != 0) || (!isOurs && ownedAmount != 0 && trad.IsThing))
            {
                // draw sell/buy price
                xPosition -= COST_WIDTH;
                Rect rect6 = new Rect(xPosition, 0f, COST_WIDTH, mainRect.height);
                Text.Anchor = TextAnchor.MiddleRight;
                RimWorld.TradeUI.DrawPrice(rect6, trad, isOurs ? TradeAction.PlayerSells : TradeAction.PlayerBuys);

                // draw owned amount
                xPosition -= OWNED_AMOUNT_WIDTH;
                Rect rect7 = new Rect(xPosition, 0f, OWNED_AMOUNT_WIDTH, mainRect.height);
                if (Mouse.IsOver(rect7))
                {
                    Widgets.DrawHighlight(rect7);
                }
                Text.Anchor = TextAnchor.MiddleRight;
                Rect rect8 = rect7;
                rect8.xMin += 5f;
                rect8.xMax -= 5f;
                Widgets.Label(rect8, ownedAmount.ToStringCached());
                TooltipHandler.TipRegionByKey(rect7, isOurs ? "ColonyCount" : "TraderCount");
            }
            else
            {
                xPosition -= (OWNED_AMOUNT_WIDTH + COST_WIDTH);
            }

            // draw animal bond/ridability
            TransferableUIUtility.DoExtraAnimalIcons(trad, mainRect, ref xPosition);

            // draw Ideaology something
            if (ModsConfig.IdeologyActive)
            {
                TransferableUIUtility.DrawCaptiveTradeInfo(trad, TradeSession.trader, mainRect, ref xPosition);
            }

            // draw icon, ID icon, name
            // Calculate rect for icon + info button + name
            Rect idRect = new Rect(0f, 0f, xPosition, mainRect.height);
            TransferableUIUtility.DrawTransferableInfo(trad, idRect, trad.TraderWillTrade ? Color.white : RimWorld.TradeUI.NoTradeColor);

            // Cleanup
            GenUI.ResetLabelAlign();
            GUI.EndGroup();
        }

        public static void DrawCurrencyTradableRow(Rect rect, Tradeable trad, bool highlight)
        {
            if (highlight)
            {
                Widgets.DrawLightHighlight(rect);
            }

            // [        icon [i] Silver     my amount      < transfer amount        their amount            ]

            Text.Font = GameFont.Medium;
            GUI.BeginGroup(rect);

            // Draw transfer amount
            Rect transferRect = new Rect(rect.center.x - (240 / 2), 0f, 240f, rect.height);
            bool flash = Time.time - Dialog_Trade.lastCurrencyFlashTime < 1f && trad.IsCurrency;
            TransferableUIUtility.DoCountAdjustInterface(transferRect, trad, 0, trad.GetMinimumToTransfer(), trad.GetMaximumToTransfer(), flash, null, false);
            //GUI.Label(transferRect, "||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||");

            // Draw owned amount
            int ourAmount = trad.CountHeldBy(Transactor.Colony);
            if (ourAmount != 0)
            {
                Text.Anchor = TextAnchor.MiddleRight;
                float ourAmountWidth = 100f;
                var ourRect = new Rect(rect.center.x - (ourAmountWidth / 2) - 300, 0f, ourAmountWidth, rect.height); ;
                if (Mouse.IsOver(ourRect))
                {
                    Widgets.DrawHighlight(ourRect);
                }
                Rect rect8 = ourRect;
                rect8.xMin += 5f;
                rect8.xMax -= 5f;
                Widgets.Label(rect8, ourAmount.ToStringCached());
                TooltipHandler.TipRegionByKey(ourRect, "ColonyCount");
            }

            // Draw their amount
            int theirAmount = trad.CountHeldBy(Transactor.Trader);
            if (theirAmount != 0 && trad.IsThing)
            {
                Text.Anchor = TextAnchor.MiddleLeft;
                float theirAmountWidth = 100f;
                var theirRect = new Rect(rect.center.x - (theirAmountWidth / 2) + 300, 0f, theirAmountWidth, rect.height);
                if (Mouse.IsOver(theirRect))
                {
                    Widgets.DrawHighlight(theirRect);
                }
                Rect rect3 = theirRect;
                rect3.xMin += 5f;
                rect3.xMax -= 5f;
                Widgets.Label(rect3, theirAmount.ToStringCached());
                TooltipHandler.TipRegionByKey(theirRect, "TraderCount");
            }

            // Draw icon, info, name
            float num = rect.width;
            //Log.Message($" full silver rect = {rect.x}, {rect.y}, {rect.width}, {rect.height}");
            Rect idRect = new Rect(0f, 0, num, rect.height);
            //Log.Message($" silver id rect = {idRect.x}, {idRect.y}, {idRect.width}, {idRect.height}");
            //TransferableUIUtility.DrawTransferableInfo(trad, idRect, trad.TraderWillTrade ? Color.white : RimWorld.TradeUI.NoTradeColor);
            MyDrawTransferableInfoSilver(trad, idRect, trad.TraderWillTrade ? Color.white : RimWorld.TradeUI.NoTradeColor);
            //GUI.Label(idRect, "||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||");
            GenUI.ResetLabelAlign();
            GUI.EndGroup();
        }

        static void MyDrawTransferableInfoSilver(Transferable trad, Rect idRect, Color labelColor)
        {
            if (!trad.HasAnyThing && trad.IsThing)
            {
                return;
            }
            if (Mouse.IsOver(idRect))
            {
                Widgets.DrawHighlight(idRect);
            }
            Rect rect = new Rect(0f, (idRect.height - 27) / 2f, 27f, 27f);
            if (trad.IsThing)
            {
                Widgets.ThingIcon(rect, trad.AnyThing, 1f, null);
            }
            else
            {
                trad.DrawIcon(rect);
            }
            if (trad.IsThing)
            {
                Widgets.InfoCardButton(40f, (idRect.height / 2f) - 12f, trad.AnyThing);
            }
            Text.Anchor = TextAnchor.MiddleLeft;
            Rect rect2 = new Rect(80f, 0f, idRect.width - 80f, idRect.height);
            Text.WordWrap = false;
            GUI.color = labelColor;
            Widgets.Label(rect2, trad.LabelCap);
            GUI.color = Color.white;
            Text.WordWrap = true;
            if (Mouse.IsOver(idRect))
            {
                Transferable localTrad = trad;
                TooltipHandler.TipRegion(idRect, new TipSignal(delegate ()
                {
                    if (!localTrad.HasAnyThing && localTrad.IsThing)
                    {
                        return "";
                    }
                    string text = localTrad.LabelCap;
                    string tipDescription = localTrad.TipDescription;
                    if (!tipDescription.NullOrEmpty())
                    {
                        text = text + ": " + tipDescription + TransferableUIUtility.ContentSourceDescription(localTrad.AnyThing);
                    }
                    return text;
                }, localTrad.GetHashCode()));
            }
        }
    }

    [HarmonyPatch(typeof(RimWorld.TransferableUIUtility), "DoCountAdjustInterfaceInternal")]
    static class Harmony_TransferableUIUtility_DoCountAdjustInterfaceInternal
    {
        static bool Prefix(Rect rect, Transferable trad, int index, int min, int max, bool flash, bool readOnly)
        {
            //Log.Message("[TradeUI] TransferableUIUtility.DoCountAdjustInterfaceInternal prefix");

            // Skip this behavior if we aren't in a trade UI (transport pod)
            if (!Find.WindowStack.IsOpen<Dialog_Trade>())
            {
                //Log.Message("[TradeUI] Dialog_Trade window is not open. Drawing vanilla UI buttons");
                return true;
            }

            rect = rect.Rounded();

            const float EDGE_MARGIN = 7f;
            const float ARROW_MARGIN = 10f;
            // theirs = 135 wide
            // [60 arrows][ARROW_MARGIN][60 text box][EDGE_MARGIN]
            // ours
            // [60 text box][ARROW_MARGIN][60 arrows][EDGE_MARGIN]
            // Need a width of 125

            // Theirs 
            //[30 button][ARROW_MARGIN][60 text box][ARROW_MARGIN][30 button][EDGE_MARGIN]
            // I am making this ARROW_MARGIN pixels wider than before

            Rect miniRect = (!trad.Interactive || readOnly) ? rect : new Rect(rect.xMax - ARROW_MARGIN - EDGE_MARGIN - 120f, rect.center.y - 12.5f, 120f + ARROW_MARGIN, 25f).Rounded();

            // TODO: WHAT IS THIS? Are these pixel coords correct?
            /*if (flash)
            {
                if (TradeUIParameters.Singleton.isDrawingColonyItems)
                    GUI.DrawTexture(new Rect(rect.x, rect.center.y - 12.5f, 90f, 25f).Rounded(), TransferableUIUtility.FlashTex);
                else
                    GUI.DrawTexture(miniRect, TransferableUIUtility.FlashTex);
            }*/

            TransferableOneWay transferableOneWay = trad as TransferableOneWay;
            bool flag = transferableOneWay != null && transferableOneWay.HasAnyThing && transferableOneWay.AnyThing is Pawn && transferableOneWay.MaxCount == 1;
            if (!trad.Interactive || readOnly)
            {
                if (flag)
                {
                    bool flag2 = trad.CountToTransfer != 0;
                    Widgets.Checkbox(rect.position, ref flag2, 24f, true, false, null, null);
                }
                else
                {
                    GUI.color = ((trad.CountToTransfer == 0) ? TransferableUIUtility.ZeroCountColor : Color.white);
                    Text.Anchor = TextAnchor.MiddleCenter;
                    // Make transfer amount always positive?
                    //Widgets.Label(miniRect, Mathf.Abs(trad.CountToTransfer).ToStringCached());
                    Widgets.Label(miniRect, trad.CountToTransfer.ToStringCached());
                    //GUI.Label(miniRect, "|||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||");
                }
            }
            else if (flag)
            {
                bool flag3 = trad.CountToTransfer != 0;
                bool flag4 = flag3;
                Widgets.Checkbox(miniRect.position, ref flag4, 24f, false, true, null, null);
                if (flag4 != flag3)
                {
                    if (flag4)
                    {
                        trad.AdjustTo(trad.GetMaximumToTransfer());
                    }
                    else
                    {
                        trad.AdjustTo(trad.GetMinimumToTransfer());
                    }
                }
            }
            else
            {
                Rect textRect = miniRect.ContractedBy(2f);
                if (!TradeUIParameters.Singleton.isDrawingColonyItems)
                {
                    // Leave room for arrows
                    textRect.xMin += 60f + ARROW_MARGIN;
                }
                textRect.width = 55f;
                
                // Draw transfer amount
                // TODO make this always show positive numbers
                int countToTransfer = trad.CountToTransfer;
                string editBuffer = trad.EditBuffer;
                Widgets.TextFieldNumeric<int>(textRect, ref countToTransfer, ref editBuffer, (float)min, (float)max);
                trad.AdjustTo(countToTransfer);
                trad.EditBuffer = editBuffer;
            }

            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
            if (trad.Interactive && !flag && !readOnly)
            {
                TransferablePositiveCountDirection positiveCountDirection = trad.PositiveCountDirection;
                int num = (positiveCountDirection == TransferablePositiveCountDirection.Source) ? 1 : -1;
                int num2 = GenUI.CurrentAdjustmentMultiplier();

                // Fix VANILLA bug that items with durability cause "<<" and ">>" to appear even when there is only one of them
                // e.g. I have "Flak Pants (normal) 98%" and they have "Flak Pants (normal)" the game will incorrectly show ">>" even though it stacks our pants in entirely different rows
                bool onlyHasOneItem = false;
                if (TradeUIParameters.Singleton.isDrawingColonyItems)
                    onlyHasOneItem = trad.GetMinimumToTransfer() == -1;
                else
                    onlyHasOneItem = trad.GetMaximumToTransfer() == 1;

                if (!TradeUIParameters.Singleton.isDrawingColonyItems && trad.CanAdjustBy(num * num2).Accepted)
                {
                    Rect arrowRect = new Rect(miniRect.x + 30f, rect.y, 30f, rect.height);
                    if (onlyHasOneItem)
                    {
                        arrowRect.x -= arrowRect.width;
                        arrowRect.width += arrowRect.width;
                    }
                    if (Widgets.ButtonText(arrowRect, "<", true, true, true))
                    {
                        trad.AdjustBy(num * num2);
                        Verse.Sound.SoundStarter.PlayOneShotOnCamera(SoundDefOf.Tick_High, null);
                    }
                    if (!onlyHasOneItem)
                    {
                        string label = "<<";
                        int? num3 = null;
                        int num4 = 0;
                        for (int i = 0; i < TransferableUIUtility.stoppingPoints.Count; i++)
                        {
                            TransferableCountToTransferStoppingPoint transferableCountToTransferStoppingPoint = TransferableUIUtility.stoppingPoints[i];
                            if (positiveCountDirection == TransferablePositiveCountDirection.Source)
                            {
                                if (trad.CountToTransfer < transferableCountToTransferStoppingPoint.threshold && (transferableCountToTransferStoppingPoint.threshold < num4 || num3 == null))
                                {
                                    label = transferableCountToTransferStoppingPoint.leftLabel;
                                    num3 = new int?(transferableCountToTransferStoppingPoint.threshold);
                                }
                            }
                            else if (trad.CountToTransfer > transferableCountToTransferStoppingPoint.threshold && (transferableCountToTransferStoppingPoint.threshold > num4 || num3 == null))
                            {
                                label = transferableCountToTransferStoppingPoint.leftLabel;
                                num3 = new int?(transferableCountToTransferStoppingPoint.threshold);
                            }
                        }
                        arrowRect.x -= arrowRect.width;
                        if (Widgets.ButtonText(arrowRect, label, true, true, true))
                        {
                            if (num3 != null)
                            {
                                trad.AdjustTo(num3.Value);
                            }
                            else if (num == 1)
                            {
                                trad.AdjustTo(trad.GetMaximumToTransfer());
                            }
                            else
                            {
                                trad.AdjustTo(trad.GetMinimumToTransfer());
                            }
                            Verse.Sound.SoundStarter.PlayOneShotOnCamera(SoundDefOf.Tick_High, null);
                        }
                    }
                }
                if (TradeUIParameters.Singleton.isDrawingColonyItems && trad.CanAdjustBy(-num * num2).Accepted)
                {
                    Rect arrowButtonRect = new Rect(miniRect.x + 55f + EDGE_MARGIN + 10, rect.y, 30f, rect.height);
                    if (onlyHasOneItem)
                    {
                        arrowButtonRect.width += arrowButtonRect.width;
                    }
                    if (Widgets.ButtonText(arrowButtonRect, ">", true, true, true))
                    {
                        trad.AdjustBy(-num * num2);
                        Verse.Sound.SoundStarter.PlayOneShotOnCamera(SoundDefOf.Tick_Low, null);
                    }
                    if (!onlyHasOneItem)
                    {
                        string label2 = ">>";
                        int? num5 = null;
                        int num6 = 0;
                        for (int j = 0; j < TransferableUIUtility.stoppingPoints.Count; j++)
                        {
                            TransferableCountToTransferStoppingPoint transferableCountToTransferStoppingPoint2 = TransferableUIUtility.stoppingPoints[j];
                            if (positiveCountDirection == TransferablePositiveCountDirection.Destination)
                            {
                                if (trad.CountToTransfer < transferableCountToTransferStoppingPoint2.threshold && (transferableCountToTransferStoppingPoint2.threshold < num6 || num5 == null))
                                {
                                    label2 = transferableCountToTransferStoppingPoint2.rightLabel;
                                    num5 = new int?(transferableCountToTransferStoppingPoint2.threshold);
                                }
                            }
                            else if (trad.CountToTransfer > transferableCountToTransferStoppingPoint2.threshold && (transferableCountToTransferStoppingPoint2.threshold > num6 || num5 == null))
                            {
                                label2 = transferableCountToTransferStoppingPoint2.rightLabel;
                                num5 = new int?(transferableCountToTransferStoppingPoint2.threshold);
                            }
                        }
                        arrowButtonRect.x += arrowButtonRect.width;
                        if (Widgets.ButtonText(arrowButtonRect, label2, true, true, true))
                        {
                            if (num5 != null)
                            {
                                trad.AdjustTo(num5.Value);
                            }
                            else if (num == 1)
                            {
                                trad.AdjustTo(trad.GetMinimumToTransfer());
                            }
                            else
                            {
                                trad.AdjustTo(trad.GetMaximumToTransfer());
                            }
                            Verse.Sound.SoundStarter.PlayOneShotOnCamera(SoundDefOf.Tick_Low, null);
                        }
                    }
                }
            }

            // Draw arrow texture
            if (trad.CountToTransfer != 0)
            {
                float textBoxCenter = 0f;
                if (!trad.Interactive || readOnly)
                {
                    // TODO: shift this a little left/right to accomodate the larger text
                    textBoxCenter = miniRect.center.x;
                    if (trad.CountToTransfer > 0)
                        textBoxCenter -= 15;
                    else
                        textBoxCenter += 15;
                }
                else if (TradeUIParameters.Singleton.isDrawingColonyItems)
                {
                    textBoxCenter = miniRect.xMin + 30f;
                }
                else
                {
                    textBoxCenter = miniRect.xMax - 30f - 2.5f;
                }
                Rect position = new Rect(textBoxCenter - (float)(TransferableUIUtility.TradeArrow.width / 2),
                    miniRect.y + miniRect.height / 2f - (float)(TransferableUIUtility.TradeArrow.height / 2),
                    (float)TransferableUIUtility.TradeArrow.width,
                    (float)TransferableUIUtility.TradeArrow.height);
                TransferablePositiveCountDirection positiveCountDirection2 = trad.PositiveCountDirection;
                if ((positiveCountDirection2 == TransferablePositiveCountDirection.Source && trad.CountToTransfer > 0) || (positiveCountDirection2 == TransferablePositiveCountDirection.Destination && trad.CountToTransfer < 0))
                {
                    position.x += position.width;
                    position.width *= -1f;
                }
                GUI.DrawTexture(position, TransferableUIUtility.TradeArrow);
            }
            //GUI.Label(miniRect, "|||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||");

            // Skip vanilla behavior
            return false;
        }
    }
}