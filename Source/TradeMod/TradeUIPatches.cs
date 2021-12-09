using Verse;
using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using UnityEngine;

/*
 * TODO list
 * Fix silver left/right arrow no aligned properly
 * Fix not having enough width for the two scroll rects to fit item names (increase width?)
 * Determine what "flash" is in Harmony_TransferableUIUtility_DoCountAdjustInterfaceInternal and if it has correct pixel coords
 * Move silver trade amount to be aligned with the center
 * Fix "Positive numbers buy. Negative numbers sell." text not aligned above silver (remove altogether?)
 * Move our silver amount further to our side (maybe have silver in each window left/right and then have the scroll rect below them?)
 * Fix scroll Rect height being incorrect (currently is the height of all the items rather than height of each list independently)
 * Fix unintuitive behavior that if I sell/buy all of an item, it is strange to reverse this move
 * * for items that exist on both sides (steel), it works well
 * * for items that only exist on one side, once it is queued to switch sides then you can't use any buttons
 * * can't swap the buttons because we only have space for two buttons. They both have to be > and >>
 * * if the item doesn't exist on the other side, we could make it visible? I queue pants to sell, trader doesn't have it, add a pants slot with << < arrows to return it to me? visible price will not be correct
 * Test with various traders
 * Test multiplayer
 */

namespace TradeUI
{
    [StaticConstructorOnStartup]
    static class TradeUIPatches
    {
        static TradeUIPatches()
        {
            new Harmony("rimworld.hobtook.tradeui").PatchAll();
        }
    }

    [HarmonyPatch(typeof(RimWorld.Dialog_Trade), "PostOpen")]
    static class Harmony_DialogTrade_PostOpen
    {
        static void Prefix()
        {
            Log.Message("[TradeUI] Dialog_Trade.PostOpen prefix");
            TradeUIParameters.Singleton.Reset();
        }
    }

    [HarmonyPatch(typeof(RimWorld.TradeUI), "DrawTradeableRow")]
    static class Harmony_TradeUI_DrawTradeableRow
    {
        // TODO: I need the Rect parameter
        static void Prefix()
        {
            //Log.Message("[TradeUI] TradeUI.DrawTradeableRow prefix");
            // TODO: we need to only draw ours/theirs
            // It may be easier to avoid calling this entirely from FillMainRect()
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
        /*static void Prefix(ref UnityEngine.Rect inRect)
        {
            Log.Message($"[TradeUI] Dialog_Trade.DoWindowContents prefix");
            GUI.EndGroup();
            // TODO: I think we need a wider rect here to accomodate the two lists
            GUI.Label(inRect, "------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------");
            inRect.xMin -= 250;
            inRect.xMax += 250;
            GUI.BeginGroup(inRect);
            GUI.Label(inRect, "....................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................");
            // TODO: override drawing silver to align better with columns (maybe draw silver at top of each mini trade window?)
            // TODO: possibly override us/them negotiator info to align better with columns
            // TODO: override trade deal amount (offset to the right in vanilla UI)
        }

        static void Postfix()
        {
            GUI.EndGroup();
        }*/
    }

    [HarmonyPatch(typeof(RimWorld.Dialog_Trade), "FillMainRect")]
    static class Harmony_DialogTrade_FillMainRect
    {
        // We may need to override this
        static bool Prefix(ref UnityEngine.Rect mainRect, ref List<Tradeable> ___cachedTradeables, ref Dialog_Trade __instance)
        {
            Log.Message($"[TradeUI] Dialog_Trade.FillMainRect prefix");
            Log.Message($"[TradeUI] FillMainRect size ({mainRect.width},{mainRect.height})");
            Text.Font = GameFont.Small;
            float height = 6f + (float)___cachedTradeables.Count * 30f;

            // Draw left view
            float halfWidth = mainRect.width / 2f;
            Rect leftScrollRect = new Rect(0, mainRect.y, halfWidth, mainRect.height);
            Rect leftInnerRect = new Rect(0, 0, (leftScrollRect.width - 16f), height);
            Widgets.BeginScrollView(leftScrollRect, ref TradeUIParameters.Singleton.scrollPositionLeft, leftInnerRect, true);
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
                    Rect rect = new Rect(0, num, leftInnerRect.width, 30f);
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
            Rect rightScrollRect = new Rect(halfWidth, mainRect.y, halfWidth, mainRect.height);
            Rect rightInnerRect = new Rect(0, 0, (rightScrollRect.width - 16f), height);
            Widgets.BeginScrollView(rightScrollRect, ref TradeUIParameters.Singleton.scrollPositionRight, rightInnerRect, true);
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

        static void MyDrawTradableRow(Rect mainRect, Tradeable trad, int index, bool isOurs)
        {
            if (index % 2 == 1)
            {
                Widgets.DrawLightHighlight(mainRect);
            }
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

            if (!trad.TraderWillTrade)
            {
                // Since no price will be shown, will we occupy more space
                xPosition -= 290f;
                Rect rect5 = new Rect(xPosition, 0f, 290f, mainRect.height);
                // But don't actually consume this space since the price will be "drawn" as empty
                xPosition += 100;

                RimWorld.TradeUI.DrawWillNotTradeText(rect5, "TraderWillNotTrade".Translate());
            }
            else if (ModsConfig.IdeologyActive && TransferableUIUtility.TradeIsPlayerSellingToSlavery(trad, TradeSession.trader.Faction) && !new HistoryEvent(HistoryEventDefOf.SoldSlave, TradeSession.playerNegotiator.Named(HistoryEventArgsNames.Doer)).DoerWillingToDo())
            {
                // Since no price will be shown, will we occupy more space
                xPosition -= 290f;
                Rect rect5 = new Rect(xPosition, 0f, 290f, mainRect.height);
                // But don't actually consume this space since the price will be "drawn" as empty
                xPosition += 100;

                RimWorld.TradeUI.DrawWillNotTradeText(rect5, "NegotiatorWillNotTradeSlaves".Translate(TradeSession.playerNegotiator));
                if (Mouse.IsOver(rect5))
                {
                    Widgets.DrawHighlight(rect5);
                    TooltipHandler.TipRegion(rect5, "NegotiatorWillNotTradeSlavesTip".Translate(TradeSession.playerNegotiator, TradeSession.playerNegotiator.Ideo.name));
                }
            }
            else
            {
                xPosition -= 190f;
                Rect rect5 = new Rect(xPosition, 0f, 190f, mainRect.height);
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
                xPosition -= 100f;
                Rect rect6 = new Rect(xPosition, 0f, 100f, mainRect.height);
                Text.Anchor = TextAnchor.MiddleRight;
                RimWorld.TradeUI.DrawPrice(rect6, trad, isOurs ? TradeAction.PlayerSells : TradeAction.PlayerBuys);

                // draw owned amount
                xPosition -= 75f;
                Rect rect7 = new Rect(xPosition, 0f, 75f, mainRect.height);
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
                xPosition -= 175f;
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

        [HarmonyPatch(typeof(RimWorld.TransferableUIUtility), "DoCountAdjustInterfaceInternal")]
        static class Harmony_TransferableUIUtility_DoCountAdjustInterfaceInternal
        {
            static bool Prefix(Rect rect, Transferable trad, int index, int min, int max, bool flash, bool readOnly)
            {
                Log.Message("[TradeUI] TransferableUIUtility.DoCountAdjustInterfaceInternal prefix");
                rect = rect.Rounded();

                const float EDGE_MARGIN = 7f;
                const float ARROW_MARGIN = 10f;
                // theirs
                // [60 arrows][10 margin to fit arrow][60 text box][5 margin]
                // ours
                // [60 text box][10 margin to fit arrow][60 arrows][5 margin]
                // Need a width of 125

                Rect miniRect = new Rect(rect.xMax - ARROW_MARGIN - EDGE_MARGIN - 120f , rect.center.y - 12.5f, 120f + ARROW_MARGIN, 25f).Rounded();
                //GUI.Label(miniRect, "|||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||");

                // TODO: WHAT IS THIS? Are these pixel coords correct?
                if (flash)
                {
                    if (TradeUIParameters.Singleton.isDrawingColonyItems)
                        GUI.DrawTexture(new Rect(rect.x, rect.center.y - 12.5f, 90f, 25f).Rounded(), TransferableUIUtility.FlashTex);
                    else
                        GUI.DrawTexture(miniRect, TransferableUIUtility.FlashTex);
                }

                TransferableOneWay transferableOneWay = trad as TransferableOneWay;
                bool flag = transferableOneWay != null && transferableOneWay.HasAnyThing && transferableOneWay.AnyThing is Pawn && transferableOneWay.MaxCount == 1;
                if (!trad.Interactive || readOnly)
                {
                    if (flag)
                    {
                        bool flag2 = trad.CountToTransfer != 0;
                        Widgets.Checkbox(miniRect.position, ref flag2, 24f, true, false, null, null);
                    }
                    else
                    {
                        GUI.color = ((trad.CountToTransfer == 0) ? TransferableUIUtility.ZeroCountColor : Color.white);
                        Text.Anchor = TextAnchor.MiddleCenter;
                        Widgets.Label(miniRect, trad.CountToTransfer.ToStringCached());
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
                    bool onlyHasOneItem = trad.GetRange() == 1;
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

                if (trad.CountToTransfer != 0)
                {
                    // TODO: fix silver having its arrow off-center
                    float textBoxCenter = 0f;
                    if (TradeUIParameters.Singleton.isDrawingColonyItems)
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
}